// BabelLicense.cpp, V1, (C) 2013, Darren Whobrey.

#include <windows.h>
#include <iostream>
#include <cstdlib>
#include <string>
using namespace std;
using std::cout;
using std::cerr;
using std::endl;
using std::string;
using std::exit;

#include "cryptlib.h"
#include "base32.h"
#include "aes.h"
#include "modes.h"
#include "osrng.h"
#include "hex.h"
using CryptoPP::HexEncoder;
using CryptoPP::HexDecoder;
using CryptoPP::AutoSeededRandomPool;
using CryptoPP::Exception;
using CryptoPP::Base32Encoder;
using CryptoPP::Base32Decoder;
using CryptoPP::StringSink;
using CryptoPP::StringSource;
using CryptoPP::StreamTransformationFilter;
using CryptoPP::AES;
using CryptoPP::CFB_Mode;

#define CIPHER_MODE CFB_Mode
#define CIPHER AES

#include "BabelLicense.h"

// The AES crypt key used to encrypt licenses.
byte LicKey[] = { 0x30, 0x17, 0xf0, 0xe3, 0x14, 0xa9, 0xc5, 0x7d,
				  0x08, 0xf2, 0x99, 0x51, 0x2e, 0x11, 0x8a, 0xdf };

// The IV for the license data block.
// Size of IVKey must be same as block size, i.e. sizeof LicStruct.
byte IVKey[] = { 
	0xf2,0x9e,0x7d,0x85,0xec,
	0x1f,0x41,0x79,0x10,0x62,
	0xa2,0xc9,0xdc,0xe2,0x8c };

byte PermutedIVKey[15];
 
char* BLError[] = {
	"ok", // 0 BL_ERR_OK
	"Command option not recognized.", // 1 BL_ERR_CMD_OPTION
	"License length not equal to 29.", // 2 BL_ERR_LIC_KEY_LEN
	"Invalid license checksum.", // 3 BL_ERR_LIC_CHK_SUM
	"Printing format problem.", // 4 BL_ERR_LIC_PRINT
	"General exception.", // 5 BL_ERR_LIC_EXCEPTION
	"Crypt exception.", // 6 BL_ERR_CRY_EXCEPTION
	"ComputerKey must be 8 chars long.", // 7 BL_ERR_LIC_COMP_LEN
	"ComputerKey checksum error.", // 8 BL_ERR_LIC_COMP_CHK
	"Not enough args.", // 9 BL_ERR_ARGS_NUM
	"ExpiryDate must be 6 chars long.", // 10 BL_ERR_EXP_DATE_LEN
	"ExpiryDate invalid value.", // 11 BL_ERR_EXP_DATE_RANGE
	"AuthCode must be 4 chars long.", // 12 BL_ERR_AUTH_CODE_LEN
	"No module ids found.", // 13 BL_ERR_NO_MODULE_IDS
	"No license was found for module.", // 14 BL_ERR_NO_MOD_LICENSE
	"All licenses for module have expired.", // 15 BL_ERR_MOD_LIC_EXPIRED
	"No authenticating license found.", // 16 BL_ERR_MOD_UNAUTHENTICATED
};

int printError(int errNo) {
	if(errNo>=0 && errNo<(sizeof(BLError)/sizeof(char*))) {
		printf("Error: %s\n",BLError[errNo]);
	} else {
		printf("Unknown error number:%d.\n",errNo);
	}
	return errNo;
}

void permuteIVKey(char rndChar) {
	int k,j,n=rndChar-'A';
	byte v;
	if(n<1) n+='A'-'0';
	n%=15;
	memcpy_s(PermutedIVKey,sizeof(PermutedIVKey),IVKey,sizeof(IVKey));
	for(k=0;k<n;k++) {
		v = PermutedIVKey[0];
		for(j=0;j<sizeof(PermutedIVKey)-1;j++) {
			PermutedIVKey[j]=PermutedIVKey[j+1];
		}
		PermutedIVKey[sizeof(PermutedIVKey)-1]=v;
	}
	PermutedIVKey[0]=rndChar;
}

