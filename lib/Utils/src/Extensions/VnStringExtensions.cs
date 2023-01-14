/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: VnStringExtensions.cs 
*
* VnStringExtensions.cs is part of VNLib.Utils which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Utils is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Utils is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Utils. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Linq;
using System.Collections.Generic;

using VNLib.Utils.Memory;
using System.Runtime.CompilerServices;

#pragma warning disable CA1062 // Validate arguments of public methods

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// A collection of extensions for <see cref="VnString"/>
    /// </summary>
    public static class VnStringExtensions
    {
        /// <summary>
        /// Derermines if the character exists within the instance
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value">The value to find</param>
        /// <returns>True if the character exists within the instance</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this VnString str, char value) => str.AsSpan().Contains(value);

        /// <summary>
        /// Derermines if the sequence exists within the instance
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value">The sequence to find</param>
        /// <param name="stringComparison"></param>
        /// <returns>True if the character exists within the instance</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this VnString str, ReadOnlySpan<char> value, StringComparison stringComparison) => str.AsSpan().Contains(value, stringComparison);


        /// <summary>
        ///  Searches for the first occurrance of the specified character within the current instance
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value">The character to search for within the instance</param>
        /// <returns>The 0 based index of the occurance, -1 if the character was not found</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(this VnString str, char value) => str.IsEmpty ? -1 : str.AsSpan().IndexOf(value);

        /// <summary>
        /// Searches for the first occurrance of the specified sequence within the current instance
        /// </summary>
        /// <param name="str"></param>
        /// <param name="search">The sequence to search for</param>
        /// <returns>The 0 based index of the occurance, -1 if the sequence was not found</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(this VnString str, ReadOnlySpan<char> search) => str.AsSpan().IndexOf(search);

        /// <summary>
        /// Searches for the first occurrance of the specified sequence within the current instance
        /// </summary>
        /// <param name="str"></param>
        /// <param name="search">The sequence to search for</param>
        /// <param name="comparison">The <see cref="StringComparison"/> type to use in searchr</param>
        /// <returns>The 0 based index of the occurance, -1 if the sequence was not found</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(this VnString str, ReadOnlySpan<char> search, StringComparison comparison) => str.AsSpan().IndexOf(search, comparison);
        
        /// <summary>
        /// Searches for the 0 based index of the first occurance of the search parameter after the start index.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="search">The sequence of data to search for</param>
        /// <param name="start">The lower boundry of the search area</param>
        /// <returns>The absolute index of the first occurrance within the instance, -1 if the sequency was not found in the windowed segment</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public static int IndexOf(this VnString str, ReadOnlySpan<char> search, int start)
        {
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start), "Start cannot be less than 0");
            }
            //Get shifted window
            ReadOnlySpan<char> self = str.AsSpan()[start..];
            //Check indexof
            int index = self.IndexOf(search);
            return index > -1 ? index + start : -1;
        }

        /// <summary>
        /// Returns the realtive index after the specified sequence within the <see cref="VnString"/> instance
        /// </summary>
        /// <param name="str"></param>
        /// <param name="search">The sequence to search for</param>
        /// <returns>The index after the found sequence within the string, -1 if the sequence was not found within the instance</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static int EndOf(this VnString str, ReadOnlySpan<char> search)
        {
            //Try to get the index of the data
            int index = IndexOf(str, search);
            //If the data was found, add the length to get the end of the string
            return index > -1 ? index + search.Length : -1;
        }

        /// <summary>
        /// Allows for trimming whitespace characters in a realtive sequence from 
        /// within a <see cref="VnString"/> buffer defined by the start and end parameters
        /// and returning the trimmed entry.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="start">The starting position within the sequence to trim</param>
        /// <param name="end">The end of the sequence to trim</param>
        /// <returns>The trimmed <see cref="VnString"/> instance as a child of the original entry</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VnString AbsoluteTrim(this VnString data, int start, int end)
        {
            AbsoluteTrim(data, ref start, ref end);
            return data[start..end];
        }
        
        /// <summary>
        /// Finds whitespace characters within the sequence defined between start and end parameters 
        /// and adjusts the specified window to "trim" whitespace
        /// </summary>
        /// <param name="data"></param>
        /// <param name="start">The starting position within the sequence to trim</param>
        /// <param name="end">The end of the sequence to trim</param>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public static void AbsoluteTrim(this VnString data, ref int start, ref int end)
        {
            ReadOnlySpan<char> trimmed = data.AsSpan();
            //trim leading whitespace
            while (start < end)
            {
                //If whitespace character shift start up
                if (trimmed[start] != ' ')
                {
                    break;
                }
                //Shift
                start++;
            }
            //remove trailing whitespace characters
            while (end > start)
            {
                //If whiterspace character shift end param down
                if (trimmed[end - 1] != ' ')
                {
                    break;
                }
                end--;
            }
        }

        /// <summary>
        /// Allows for trimming whitespace characters in a realtive sequence from 
        /// within a <see cref="VnString"/> buffer and returning the trimmed entry.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="start">The starting position within the sequence to trim</param>
        /// <returns>The trimmed <see cref="VnString"/> instance as a child of the original entry</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VnString AbsoluteTrim(this VnString data, int start) => AbsoluteTrim(data, start, data.Length);

        /// <summary>
        /// Trims leading or trailing whitespace characters and returns a new child instance 
        /// without leading or trailing whitespace
        /// </summary>
        /// <returns>A child <see cref="VnString"/> of the current instance without leading or trailing whitespaced</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VnString RelativeTirm(this VnString data) => AbsoluteTrim(data, 0);

        /// <summary>
        /// Allows for enumeration of segments of data within the specified <see cref="VnString"/> instance that are 
        /// split by the search parameter
        /// </summary>
        /// <param name="data"></param>
        /// <param name="search">The sequence of data to delimit segments</param>
        /// <param name="options">The options used to split the string instances</param>
        /// <returns>An iterator to enumerate the split segments</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static IEnumerable<VnString> Split(this VnString data, ReadOnlyMemory<char> search, StringSplitOptions options = StringSplitOptions.None)
        {
            int lowerBound = 0;
            //Make sure the length of the search param is not 0
            if(search.IsEmpty)
            {
                //Return the entire string
                yield return data;
            }
            //No string options
            else if (options == 0)
            {
                do
                {
                    //Capture the first = and store argument + value
                    int splitIndex = data.IndexOf(search.Span, lowerBound);
                    //If no split index is found, then return remaining data
                    if (splitIndex == -1)
                    {
                        break;
                    }
                    yield return data[lowerBound..splitIndex];
                    //Shift the lower window to the end of the last string
                    lowerBound = splitIndex + search.Length;
                } while (true);
                //Return remaining data
                yield return data[lowerBound..];
            }
            //Trim but do not remove empties
            else if ((options & StringSplitOptions.RemoveEmptyEntries) == 0)
            {
                do
                {
                    //Capture the first = and store argument + value
                    int splitIndex = data.IndexOf(search.Span, lowerBound);
                    //If no split index is found, then return remaining data
                    if (splitIndex == -1)
                    {
                        break;
                    }
                    //trim and return
                    yield return data.AbsoluteTrim(lowerBound, splitIndex);
                    //Shift the lower window to the end of the last string
                    lowerBound = splitIndex + search.Length;
                } while (true);
                //Return remaining data
                yield return data.AbsoluteTrim(lowerBound);
            }
            //Remove empty entires but do not trim them
            else if ((options & StringSplitOptions.TrimEntries) == 0)
            {
                do
                {
                    //Capture the first = and store argument + value
                    int splitIndex = data.IndexOf(search.Span, lowerBound);
                    //If no split index is found, then return remaining data
                    if (splitIndex == -1)
                    {
                        break;
                    }
                    //If the split index is the next sequence, then the result is empty, so exclude it
                    else if(splitIndex > 0)
                    {
                        yield return data[lowerBound..splitIndex];
                    }
                    //Shift the lower window to the end of the last string
                    lowerBound = splitIndex + search.Length;
                } while (true);
                //Return remaining data if available
                if (lowerBound < data.Length)
                {
                    yield return data[lowerBound..];
                }
            }
            //Must mean remove and trim
            else
            {
                //Get stack varables to pass to trim function
                int trimStart, trimEnd;
                do
                {
                    //Capture the first = and store argument + value
                    int splitIndex = data.IndexOf(search.Span, lowerBound);
                    //If no split index is found, then return remaining data
                    if (splitIndex == -1)
                    {
                        break;
                    }
                    //Get stack varables to pass to trim function
                    trimStart = lowerBound;
                    trimEnd = splitIndex; //End of the segment is the relative split index + the lower bound of the window
                    //Trim whitespace chars
                    data.AbsoluteTrim(ref trimStart, ref trimEnd);
                    //See if the string has data 
                    if((trimEnd - trimStart) > 0)
                    {
                        yield return data[trimStart..trimEnd];
                    }
                    //Shift the lower window to the end of the last string
                    lowerBound = splitIndex + search.Length;
                } while (true);
                //Trim remaining 
                trimStart = lowerBound;
                trimEnd = data.Length;
                data.AbsoluteTrim(ref trimStart, ref trimEnd);
                //If the remaining string is not empty return it
                if ((trimEnd - trimStart) > 0)
                {
                    yield return data[trimStart..trimEnd];
                }
            }
        }

        /// <summary>
        /// Trims any leading or trailing <c>'\r'|'\n'|' '</c>(whitespace) characters from the segment
        /// </summary>
        /// <returns>The trimmed segment</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public static VnString TrimCRLF(this VnString data)
        {
            ReadOnlySpan<char> trimmed = data.AsSpan();
            int start = 0, end = trimmed.Length;
            //trim leading \r\n chars
            while (start < end)
            {
                char t = trimmed[start];
                //If character \r or \n slice it off
                if (t != '\r' && t != '\n' && t != ' ') {
                    break;
                }
                //Shift
                start++;
            }
            //remove trailing crlf characters
            while (end > start)
            {
                char t = trimmed[end - 1];
                //If character \r or \n slice it off
                if (t != '\r' && t != '\n' && t != ' ') {
                    break;
                }
                end--;
            }
            return data[start..end];
        }

        /// <summary>
        /// Converts the current handle to a <see cref="VnString"/>, a zero-alloc immutable wrapper 
        /// for a memory handle
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="length">The number of characters from the handle to reference (length of the string)</param>
        /// <returns>The new <see cref="VnString"/> wrapper</returns>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VnString ToVnString(this MemoryHandle<char> handle, int length) => VnString.ConsumeHandle(handle, 0, length);

        /// <summary>
        /// Converts the current handle to a <see cref="VnString"/>, a zero-alloc immutable wrapper 
        /// for a memory handle
        /// </summary>
        /// <param name="handle"></param>
        /// <returns>The new <see cref="VnString"/> wrapper</returns>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VnString ToVnString(this MemoryHandle<char> handle) => VnString.ConsumeHandle(handle, 0, handle.GetIntLength());

        /// <summary>
        /// Converts the current handle to a <see cref="VnString"/>, a zero-alloc immutable wrapper 
        /// for a memory handle
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="offset">The offset in characters that represents the begining of the string</param>
        /// <param name="length">The number of characters from the handle to reference (length of the string)</param>
        /// <returns>The new <see cref="VnString"/> wrapper</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VnString ToVnString(this MemoryHandle<char> handle, nuint offset, int length) => VnString.ConsumeHandle(handle, offset, length);
    }
}