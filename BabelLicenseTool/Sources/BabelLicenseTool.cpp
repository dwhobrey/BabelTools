// BabelLicenseTool.cpp, V1, (C) 2013, Darren Whobrey.
#include <windows.h>
#include <iostream>
#include <cstdlib>
#include <string>

#include "BabelLicense.h"

using namespace std;

// Test params:
// -g 4R9JB5R2 201402 fe80 8 01 02 04 f0
// -d SKLJC-GHHT8-4PGNC-AQ47X-NUSD8
int __cdecl main(int argc, char* argv[]) {
	if((argc<2)||(strlen(argv[1])<2)) {
		printf("Babel License Tool, v1, 2013.\nCommand format:\n");
		printf("  blt [-g <ComputerKey> <ExpiryDate> <AuthCode> <RndChar> {<ModuleId> ' '}+]\n");
		printf("  blt [-d <LicenseKey>]\n");
		printf("  where:\n");
		printf("    <ComputerKey> = Base32 encoding, 8 chars long.\n");
		printf("    <ExpiryDate> = YYYYMM, year: {2013..2034}, month: {1..12}.\n");
		printf("    <AuthCode> = XXXX, 4 char hex value.\n");
		printf("    <RndChar> = Base32 char to seed IV.\n");
		printf("    <ModuleId> = XX, 2 char hex value, up to 6 modules.\n");
		printf("    <LicenseKey> = 29 digit key to decode.\n");
		return BL_ERR_CMD_OPTION;
	}
	try	{
		if(argv[1][1]=='g') {
			char outbuff[64];
			argv+=2; argc-=2;
			int err=generateLicense(outbuff, sizeof(outbuff),argc, argv);
			if(err==BL_ERR_OK) printf("%s",outbuff);
			else return printError(err);
		} else if(argv[1][1]=='d') { 
			LicStruct licData;
			char rndChar=0;
			int err = parseLicense(argv[2], &licData, rndChar);
			if(err!=BL_ERR_OK) return printError(err);
			char outbuff[100];
			err=sprintLicense(outbuff,sizeof(outbuff),&licData,rndChar);
			if(err==BL_ERR_OK) printf("%s\n",outbuff);
			else return printError(err);
		} else {
			printf("Command option not recognized: '%c'.",argv[1][1]);
			return BL_ERR_CMD_OPTION;
		}
	}
	catch(...) {
		std::cerr << "Unknown Error" << std::endl;
		return BL_ERR_LIC_EXCEPTION;
	} 
	return BL_ERR_OK;
}


