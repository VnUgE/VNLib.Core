/*
* Copyright (c) 2025 Vaughn Nugent
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

namespace VNLib.Hashing
{
    /// <summary>
    /// Represents native library error codes returned by Argon2 hashing operations
    /// </summary>
    public enum Argon2_ErrorCodes
    {
        /// <summary>
        /// Operation completed successfully
        /// </summary>
        ARGON2_OK = 0,
        
        /// <summary>
        /// Output pointer is null
        /// </summary>
        ARGON2_OUTPUT_PTR_NULL = -1,
        
        /// <summary>
        /// Output buffer is too short
        /// </summary>
        ARGON2_OUTPUT_TOO_SHORT = -2,
        
        /// <summary>
        /// Output buffer is too long
        /// </summary>
        ARGON2_OUTPUT_TOO_LONG = -3,
        
        /// <summary>
        /// Password is too short
        /// </summary>
        ARGON2_PWD_TOO_SHORT = -4,
        
        /// <summary>
        /// Password is too long
        /// </summary>
        ARGON2_PWD_TOO_LONG = -5,
        
        /// <summary>
        /// Salt is too short
        /// </summary>
        ARGON2_SALT_TOO_SHORT = -6,
        
        /// <summary>
        /// Salt is too long
        /// </summary>
        ARGON2_SALT_TOO_LONG = -7,
        
        /// <summary>
        /// Associated data is too short
        /// </summary>
        ARGON2_AD_TOO_SHORT = -8,
        
        /// <summary>
        /// Associated data is too long
        /// </summary>
        ARGON2_AD_TOO_LONG = -9,
        
        /// <summary>
        /// Secret is too short
        /// </summary>
        ARGON2_SECRET_TOO_SHORT = -10,
        
        /// <summary>
        /// Secret is too long
        /// </summary>
        ARGON2_SECRET_TOO_LONG = -11,
        
        /// <summary>
        /// Time cost parameter is too small
        /// </summary>
        ARGON2_TIME_TOO_SMALL = -12,
        
        /// <summary>
        /// Time cost parameter is too large
        /// </summary>
        ARGON2_TIME_TOO_LARGE = -13,
        
        /// <summary>
        /// Memory cost parameter is too small
        /// </summary>
        ARGON2_MEMORY_TOO_LITTLE = -14,
        
        /// <summary>
        /// Memory cost parameter is too large
        /// </summary>
        ARGON2_MEMORY_TOO_MUCH = -15,
        
        /// <summary>
        /// Too few parallel lanes specified
        /// </summary>
        ARGON2_LANES_TOO_FEW = -16,
        
        /// <summary>
        /// Too many parallel lanes specified
        /// </summary>
        ARGON2_LANES_TOO_MANY = -17,
        
        /// <summary>
        /// Password pointer is null but length is non-zero
        /// </summary>
        ARGON2_PWD_PTR_MISMATCH = -18,
        
        /// <summary>
        /// Salt pointer is null but length is non-zero
        /// </summary>
        ARGON2_SALT_PTR_MISMATCH = -19,
        
        /// <summary>
        /// Secret pointer is null but length is non-zero
        /// </summary>
        ARGON2_SECRET_PTR_MISMATCH = -20,
        
        /// <summary>
        /// Associated data pointer is null but length is non-zero
        /// </summary>
        ARGON2_AD_PTR_MISMATCH = -21,
        
        /// <summary>
        /// Memory allocation failed
        /// </summary>
        ARGON2_MEMORY_ALLOCATION_ERROR = -22,
        
        /// <summary>
        /// Free memory callback is null
        /// </summary>
        ARGON2_FREE_MEMORY_CBK_NULL = -23,
        
        /// <summary>
        /// Allocate memory callback is null
        /// </summary>
        ARGON2_ALLOCATE_MEMORY_CBK_NULL = -24,
        
        /// <summary>
        /// Incorrect parameter value
        /// </summary>
        ARGON2_INCORRECT_PARAMETER = -25,
        
        /// <summary>
        /// Incorrect Argon2 type
        /// </summary>
        ARGON2_INCORRECT_TYPE = -26,
        
        /// <summary>
        /// Output pointer mismatch
        /// </summary>
        ARGON2_OUT_PTR_MISMATCH = -27,
        
        /// <summary>
        /// Too few threads specified
        /// </summary>
        ARGON2_THREADS_TOO_FEW = -28,
        
        /// <summary>
        /// Too many threads specified
        /// </summary>
        ARGON2_THREADS_TOO_MANY = -29,
        
        /// <summary>
        /// Required arguments are missing
        /// </summary>
        ARGON2_MISSING_ARGS = -30,
        
        /// <summary>
        /// Encoding operation failed
        /// </summary>
        ARGON2_ENCODING_FAIL = -31,
        
        /// <summary>
        /// Decoding operation failed
        /// </summary>
        ARGON2_DECODING_FAIL = -32,
        
        /// <summary>
        /// Thread operation failed
        /// </summary>
        ARGON2_THREAD_FAIL = -33,
        
        /// <summary>
        /// Decoding length validation failed
        /// </summary>
        ARGON2_DECODING_LENGTH_FAIL = -34,
        
        /// <summary>
        /// Hash verification failed - passwords do not match
        /// </summary>
        ARGON2_VERIFY_MISMATCH = -35
    }
}