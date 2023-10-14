
#pragma once
#ifndef VNLIB_RPMALLOC_H

#if defined(_WIN32) || defined(_WIN64)

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files
#include <windows.h>

#else

#define _GNU_SOURCE // for RTLD_NEXT

#include <stddef.h>

#define TRUE 1
#define FALSE 0

//Windows type aliases for non-win
typedef int BOOL;

#endif

#endif // !VNLIB_RPMALLOC_H