WORD calcCRC(byte *ptr, byte count) {
    WORD crc = CRC_SEED;
    byte i;
    while (count-- > 0) {
        crc = crc ^ *ptr++;
        for (i=8; i>0; i--) {
            if (crc & 0x0001)
                crc = (crc >> 1) ^ CRC_POLY;
            else
                crc >>= 1;
        }
    }
    return crc;
}

// Writes LicStruct to buffer suitable for inputing to blt.
// Output format: <ComputerKey> <ExpiryDate> <AuthCode> <RndChar> {<ModuleId> ' '}+
// Returns BL_ERR_OK on success.
int sprintLicense(char* buffer,size_t bufsiz,LicStruct* q,char rndChar) {
	try {
		buffer[0]=0;
		// Convert ComputerKey to base32 for display:
		char tmp[sizeof(DWORD)+1]; // Need to use a buffer to make compKey len base32 divisible.
		string encoded;
		*((int*)tmp)=q->computerKey;
		WORD chkSum=calcCRC((byte*)tmp,sizeof(DWORD));
		tmp[sizeof(DWORD)] = (chkSum&0xff);
		string compKey((const char*)tmp,sizeof(DWORD)+1);
		CryptoPP::StringSource bb(compKey,true,new CryptoPP::Base32Encoder(new CryptoPP::StringSink(encoded),true,8));
		int year = q->expiryDate / 12;
		year+=2013;
		int month = q->expiryDate % 12;
		++month;
		int j=sprintf_s(buffer,bufsiz,"%s %4d%02d %04x %c",encoded.data(),year,month,q->authCode,rndChar);
		if(j<0) return BL_ERR_LIC_PRINT;
		int n=q->numModules;
		if(n>0) {
			for(int k=0;k<n;k++) {
				j+=sprintf_s(buffer+j,bufsiz-j," %02x",q->moduleIds[k]);
			}
		} 
		buffer[j]=0;
	}
	catch( CryptoPP::Exception) { // & e) {
		// std::cerr << e.what() << std::endl;
		return BL_ERR_CRY_EXCEPTION;
	}
	catch(...) {
		// std::cerr << "Unknown Error" << std::endl;
		return BL_ERR_LIC_EXCEPTION;
	} 
	return BL_ERR_OK;
}

// Parse a license string and return result in q and rndChar.
// Returns BL_ERR_OK on success.
int parseLicense(char* p, LicStruct *q, char & rndChar) {
	try {
		rndChar=0;
		int k =strlen(p);
		if(k!=29)  return BL_ERR_LIC_KEY_LEN;

		// Get RndChar & remove.
		rndChar = p[--k];

		// Convert from base32 to data.
		string licText((const char*)p,k);
		string decoded;
		CryptoPP::StringSource ss(licText, true,new CryptoPP::Base32Decoder(new CryptoPP::StringSink(decoded)));

		// Permute IVKey.
		permuteIVKey(rndChar);

		// Decrypt.
		std::string RecoveredText;
		CryptoPP::CIPHER_MODE<CryptoPP::CIPHER>::Decryption	Decryptor( LicKey, sizeof(LicKey), PermutedIVKey );

		// Decryption
		CryptoPP::StringSource( decoded, true,
			new CryptoPP::StreamTransformationFilter( Decryptor,
			new CryptoPP::StringSink( RecoveredText ) ) );

		LicStruct* r = (LicStruct*)RecoveredText.data();
		// Check CheckSum.
		WORD chkSum=calcCRC((byte*)r,CheckLength);
		if(r->checkSum != (chkSum & 0xff)) return BL_ERR_LIC_CHK_SUM;
		memcpy_s(q,sizeof(LicStruct),r,sizeof(LicStruct));
	}
	catch( CryptoPP::Exception) { // & e) {
		// std::cerr << e.what() << std::endl;
		return BL_ERR_CRY_EXCEPTION;
	}
	catch(...) {
		// std::cerr << "Unknown Error" << std::endl;
		return BL_ERR_LIC_EXCEPTION;
	} 
	return BL_ERR_OK;
}

