/*
* Copyright(c) 2025 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_monocypher
* File: platform.h
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


/*
*	Contains platform specific defintions
*/

#pragma once

#ifndef _VN_MONOCYPHER_PLATFORM_H
#define _VN_MONOCYPHER_PLATFORM_H

#if defined(_MSC_VER) || defined(WIN32) || defined(_WIN32)
	#define _VN_IS_WINDOWS
#elif defined(__linux__) || defined(__unix__) || defined(__posix__)
	#define _VN_IS_LINUX
#elif defined(__APPLE__) || defined(__MACH__)
	#define _VN_IS_MAC
#endif

/*
* Define supported inline defintions for various compilers
* and C standards
*/

#if defined(_VN_IS_WINDOWS) || defined(inline) || defined(__clang__)
	#define _vn_inline inline
#elif defined(__STDC_VERSION__) && __STDC_VERSION__ >= 199901L /* C99 allows usage of inline keyword */
	#define _vn_inline inline
#elif defined(__GNUC__) || defined(__GNUG__)
	#define _vn_inline __inline__
#else
	#define _vn_inline
	#pragma message("Warning: No inline keyword defined for this compiler")
#endif

/* NULL */
#ifndef NULL
	#define NULL ((void*)0)
#endif /*  !NULL */

#ifndef _In_
	#define _In_
#endif

#ifndef _VN_IS_WINDOWS
	#define TRUE 1
	#define FALSE 0
#endif // True and False


#ifdef DEBUG
	/* Must include assert.h for assertions */
	#include <assert.h> 
	#define DEBUG_ASSERT(x) assert(x);
	#define DEBUG_ASSERT2(x, message) assert(x && message);	

	/*
	* Compiler enabled static assertion keywords are
	* only available in C11 and later. Later versions
	* have macros built-in from assert.h so we can use
	* the static_assert macro directly.
	*
	* Static assertions are only used for testing such as
	* sanity checks and this library targets the c89 standard
	* so static_assret very likely will not be available.
	*/
	#if defined(__STDC_VERSION__) && __STDC_VERSION__ >= 201112L
	#define STATIC_ASSERT(x, m) static_assert(x, m);
#elif !defined(STATIC_ASSERT)
	#define STATIC_ASSERT(x, m)
	#pragma message("Static assertions are not supported by this language version")
#endif

#else
	#define DEBUG_ASSERT(x)
	#define DEBUG_ASSERT2(x, message)
	#define STATIC_ASSERT(x, m)
#endif


#endif // !VNCP_PLATFORM_H
