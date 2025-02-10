/*
* Copyright (c) 2025 Vaughn Nugent
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
#ifndef VN_MONOCYPHER_UTIL_H
#define VN_MONOCYPHER_UTIL_H

#include "platform.h"

//Set api export calling convention (allow used to override)
#ifndef VNLIB_CC
    #ifdef _VN_IS_WINDOWS
        //STD for importing to other languages such as .NET
        #define VNLIB_CC __stdcall
    #else
        #define VNLIB_CC 
    #endif
#endif // !NC_CC

#ifndef VNLIB_EXPORT	//Allow users to disable the export/impoty macro if using source code directly
    #ifdef VNLIB_EXPORTING
        #ifdef _VN_IS_WINDOWS
            #define VNLIB_EXPORT __declspec(dllexport)
        #else
            #define VNLIB_EXPORT __attribute__((visibility("default")))
        #endif // _NC_IS_WINDOWS
    #else
        #ifdef _VN_IS_WINDOWS
            #define VNLIB_EXPORT __declspec(dllimport)
        #else
            #define VNLIB_EXPORT
        #endif // _VN_IS_WINDOWS
    #endif // !VNLIB_EXPORTING
#endif // !VNLIB_EXPORT


#define ERR_INVALID_PTR -1
#define ERR_OUT_OF_MEMORY -2

#define VALIDATE_PTR(ptr) if (!ptr) return ERR_INVALID_PTR

#endif /* !VN_MONOCYPHER_UTIL_H */