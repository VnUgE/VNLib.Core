/*
* Copyright (c) 2023 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_compress
* File: util.h
*
* vnlib_compress is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* vnlib_compress is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with vnlib_compress. If not, see http://www.gnu.org/licenses/.
*/

#pragma once

#ifndef UTIL_H_
#define UTIL_H_

#include <stdlib.h>

/*
* Stub missing types and constants for GCC
*/
#if defined(__GNUC__)
#define inline __inline__
#define VNLIB_EXPORT __attribute__((visibility("default")))
#define VNLIB_CC 
#elif defined(_MSC_VER)
#define VNLIB_EXPORT __declspec(dllexport)
#define VNLIB_CC __cdecl
#endif /* WIN32 */

#define ERR_INVALID_PTR -1
#define ERR_OUT_OF_MEMORY -2

#define TRUE 1;
#define FALSE 0;

#ifndef NULL
#define NULL 0
#endif /* !NULL */

#ifndef _In_
#define _In_
#endif // !_In_


/*
* Stub method for malloc. All calls to vnmalloc should be freed with vnfree.
*/
#define vnmalloc(size) malloc(size)

/*
* Stub method for free
*/
#define vnfree(ptr) free(ptr)

/*
* Stub method for calloc. All calls to vncalloc should be freed with vnfree.
*/
#define vncalloc(num, size) calloc(num, size)


#endif /* !UTIL_H_ */