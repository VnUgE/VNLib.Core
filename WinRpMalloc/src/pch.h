/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: WinRpMalloc
* File: pch.h 
*
* pch.h is part of WinRpMalloc which is part of the larger 
* VNLib collection of libraries and utilities.
*
* WinRpMalloc is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* WinRpMalloc is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with WinRpMalloc. If not, see http://www.gnu.org/licenses/.
*/

// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

#include "framework.h"
// add headers that you want to pre-compile here

//Using firstclass heaps, define 
#define RPMALLOC_FIRST_CLASS_HEAPS 1

/*
* Enabling adaptive thread cache because I am not using thread initilaizations
*/
#define ENABLE_ADAPTIVE_THREAD_CACHE 1

#ifdef DEBUG

ENABLE_VALIDATE_ARGS 1
ENABLE_ASSERTS 1
ENABLE_STATISTICS 1

#endif // DEBUG

#include "rpmalloc.h"

#endif //PCH_H
