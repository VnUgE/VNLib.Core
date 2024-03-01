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

#include <stdlib.h>

/*
* Stub missing types and constants for GCC
*/

/*
* If a custom allocator is enabled, use the native heap api
* header and assume linking is enabled
*/
#ifdef VNLIB_CUSTOM_MALLOC_ENABLE
#include <NativeHeapApi.h>
#endif

#if defined(_MSC_VER) || defined(WIN32) || defined(_WIN32)
	#define IS_WINDOWS
#endif

//Set api export calling convention (allow used to override)
#ifndef VNLIB_CC
	#ifdef IS_WINDOWS
		//STD for importing to other languages such as .NET
		#define VNLIB_CC __stdcall
	#else
		#define VNLIB_CC 
	#endif
#endif // !VNLIB_CC

#ifndef VNLIB_EXPORT	//Allow users to disable the export/impoty macro if using source code directly
	#ifdef VNLIB_EXPORTING
		#ifdef IS_WINDOWS
			#define VNLIB_EXPORT __declspec(dllexport)
		#else
			#define VNLIB_EXPORT __attribute__((visibility("default")))
		#endif // IS_WINDOWS
	#else
		#ifdef IS_WINDOWS
			#define VNLIB_EXPORT __declspec(dllimport)
		#else
			#define VNLIB_EXPORT
		#endif // IS_WINDOWS
	#endif // !VNLIB_EXPORTING
#endif // !VNLIB_EXPORT


/* If not Windows, define inline */
#ifndef IS_WINDOWS
	#ifndef inline
		#define inline __inline__
	#endif // !inline
#endif // !IS_WINDOWS

//NULL
#ifndef NULL
	#define NULL ((void*)0)
#endif // !NULL

#ifndef TRUE
	#define TRUE 1
#endif // !TRUE

#ifndef FALSE
	#define FALSE 0
#endif // !FALSE

#ifndef _In_
	#define _In_
#endif

/*
* Add debug runtime assertions
*/
#ifdef DEBUG
	#include <assert.h>
#else
	#define assert(x) {}
#endif

/*
* ERRORS AND CONSTANTS
*/
#define ERR_INVALID_PTR -1
#define ERR_OUT_OF_MEMORY -2

#ifdef NATIVE_HEAP_API	/* Defined in the NativeHeapApi */
	/*
	* Add overrides for malloc, calloc, and free that use
	* the nativeheap api to allocate memory
	*/

	static inline void* vnmalloc(size_t num, size_t size)
	{
		return heapAlloc(heapGetSharedHeapHandle(), num, size, FALSE);
	}

	static inline void* vncalloc(size_t num, size_t size)
	{
		return heapAlloc(heapGetSharedHeapHandle(), num, size, TRUE);
	}

	static inline void vnfree(void* ptr)
	{
		ERRNO result;
		result = heapFree(heapGetSharedHeapHandle(), ptr);

		//track failed free results
		assert(result > 0);
	}

#else

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

#endif // NATIVE_HEAP_API

#endif /* !UTIL_H_ */