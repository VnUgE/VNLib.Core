/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: Argon2Context.cs 
*
* Argon2Context.cs is part of VNLib.Hashing.Portable which is part of the larger 
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

using System;
using System.Runtime.InteropServices;

namespace VNLib.Hashing
{

    public static unsafe partial class VnArgon2
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private ref struct Argon2Context
        {
            public void* outptr;         /* output array */
            public UInt32 outlen;         /* digest length */

            public void* pwd;            /* password array */
            public UInt32 pwdlen;         /* password length */

            public void* salt;           /* salt array */
            public UInt32 saltlen;        /* salt length */

            public void* secret;         /* key array */
            public UInt32 secretlen;      /* key length */

            public void* ad;             /* associated data array */
            public UInt32 adlen;          /* associated data length */

            public UInt32 t_cost;         /* number of passes */
            public UInt32 m_cost;         /* amount of memory requested (KB) */
            public UInt32 lanes;          /* number of lanes */
            public UInt32 threads;        /* maximum number of threads */

            public Argon2Version version;      /* version number */

            public void* allocate_cbk;   /* pointer to memory allocator */
            public void* free_cbk;       /* pointer to memory deallocator */

            public UInt32 flags;          /* array of bool options */
        }
    }
}