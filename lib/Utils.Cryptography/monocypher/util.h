/*
* Copyright (c) 2023 Vaughn Nugent
*
* vnlib_monocypher is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* vnlib_monocypher is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with vnlib_monocypher. If not, see http://www.gnu.org/licenses/.
*/

#pragma once
#ifndef  VN_MONOCYPHER_UTIL_H

#if defined(__GNUC__)
	#define inline __inline__
	#define VNLIB_EXPORT __attribute__((visibility("default")))
	#define VNLIB_CC 
#elif defined(_MSC_VER)
	#define VNLIB_EXPORT __declspec(dllexport)
	#define VNLIB_CC __cdecl
#endif /* WIN32 */

#ifdef USE_MEM_UTIL

	/* Include stdlib for malloc */
	#include <stdlib.h>

	/* If a custom allocator is not defined, set macros for built-in function */
	#ifndef CUSTOM_ALLOCATOR

		/* malloc and friends fallback if not defined */
		#define vnmalloc(size) malloc(size)
		#define vncalloc(count, size) calloc(count, size)
		#define vnrealloc(ptr, size) realloc(ptr, size)
		#define vnfree(ptr) free(ptr)

	#endif /* !CUSTOM_ALLOCATOR */

	#ifdef WIN32

		/* required for memove on windows */
		#include <memory.h>

		#define _memmove(dst, src, size) memmove_s(dst, size, src, size)
	#else
		/* use string.h posix on non-win platforms */
		#include <string.h>

		#define _memmove memmove
	#endif /* WIN32 */

#endif //  USE_MEM_UTIL

#ifndef _In_
#define _In_
#endif

#define ERR_INVALID_PTR -1
#define ERR_OUT_OF_MEMORY -2

#define TRUE 1
#define FALSE 0

#ifndef NULL
#define NULL 0
#endif /* !NULL */

#define VALIDATE_PTR(ptr) if (!ptr) return ERR_INVALID_PTR

#endif /* !VN_MONOCYPHER_UTIL_H */