/*
 Generate LicenseKey.
 Args in argv:
	<ComputerKey> <ExpiryDate> <AuthCode> <RndChar> {<ModuleId>}+
 where:
   <ComputerKey> = Base32 encoding, 8 chars long.
   <ExpiryDate> = YYYYMM, year: {2013..2034}, month: {1..12}.
   <AuthCode> = XXXX, 4 char hex value.
   <RndChar> = Base32 char to seed IV.
   <ModuleId> = XX, 2 char hex value, up to 6 modules.
   <LicenseKey> = 29 digit key to decode.
   argc>=5.
 Returns BL_ERR_OK on success.
*/
int generateLicense(char* buffer, size_t bufsiz,int argc, char* argv[]) {
	try {
		char* p;
		int n=0,j,k,year=0,month=0;
		WORD chkSum;
		byte rndChar=0,chkLo;
		LicStruct licData;
		if(argc<5) return BL_ERR_ARGS_NUM;
		// Decode base32 ComputerKey text to DWORD.
		p = argv[n++];
		k=strlen(p);
		if(k!=8) return BL_ERR_LIC_COMP_LEN;
		string compText((const char*)p,k);
		string decoded;
		CryptoPP::StringSource ss(compText, true,new CryptoPP::Base32Decoder(new CryptoPP::StringSink(decoded)));
		licData.computerKey=*((DWORD*)decoded.data());
		chkLo = decoded[sizeof(DWORD)];
		chkSum=calcCRC((byte*)&(licData.computerKey),sizeof(DWORD));
		if((chkSum&0xff)!=chkLo) return BL_ERR_LIC_COMP_CHK;

		// Decode ExpiryDate.
		p = argv[n++];
		k=strlen(p);
		if(k!=6)  return BL_ERR_EXP_DATE_LEN;
		sscanf_s(p,"%4d%2d",&year,&month);
		if(year<2013||year>2034||month<1||month>12) return BL_ERR_EXP_DATE_RANGE;
		year-=2013; --month;
		licData.expiryDate = year*12+month;

		// Get AuthoCode.
		p = argv[n++];
		k=strlen(p);
		if(k!=4) return BL_ERR_AUTH_CODE_LEN;
		sscanf_s(p,"%x",&k);
		licData.authCode = (WORD)k;

		// Get IVKey RndChar.
		rndChar = argv[n++][0];

		// Get ModuleIds.
		licData.numModules=0;
		while((n<argc) && (licData.numModules<6)) {
			sscanf_s(argv[n++],"%x",&k);
			licData.moduleIds[licData.numModules++]=(byte)k;
		}

		if(licData.numModules<1) return BL_ERR_NO_MODULE_IDS;

		// Fill unused ModuleIds with random values.
		j=sizeof(licData.moduleIds) - licData.numModules;
		if(j>0) {
			AutoSeededRandomPool prng;
			prng.GenerateBlock(licData.moduleIds+licData.numModules, j);
		}

		// Compute CheckSum.
		chkSum=calcCRC((byte*)&licData,CheckLength);
		licData.checkSum = chkSum & 0xff;

		// Permute IVKey:
		permuteIVKey(rndChar);

		// Encrypt.
		std::string PlainText((char*)&licData,sizeof(licData));
		std::string CipherText;
		CryptoPP::CIPHER_MODE<CryptoPP::CIPHER>::Encryption	Encryptor( LicKey, sizeof(LicKey), PermutedIVKey );
		CryptoPP::StringSource( PlainText, true,
			new CryptoPP::StreamTransformationFilter( Encryptor,
			new CryptoPP::StringSink(CipherText) ));

		// Convert to Base32.
		string encoded;
		CryptoPP::StringSource bb(CipherText,true,new CryptoPP::Base32Encoder(new CryptoPP::StringSink(encoded),true,5,"-"));	

		// Output and add trailing RndChar.
		sprintf_s(buffer,bufsiz,"%s%c",encoded.data(),rndChar);
	}
	catch( CryptoPP::Exception) { // & e) {
		// std::cerr << e.what() << std::endl;
		return BL_ERR_CRY_EXCEPTION;
	}
	catch(...) {
		// std::cerr << "Unknown Error" << std::endl;
		return BL_ERR_LIC_EXCEPTION;
	} 
	return BL_ERR_OK;
}
