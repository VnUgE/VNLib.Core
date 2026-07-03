/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: test.h
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
*  TEST HELPER HEADER
*
* Contains macros and functions to assist with testing across multiple
* test projects.
*/

#pragma once

#ifndef _WTL_TEST_H
#define _WTL_TEST_H

#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <stdlib.h>

#include <wtlfu.h>

#ifdef _VN_IS_WINDOWS
    #define IS_WINDOWS
#endif

#ifdef IS_WINDOWS
    #define WIN32_LEAN_AND_MEAN
    #include <windows.h>
#endif

#ifdef IS_WINDOWS
    #define TASSERT(x) if(!(x)) { printf("ERROR! Internal test assumption failed: %s. @ Line: %d\n Aborting tests...\n", #x, __LINE__); ExitProcess(1); }
#else
    #define TASSERT(x) if(!(x)) { printf("ERROR! Internal test assumption failed: %s. @ Line: %d\n Aborting tests...\n", #x, __LINE__); exit(1); }
#endif

#define PRINTL(x) puts(x); puts("\n");

#define ENSURE(x) if(!(x)) { printf("Test assumption failed on line %d\n", __LINE__); return 1; }

#define EXPECT_THAT(message, bool_expr) printf("\tTesting %s [%s:%d]\n", #bool_expr, __FILE__, __LINE__); \
if(!(bool_expr))\
{ printf("FAILED: %s @ callsite %s. Line: %d \n", message, #bool_expr, __LINE__); return 1; }

#define EXPECT_EQ(x, expected) printf("\tTesting %s == %s [%s:%d]\n", #x, #expected, __FILE__, __LINE__); if(((long)x) != ((long)expected)) \
{ printf("FAILED: Expected %ld but got %ld @ callsite %s. Line: %d \n", ((long)expected), ((long)x), #x, __LINE__); return 1; }

#define EXPECT_NE(x, expected) printf("\tTesting %s != %s [%s:%d]\n", #x, #expected, __FILE__, __LINE__); if(((long)x) == ((long)expected)) \
{ printf("FAILED: Expected distinct values but both were %ld @ callsite %s. Line: %d \n", ((long)expected), #x, __LINE__); return 1; }

#define EXPECT_TRUE(x) EXPECT_THAT("Expected true", (x))
#define EXPECT_FALSE(x) EXPECT_THAT("Expected false", !(x))

#define TEST EXPECT_EQ

#ifdef IS_WINDOWS
    #define ZERO_FILL(x, size) SecureZeroMemory(x, size)
#else
    #define ZERO_FILL(x, size) memset(x, 0, size)
#endif

#define strlen32(x) (uint32_t)strlen(x)

#define RUN_TEST(result) PRINTL("RUNNING TEST: " #result)  \
    if (result != 0) { return 1; }                         \
    else { PRINTL("\nPASSED: " #result) }                  \

#define TEST_GROUP(result) PRINTL("BEGINNING GROUP: " #result)   \
    if (result != 0) { return 1; }                              \
    else { PRINTL("GROUP: "#result" COMPLETE")  }               \

void FillRandomData(void* pbBuffer, size_t length);

#endif /* !_WTL_TEST_H */
