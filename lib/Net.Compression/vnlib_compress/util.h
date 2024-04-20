/*
* Copyright (c) 2024 Vaughn Nugent
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

/*
* If a custom allocator is enabled, use the native heap api
* header and assume linking is enabled. Heap functions below
* will be enabled when heapapi.h is included.
*/
#ifdef VNLIB_CUSTOM_MALLOC_ENABLE
	/* Since static linking ie snabled, heapapi must have extern symbol not dllimport */
	#define VNLIB_HEAP_API extern	
	#include <NativeHeapApi.h>
#endif

#if defined(_MSC_VER) || defined(WIN32) || defined(_WIN32)
	#define IS_WINDOWS
#endif

#if defined(IS_WINDOWS) || defined(inline) || defined(__clang__)
	#define _cp_fn_inline inline
#elif defined(__STDC_VERSION__) && __STDC_VERSION__ >= 199901L /* C99 allows usage of inline keyword */
	#define _cp_fn_inline inline
#elif defined(__GNUC__) || defined(__GNUG__)
	#define _cp_fn_inline __inline__
#else
	#define _cp_fn_inline
	#pragma message("Warning: No inline keyword defined for this compiler")
#endif

#ifndef NULL
	#define NULL ((void*)0)
#endif /* !NULL */

#ifndef TRUE
	#define TRUE 1
#endif /* !TRUE */

#ifndef FALSE
	#define FALSE 0
#endif /* !FALSE */

/*
* Add debug runtime assertions
*/
#ifdef DEBUG
	#include <assert.h>
#else
	#define assert(x) {}
#endif

#define CHECK_NULL_PTR(ptr) if(!ptr) return ERR_INVALID_PTR;

#ifdef NATIVE_HEAP_API	/* Defined in the NativeHeapApi */

	#include <stddef.h>

	/*
	* Add overrides for malloc, calloc, and free that use
	* the nativeheap api to allocate memory
	* 
	* Inline fuctions are used to enforce type safety and 
	* api consistency.
	*/

	static _cp_fn_inline void* vnmalloc(size_t num, size_t size)
	{
		return heapAlloc(heapGetSharedHeapHandle(), num, size, FALSE);
	}

	static _cp_fn_inline void* vncalloc(size_t num, size_t size)
	{
		return heapAlloc(heapGetSharedHeapHandle(), num, size, TRUE);
	}

	static _cp_fn_inline void vnfree(void* ptr)
	{
	#ifdef DEBUG

		ERRNO result;
		result = heapFree(heapGetSharedHeapHandle(), ptr);

		/* track failed free results */
		assert(result > 0);

	#else
	
		heapFree(heapGetSharedHeapHandle(), ptr);
	
	#endif
		
	}

#else

	/*
	* Required for built-in memory api
	*/
	#include <stdlib.h>

	/*
	* Stub method for malloc. All calls to vnmalloc should be freed with vnfree.
	*/
	#define vnmalloc(num, size) malloc(num * size)

	/*
	* Stub method for free
	*/
	#define vnfree(ptr) free(ptr)

	/*
	* Stub method for calloc. All calls to vncalloc should be freed with vnfree.
	*/
	#define vncalloc(num, size) calloc(num, size)

#endif /* NATIVE_HEAP_API */

#endif /* !UTIL_H_ */