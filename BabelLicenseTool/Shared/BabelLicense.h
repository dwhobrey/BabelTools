// BabelLicense.h, V1, (C) 2013, Darren Whobrey.

#define BL_ERR_OK 0 // No error.
#define BL_ERR_CMD_OPTION 1 // Command option not recognized.
#define BL_ERR_LIC_KEY_LEN 2 // License length not equal to 29.
#define BL_ERR_LIC_CHK_SUM 3 // Invalid license checksum.
#define BL_ERR_LIC_PRINT 4 // Printing format problem.
#define BL_ERR_LIC_EXCEPTION 5 // General exception.
#define BL_ERR_CRY_EXCEPTION 6 // Crypt exception.
#define BL_ERR_LIC_COMP_LEN 7 // Computer key length not equal to 8.
#define BL_ERR_LIC_COMP_CHK 8 // ComputerKey checksum error.
#define BL_ERR_ARGS_NUM 9 // Not enough args.
#define BL_ERR_EXP_DATE_LEN 10 // ExpiryDate must be 6 chars long.
#define BL_ERR_EXP_DATE_RANGE 11 // ExpiryDate invalid value.
#define BL_ERR_AUTH_CODE_LEN 12 // AuthCode must be 4 chars long.
#define BL_ERR_NO_MODULE_IDS 13 // No module ids found.
#define BL_ERR_NO_MOD_LICENSE 14 // No license was found for module.
#define BL_ERR_MOD_LIC_EXPIRED 15 // All licenses for module have expired.
#define BL_ERR_MOD_UNAUTHENTICATED 16 // No authenticating license found.

#define CheckLength (sizeof(LicStruct)-1) // Number of bytes in LicStruct to checksum.

#define CRC_SEED  0xFFFFu
#define CRC_POLY  0xA001u

#pragma pack(push,1)
typedef /*__declspec(align(8))*/ struct {
	DWORD computerKey;
	WORD authCode;
	byte expiryDate;
	byte numModules;
	byte moduleIds[6];
	byte checkSum;
} LicStruct;
#pragma pack(pop) 

#pragma pack(push,1)
typedef struct {
	DWORD hash;
	byte idKind;
	byte textEncoding[8];
} IdentityStruct;
#pragma pack(pop) 

#pragma pack(push,1)
typedef struct {
	WORD year;
	WORD month;
	byte numModules;
	byte moduleIds[6];
} InfoStruct;
#pragma pack(pop) 

extern byte LicKey[];
extern byte IVKey[];
extern char* BLError[];
extern void permuteIVKey(char rndChar);
extern WORD calcCRC(byte *ptr, byte count);
extern int printError(int errNo);
extern int sprintLicense(char* buffer,size_t bufsiz,LicStruct* q,char rndChar);
extern int parseLicense(char* p, LicStruct *q, char & rndChar);
extern int generateLicense(char* buffer, size_t bufsiz,int argc, char* argv[]);
