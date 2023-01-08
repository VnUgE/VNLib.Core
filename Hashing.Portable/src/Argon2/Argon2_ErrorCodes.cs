/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: Argon2_ErrorCodes.cs 
*
* Argon2_ErrorCodes.cs is part of VNLib.Hashing.Portable which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Hashing.Portable is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Hashing.Portable is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Hashing.Portable. If not, see http://www.gnu.org/licenses/.
*/

/*
 *  VnArgon2.cs
 *  Author: Vaughhn Nugent
 *  Date: July 17, 2021
 *  
 *  Dependencies Argon2.
 *  https://github.com/P-H-C/phc-winner-argon2
 *  
 */

namespace VNLib.Hashing
{
    public enum Argon2_ErrorCodes
    {
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
    }
}