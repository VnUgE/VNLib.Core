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


#ifndef VN_MONOCYPHER_ARGON2_H
#define VN_MONOCYPHER_ARGON2_H

#include <stdint.h>
#include "util.h"

/*
    The following types are 1:1 with the Argon2 reference library,
    this allows for a common api interface between the two libraries for 
    dynamic linking
*/

typedef enum Argon2Type
{
	Argon2_d = 0,
	Argon2_i = 1,
	Argon2_id = 2

} Argon2Type;

/* Error codes */
typedef enum Argon2_ErrorCodes {
    ARGON2_OK = 0,

    ARGON2_OUTPUT_PTR_NULL = -1,

    ARGON2_OUTPUT_TOO_SHORT = -2,
    ARGON2_OUTPUT_TOO_LONG = -3,

    ARGON2_PWD_TOO_SHORT = -4,
    ARGON2_PWD_TOO_LONG = -5,

    ARGON2_SALT_TOO_SHORT = -6,
    ARGON2_SALT_TOO_LONG = -7,

    ARGON2_AD_TOO_SHORT = -8,
    ARGON2_AD_TOO_LONG = -9,

    ARGON2_SECRET_TOO_SHORT = -10,
    ARGON2_SECRET_TOO_LONG = -11,

    ARGON2_TIME_TOO_SMALL = -12,
    ARGON2_TIME_TOO_LARGE = -13,

    ARGON2_MEMORY_TOO_LITTLE = -14,
    ARGON2_MEMORY_TOO_MUCH = -15,

    ARGON2_LANES_TOO_FEW = -16,
    ARGON2_LANES_TOO_MANY = -17,

    ARGON2_PWD_PTR_MISMATCH = -18,    /* NULL ptr with non-zero length */
    ARGON2_SALT_PTR_MISMATCH = -19,   /* NULL ptr with non-zero length */
    ARGON2_SECRET_PTR_MISMATCH = -20, /* NULL ptr with non-zero length */
    ARGON2_AD_PTR_MISMATCH = -21,     /* NULL ptr with non-zero length */

    ARGON2_MEMORY_ALLOCATION_ERROR = -22,

    ARGON2_FREE_MEMORY_CBK_NULL = -23,
    ARGON2_ALLOCATE_MEMORY_CBK_NULL = -24,

    ARGON2_INCORRECT_PARAMETER = -25,
    ARGON2_INCORRECT_TYPE = -26,

    ARGON2_OUT_PTR_MISMATCH = -27,

    ARGON2_THREADS_TOO_FEW = -28,
    ARGON2_THREADS_TOO_MANY = -29,

    ARGON2_MISSING_ARGS = -30,

    ARGON2_ENCODING_FAIL = -31,

    ARGON2_DECODING_FAIL = -32,

    ARGON2_THREAD_FAIL = -33,

    ARGON2_DECODING_LENGTH_FAIL = -34,

    ARGON2_VERIFY_MISMATCH = -35
} argon2_error_codes;

typedef struct Argon2_Context {
    
    uint8_t* out;    /* output array */
    const uint32_t outlen; /* digest length */

    const uint8_t* pwd;    /* password array */
    const uint32_t pwdlen; /* password length */

    const uint8_t* salt;    /* salt array */
    const uint32_t saltlen; /* salt length */

    const uint8_t* secret;    /* key array */
    const uint32_t secretlen; /* key length */

    const uint8_t* ad;    /* associated data array */
    const uint32_t adlen; /* associated data length */

    const uint32_t t_cost;  /* number of passes */
    const uint32_t m_cost;  /* amount of memory requested (KB) */
    const uint32_t lanes;   /* number of lanes */
    const uint32_t threads; /* maximum number of threads */

    const Argon2Type version; /* version number */

    const void* allocate_cbk; /* pointer to memory allocator */
    const void* free_cbk;   /* pointer to memory deallocator */

    const uint32_t flags; /* array of bool options */
} argon2Ctx;


VNLIB_EXPORT uint32_t VNLIB_CC Argon2CalcWorkAreaSize(const argon2Ctx* context);

VNLIB_EXPORT argon2_error_codes VNLIB_CC Argon2ComputeHash(const argon2Ctx* context, void* workArea);

#endif /* VN_MONOCYPHER_ARGON2_H */