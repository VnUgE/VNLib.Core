/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: Argon2PasswordEntry.cs 
*
* Argon2PasswordEntry.cs is part of VNLib.Hashing.Portable which is part of the larger 
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
using System.Globalization;

using VNLib.Utils.Extensions;

namespace VNLib.Hashing
{

    internal readonly ref struct Argon2PasswordEntry
    {
        private readonly ReadOnlySpan<char> _window;

        public readonly Argon2Version Version;
        public readonly ReadOnlySpan<char> Salt;
        public readonly ReadOnlySpan<char> Hash;

        private static Argon2Version ParseVersion(ReadOnlySpan<char> window)
        {
            //Version comes after the v= prefix
            ReadOnlySpan<char> v = window.SliceAfterParam("v=");
            v = v.SliceBeforeParam(',');
            //Parse the version as an enum value
            return Enum.Parse<Argon2Version>(v);
        }

        private static uint ParseTimeCost(ReadOnlySpan<char> window)
        {
            //TimeCost comes after the t= prefix
            ReadOnlySpan<char> t = window.SliceAfterParam("t=");
            t = t.SliceBeforeParam(',');
            //Parse the time cost as an unsigned integer
            return uint.Parse(t, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static uint ParseMemoryCost(ReadOnlySpan<char> window)
        {
            //MemoryCost comes after the m= prefix
            ReadOnlySpan<char> m = window.SliceAfterParam("m=");
            m = m.SliceBeforeParam(',');
            //Parse the memory cost as an unsigned integer
            return uint.Parse(m, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static uint ParseParallelism(ReadOnlySpan<char> window)
        {
            //Parallelism comes after the p= prefix
            ReadOnlySpan<char> p = window.SliceAfterParam("p=");
            p = p.SliceBeforeParam(',');
            //Parse the parallelism as an unsigned integer
            return uint.Parse(p, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static ReadOnlySpan<char> ParseSalt(ReadOnlySpan<char> window)
        {
            //Salt comes after the s= prefix
            ReadOnlySpan<char> s = window.SliceAfterParam("s=");
            s = s.SliceBeforeParam('$');
            //Parse the salt as a string
            return s;
        }

        private static ReadOnlySpan<char> ParseHash(ReadOnlySpan<char> window)
        {
            //Get last index of dollar sign for the start of the password hash
            int start = window.LastIndexOf('$');
            return window[(start + 1)..];
        }

        public Argon2PasswordEntry(ReadOnlySpan<char> str)
        {
            _window = str;
            Version = ParseVersion(str);
            Salt = ParseSalt(str);
            Hash = ParseHash(str);
        }

        public readonly Argon2CostParams GetCostParams()
        {
            return new()
            {
                MemoryCost = ParseMemoryCost(_window),
                TimeCost = ParseTimeCost(_window),
                Parallelism = ParseParallelism(_window)
            };
        }
    }
}