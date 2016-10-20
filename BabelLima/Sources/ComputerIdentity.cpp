//  ComputerIdentity.cpp.
#include <windows.h>
#include <iostream>
#include <cstdlib>
#include <string>
#include "cryptlib.h"
#include "base32.h"
#include "crc.h"

#include "BabelLicense.h"

using namespace std;
using CryptoPP::Base32Encoder;

extern char* GetHardwareKeys(DWORD * computerId, DWORD* mac);

extern "C" {

// Works out an Identity hash for this machine.
// Returns in struct pointed to by p:
// DWORD hash,
// byte idKind, this is H(ardDrive), C(computerId), or M(ac).
// The hash & chksum converted to text in Base32.
// If unable to compute hash, hash=0, hash string = '\0'.
__declspec(dllexport) void __stdcall GetIdentity(IdentityStruct* p)  {
	char * pSN;
	DWORD compId=0,mac=0; int k;
	p->hash=0;
	p->idKind=0;
	pSN = GetHardwareKeys(&compId,&mac);
	if(*pSN!='\0') {
		CryptoPP::CRC32 crc;
		crc.CalculateDigest((byte*)&(p->hash), (const byte*)pSN, strlen(pSN));
		p->idKind='H';
	} else if(compId!=0) {
		p->hash = compId;
		p->idKind='C';
	} else if(mac!=0) {
		p->hash = mac;
		p->idKind='M';
	}
	string encoded;
	string compKey((const char*)&(p->hash),sizeof(DWORD)+1);
	WORD chkSum=calcCRC((byte*)&(p->hash),sizeof(DWORD));
	compKey[sizeof(DWORD)]=(chkSum&0xff);
	CryptoPP::StringSource bb(compKey,true,new CryptoPP::Base32Encoder(new CryptoPP::StringSink(encoded),true,8));
	for(k=0;k<8;k++) p->textEncoding[k] = encoded[k];
}

// Converts text encoded Identity back to hash.
// On entry p->textEncoding is the identity text to convert.
// Returns in buffer:
// DWORD hash,
// byte idKind, this is H(ardDrive), C(computerId), or M(ac).
__declspec(dllexport) void __stdcall DecodeIdentity(IdentityStruct* p)  {
	CryptoPP::Base32Decoder textDecoder;
	textDecoder.Put((byte*)(p->textEncoding),sizeof(p->textEncoding));
	CryptoPP::word64 size = textDecoder.MaxRetrievable();
	textDecoder.Get((byte*)&(p->hash), (size_t)(sizeof(DWORD)+1));
}

}