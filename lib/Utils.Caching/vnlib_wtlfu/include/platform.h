/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: platform.h
*
* This library is free software; you can redistribute it and/or
* modify it under the terms of the GNU Lesser General Public License
* as published by the Free Software Foundation; either version 2.1
* of the License, or (at your option) any later version.
*
* This library is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* Lesser General Public License for more details.
*
* You should have received a copy of the GNU Lesser General Public License
* along with vnlib_wtlfu. If not, see http://www.gnu.org/licenses/.
*/

/*
* Contains platform specific definitions for the vnlib_wtlfu library.
*/

#pragma once

#ifndef VN_WTLFU_PLATFORM_H
#define VN_WTLFU_PLATFORM_H

#if defined(_MSC_VER) || defined(WIN32) || defined(_WIN32)
    #define _VN_IS_WINDOWS
#elif defined(__linux__) || defined(__unix__) || defined(__posix__)
    #define _VN_IS_LINUX
#elif defined(__APPLE__) || defined(__MACH__)
    #define _VN_IS_MAC
#endif

/*
* Define supported inline definitions for various compilers
* and C standards
*/

#if defined(_VN_IS_WINDOWS) || defined(__clang__)
    #define _vn_inline inline
#elif defined(__STDC_VERSION__) && __STDC_VERSION__ >= 199901L /* C99 allows usage of inline keyword */
    #define _vn_inline inline
#elif defined(__GNUC__) || defined(__GNUG__)
    #define _vn_inline __inline__
#else
    #define _vn_inline
    #pragma message("Warning: No inline keyword defined for this compiler")
#endif

/*
* Set api export calling convention (allow user to override)
*/
#ifndef VNLIB_CC
    #ifdef _VN_IS_WINDOWS
        /* STD for importing to other languages such as .NET */
        #define VNLIB_CC __stdcall
    #else
        #define VNLIB_CC
    #endif
#endif /* !VNLIB_CC */

/*
* Set api export/import macros (allow user to override)
*/
#ifndef VNLIB_EXPORT
    #ifdef VNLIB_EXPORTING
        #ifdef _VN_IS_WINDOWS
            #define VNLIB_EXPORT __declspec(dllexport)
        #else
            #define VNLIB_EXPORT __attribute__((visibility("default")))
        #endif /* _VN_IS_WINDOWS */
    #else
        #ifdef _VN_IS_WINDOWS
            #define VNLIB_EXPORT __declspec(dllimport)
        #else
            #define VNLIB_EXPORT
        #endif /* _VN_IS_WINDOWS */
    #endif /* !VNLIB_EXPORTING */
#endif /* !VNLIB_EXPORT */

/*
* Internal symbol export macro. In debug/test builds, internal symbols
* are exported from the shared library so tests can link against the
* DLL. In release builds, internal symbols are hidden.
*/
#ifdef DEBUG
    #define vnlib_fn_internal VNLIB_EXPORT
#else
    #define vnlib_fn_internal
#endif

#ifndef _Out_
    #define _Out_
#endif // !_Out_

#ifndef _In_
    #define _In_
#endif // !_In_

#endif /* !VN_WTLFU_PLATFORM_H */
