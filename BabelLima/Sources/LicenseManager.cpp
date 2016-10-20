//  LicenseManager.cpp.
#include <windows.h>
#include <ctime>
#include <iostream>
#include <cstdlib>
#include <string>
#include <hash_map>

#include "BabelLicense.h"

using namespace std;
using namespace stdext;

typedef hash_map<string,LicStruct> LicenseTableType;
typedef pair <string,LicStruct> LicenseTablePair;

LicenseTableType Licenses;

// Checks if the license applies to the module.
bool licenseForModule(LicStruct & licData,byte moduleId) {
	for(int k=0;k<licData.numModules;k++) {
		if(licData.moduleIds[k]==moduleId) return true;
	}
	return false;
}

// Checks if license has expired.
// Returns true if still valid.
bool licenseDateValid(LicStruct & licData) {
    time_t t = time(0);
    struct tm * now = localtime( & t );
	int year=1900+now->tm_year;
	int month=now->tm_mon;
	int licYear = (licData.expiryDate/12)+2013;
	int licMonth = licData.expiryDate%12;
	if(licYear>year) return true;
	if(licYear<year) return false;
	if(licMonth>=month) return true;
	return false;
}

// Checks that AuthCode matches that in the license.
bool licenseAuthenticate(LicStruct & licData,WORD authCode) {
	return licData.authCode==authCode;
}

extern "C" {

__declspec(dllexport) int __stdcall LimaStatus()  {
	return 100;
}

// Just extract the public info from a license and return result in q.
// Returns BL_ERR_OK on success.
__declspec(dllexport) int __stdcall InfoLicense(char* p, InfoStruct *q) {
	char rndChar=0;
	LicStruct licData;
	int err=parseLicense(p,&licData,rndChar);
	if(err==BL_ERR_OK) {
		q->year = (licData.expiryDate/12)+2013;
		q->month = licData.expiryDate%12;
		int n=licData.numModules;
		q->numModules = n;
		for(int k=0;k<n;k++) q->moduleIds[k] = licData.moduleIds[k];
	}
	return err;
}

// Clear Licenses.
__declspec(dllexport) void __stdcall ClearLicenses()  {
	Licenses.clear();
}

// Add License.
// Rejects invalid or out of date licenses.
// Returns BL_ERR_OK only if license is valid and not expired.
__declspec(dllexport) int __stdcall AddLicense(byte* p)  {
	if(p!=NULL) {
		string s((char*)p,strlen((char*)p));
		LicenseTableType::iterator t;
		t=Licenses.find(s);
		if(t==Licenses.end()) {
			LicStruct licData;
			char rndChar=0;
			int err=parseLicense((char*)p, &licData, rndChar);
			if(err!=BL_ERR_OK) return err;
			if(!licenseDateValid(licData)) return BL_ERR_MOD_LIC_EXPIRED;
			Licenses.insert(LicenseTablePair(s,licData));
		}
	}
	return BL_ERR_OK;
}

// Check Module is Licensed.
__declspec(dllexport) int __stdcall CheckModuleLicensed(byte moduleId)  {
	bool licensesExpired=false;
	for(LicenseTableType::iterator i = Licenses.begin(); i != Licenses.end(); i++) {
		if(licenseForModule(i->second,moduleId)) {
			if(licenseDateValid(i->second)) return BL_ERR_OK;
			licensesExpired=true;
		}
	}
	if(licensesExpired) return BL_ERR_MOD_LIC_EXPIRED;
	return BL_ERR_NO_MOD_LICENSE;
}

// Check Module is Authenticated.
__declspec(dllexport) int __stdcall CheckModuleAuthenticated(byte moduleId,WORD authCode)  {
	for(LicenseTableType::iterator i = Licenses.begin(); i != Licenses.end(); i++) {
		if(licenseForModule(i->second,moduleId)) {
			if(licenseDateValid(i->second) 
				&& licenseAuthenticate(i->second,authCode)) return BL_ERR_OK;
		}
	}
	return BL_ERR_MOD_UNAUTHENTICATED;
}

}