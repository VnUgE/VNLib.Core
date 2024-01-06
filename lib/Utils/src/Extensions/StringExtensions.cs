/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: StringExtensions.cs 
*
* StringExtensions.cs is part of VNLib.Utils which is part of the larger 
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
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using VNLib.Utils.Memory;

namespace VNLib.Utils.Extensions
{
    /// <summary>
    /// Delegate for a stateless span action
    /// </summary>
    /// <param name="line">The line of data to process</param>
    public delegate void StatelessSpanAction(ReadOnlySpan<char> line);

    /// <summary>
    /// Extention methods for string (character buffer)
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Split a string based on split value and insert into the specified list
        /// </summary>
        /// <param name="value"></param>
        /// <param name="splitter">The value to split the string on</param>
        /// <param name="output">The list to output data to</param>
        /// <param name="options">String split options</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Split<T>(this string value, string splitter, T output, StringSplitOptions options) where T : ICollection<string>
        {
            Split(value, splitter.AsSpan(), output, options);
        }

        /// <summary>
        /// Split a string based on split value and insert into the specified list
        /// </summary>
        /// <param name="value"></param>
        /// <param name="splitter">The value to split the string on</param>
        /// <param name="output">The list to output data to</param>
        /// <param name="options">String split options</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Split<T>(this string value, char splitter, T output, StringSplitOptions options) where T: ICollection<string>
        {
            //Create span from char pointer
            ReadOnlySpan<char> cs = MemoryMarshal.CreateReadOnlySpan(ref splitter, 1);
            //Call the split function on the span
            Split(value, cs, output, options);
        }

        /// <summary>
        /// Split a string based on split value and insert into the specified list
        /// </summary>
        /// <param name="value"></param>
        /// <param name="splitter">The value to split the string on</param>
        /// <param name="output">The list to output data to</param>
        /// <param name="options">String split options</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Split<T>(this string value, ReadOnlySpan<char> splitter, T output, StringSplitOptions options) where T : ICollection<string>
        {
            Split(value.AsSpan(), splitter, output, options);
        }

        /// <summary>
        /// Split a string based on split value and insert into the specified list
        /// </summary>
        /// <param name="value"></param>
        /// <param name="splitter">The value to split the string on</param>
        /// <param name="output">The list to output data to</param>
        /// <param name="options">String split options</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Split<T>(this ReadOnlySpan<char> value, char splitter, T output, StringSplitOptions options) where T : ICollection<string>
        {
            //Create span from char pointer
            ReadOnlySpan<char> cs = MemoryMarshal.CreateReadOnlySpan(ref splitter, 1);
            //Call the split function on the span
            Split(value, cs, output, options);
        }

        /// <summary>
        /// Split a <see cref="ReadOnlySpan{T}"/> based on split value and insert into the specified list
        /// </summary>
        /// <param name="value"></param>
        /// <param name="splitter">The value to split the string on</param>
        /// <param name="output">The list to output data to</param>
        /// <param name="options">String split options</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Split<T>(this ReadOnlySpan<char> value, ReadOnlySpan<char> splitter, T output, StringSplitOptions options) where T : ICollection<string>
        {
            //Create a local function that adds the split strings to the list
            static void SplitFound(ReadOnlySpan<char> split, T output) => output.Add(split.ToString());

            //Invoke the split function with the local callback method
            Split(value, splitter, options, SplitFound, output);
        }

        /// <summary>
        /// Split a <see cref="ReadOnlySpan{T}"/> based on split value and pass it to the split delegate handler
        /// </summary>
        /// <param name="value"></param>
        /// <param name="splitter">The sequence to split the string on</param>
        /// <param name="options">String split options</param>
        /// <param name="splitCb">The action to invoke when a split segment has been found</param>
        /// <param name="state">The state to pass to the callback handler</param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void Split<T>(this ReadOnlySpan<char> value, ReadOnlySpan<char> splitter, StringSplitOptions options, ReadOnlySpanAction<char, T> splitCb, T state)
        {
            _ = splitCb ?? throw new ArgumentNullException(nameof(splitCb));

            //Get span over string
            ForwardOnlyReader<char> reader = new(value);

            //No string options
            if (options == 0)
            {
                do
                {
                    //Find index of the splitter
                    int start = reader.Window.IndexOf(splitter);

                    //guard
                    if (start == -1)
                    {
                        break;
                    }

                    //Trim and add it regardless of length
                    splitCb(reader.Window[..start], state);

                    //shift window
                    reader.Advance(start + splitter.Length);
                } while (true);

                //Trim remaining and add it regardless of length
                splitCb(reader.Window, state);
            }
            //Trim but do not remove empties
            else if ((options & StringSplitOptions.TrimEntries) == StringSplitOptions.TrimEntries)
            {
                do
                {
                    //Find index of the splitter
                    int start = reader.Window.IndexOf(splitter);

                    //guard
                    if (start == -1) 
                    {
                        break;
                    }

                    //Trim and add it regardless of length
                    splitCb(reader.Window[..start].Trim(), state);

                    //shift window
                    reader.Advance(start + splitter.Length);
                } while (true);

                //Trim remaining and add it regardless of length
                splitCb(reader.Window.Trim(), state);
            }
            //Remove empty entires but do not trim them
            else if ((options & StringSplitOptions.RemoveEmptyEntries) == StringSplitOptions.RemoveEmptyEntries)
            {
                //Get data before splitter and trim it
                ReadOnlySpan<char> data;
                do
                {
                    //Find index of the splitter
                    int start = reader.Window.IndexOf(splitter);
                    //guard
                    if (start == -1)
                    {
                        break;
                    }
                    //Get data before splitter and trim it
                    data = reader.Window[..start];
                    //If its not empty, then add it to the list
                    if (!data.IsEmpty)
                    {
                        splitCb(data, state);
                    }
                    
                    reader.Advance(start + splitter.Length);

                } while (true);

                //Add if not empty
                if (reader.WindowSize > 0)
                {
                    splitCb(reader.Window, state);
                }
            }
            //Must mean remove and trim
            else
            {
                //Get data before splitter and trim it
                ReadOnlySpan<char> data;
                do
                {
                    //Find index of the splitter
                    int start = reader.Window.IndexOf(splitter);

                    //guard
                    if (start == -1)
                    {
                        break;
                    }

                    //Get data before splitter and trim it
                    data = reader.Window[..start].Trim();

                    //If its not empty, then add it to the list
                    if (!data.IsEmpty)
                    {
                        splitCb(data, state);
                    }
                   
                    reader.Advance(start + splitter.Length);

                } while (true);

                //Trim remaining
                data = reader.Window.Trim();

                //Add if not empty
                if (!data.IsEmpty)
                {
                    splitCb(data, state);
                }
            }
        }
        
        /// <summary>
        /// Split a <see cref="ReadOnlySpan{T}"/> based on split value and pass it to the split delegate handler
        /// </summary>
        /// <param name="value"></param>
        /// <param name="splitter">The character to split the string on</param>
        /// <param name="options">String split options</param>
        /// <param name="splitCb">The action to invoke when a split segment has been found</param>
        /// <param name="state"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Split<T>(this ReadOnlySpan<char> value, char splitter, StringSplitOptions options, ReadOnlySpanAction<char, T> splitCb, T state)
        {
            //Alloc a span for char
            ReadOnlySpan<char> cs = MemoryMarshal.CreateReadOnlySpan(ref splitter, 1);
            //Call the split function on the span
            Split(value, cs, options, splitCb, state);
        }

        /// <summary>
        /// Split a <see cref="ReadOnlySpan{T}"/> based on split value and pass it to the split delegate handler
        /// </summary>
        /// <param name="value"></param>
        /// <param name="splitter">The sequence to split the string on</param>
        /// <param name="options">String split options</param>
        /// <param name="splitCb">The action to invoke when a split segment has been found</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Split(this ReadOnlySpan<char> value, ReadOnlySpan<char> splitter, StringSplitOptions options, StatelessSpanAction splitCb)
        {
            //Create a SpanSplitDelegate with the non-typed delegate as the state argument
            static void ssplitcb(ReadOnlySpan<char> param, StatelessSpanAction callback) => callback(param);
            //Call split with the new callback delegate
            Split(value, splitter, options, ssplitcb, splitCb);
        }

        /// <summary>
        /// Split a <see cref="ReadOnlySpan{T}"/> based on split value and pass it to the split delegate handler
        /// </summary>
        /// <param name="value"></param>
        /// <param name="splitter">The character to split the string on</param>
        /// <param name="options">String split options</param>
        /// <param name="splitCb">The action to invoke when a split segment has been found</param>
        /// <exception cref="ArgumentNullException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Split(this ReadOnlySpan<char> value, char splitter, StringSplitOptions options, StatelessSpanAction splitCb)
        {
            //Create a SpanSplitDelegate with the non-typed delegate as the state argument
            static void ssplitcb(ReadOnlySpan<char> param, StatelessSpanAction callback) => callback(param);
            //Call split with the new callback delegate
            Split(value, splitter, options, ssplitcb, splitCb);
        }

        /// <summary>
        /// Gets the index of the end of the found sequence
        /// </summary>
        /// <param name="data"></param>
        /// <param name="search">Sequence to search for within the current sequence</param>
        /// <returns>the index of the end of the sequenc</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EndOf(this ReadOnlySpan<char> data, ReadOnlySpan<char> search)
        {
            int index = data.IndexOf(search);
            return index > -1 ? index + search.Length : -1;
        }

        /// <summary>
        /// Gets the index of the end of the found character
        /// </summary>
        /// <param name="data"></param>
        /// <param name="search">Character to search for within the current sequence</param>
        /// <returns>the index of the end of the sequence</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EndOf(this ReadOnlySpan<char> data, char search)
        {
            int index = data.IndexOf(search);
            return index > -1 ? index + 1 : -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(this in Memory<byte> data, byte search) => data.Span.IndexOf(search);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(this in Memory<byte> data, ReadOnlySpan<byte> search) => data.Span.IndexOf(search);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(this in Memory<byte> data, ReadOnlyMemory<byte> search) => IndexOf(data, search.Span);

        /// <summary>
        /// Slices the current span from the begining of the segment to the first occurrance of the specified character. 
        /// If the character is not found, the entire segment is returned
        /// </summary>
        /// <param name="data"></param>
        /// <param name="search">The delimiting character</param>
        /// <returns>The segment of data before the search character, or the entire segment if not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> SliceBeforeParam(this ReadOnlySpan<char> data, char search)
        {
            //Find the index of the specified data
            int index = data.IndexOf(search);
            //Return the slice of data before the index, or an empty span if it was not found
            return index > -1 ? data[..index] : data;
        }

        /// <summary>
        /// Slices the current span from the begining of the segment to the first occurrance of the specified character sequence. 
        /// If the character sequence is not found, the entire segment is returned
        /// </summary>
        /// <param name="data"></param>
        /// <param name="search">The delimiting character sequence</param>
        /// <returns>The segment of data before the search character, or the entire <paramref name="data"/> if the seach sequence is not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> SliceBeforeParam(this ReadOnlySpan<char> data, ReadOnlySpan<char> search)
        {
            //Find the index of the specified data
            int index = data.IndexOf(search);
            //Return the slice of data before the index, or an empty span if it was not found
            return index > -1 ? data[..index] : data;
        }

        /// <summary>
        /// Gets the remaining segment of data after the specified search character or <see cref="ReadOnlySpan{T}.Empty"/> 
        /// if the search character is not found within the current segment
        /// </summary>
        /// <param name="data"></param>
        /// <param name="search">The character to search for within the segment</param>
        /// <returns>The segment of data after the search character or <see cref="ReadOnlySpan{T}.Empty"/> if not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> SliceAfterParam(this ReadOnlySpan<char> data, char search)
        {
            //Find the index of the specified data
            int index = EndOf(data, search);

            //Return the slice of data after the index, or an empty span if it was not found
            return index > -1 ? data[index..] : ReadOnlySpan<char>.Empty;
        }

        /// <summary>
        /// Gets the remaining segment of data after the specified search sequence or <see cref="ReadOnlySpan{T}.Empty"/> 
        /// if the search sequence is not found within the current segment
        /// </summary>
        /// <param name="data"></param>
        /// <param name="search">The sequence to search for within the segment</param>
        /// <returns>The segment of data after the search sequence or <see cref="ReadOnlySpan{T}.Empty"/> if not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> SliceAfterParam(this ReadOnlySpan<char> data, ReadOnlySpan<char> search)
        {
            //Find the index of the specified data
            int index = EndOf(data, search);

            //Return the slice of data after the index, or an empty span if it was not found
            return index > -1 ? data[index..] : ReadOnlySpan<char>.Empty;
        }

        /// <summary>
        /// Trims any leading or trailing <c>'\r'|'\n'|' '</c>(whitespace) characters from the segment
        /// </summary>
        /// <returns>The trimmed segment</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<char> TrimCRLF(this ReadOnlySpan<char> data)
        {
            int start = 0, end = data.Length;

            //trim leading \r\n chars
            while(start < end)
            {
                char t = data[start];

                //If character \r or \n slice it off
                if (t != '\r' && t != '\n' && t != ' ')
                {
                    break;
                }

                //Shift
                start++;
            }

            //remove trailing crlf characters
            while (end > start)
            {
                char t = data[end - 1];

                //If character \r or \n slice it off
                if (t != '\r' && t != '\n' && t != ' ')
                {
                    break;
                }

                end--;
            }

            return data[start..end];
        }

        /// <summary>
        /// Replaces a character sequence within the buffer 
        /// </summary>
        /// <param name="buffer">The character buffer to process</param>
        /// <param name="search">The sequence to search for</param>
        /// <param name="replace">The sequence to write in the place of the search parameter</param>
        /// <exception cref="OutOfMemoryException"></exception>
        public static int Replace(this Span<char> buffer, ReadOnlySpan<char> search, ReadOnlySpan<char> replace)
        {
            ForwardOnlyWriter<char> writer = new (buffer);
            writer.Replace(search, replace);
            return writer.Written;
        }

        /// <summary>
        /// Replaces a character sequence within the writer 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="search">The sequence to search for</param>
        /// <param name="replace">The sequence to write in the place of the search parameter</param>
        /// <exception cref="OutOfMemoryException"></exception>
        public static void Replace(this ref ForwardOnlyWriter<char> writer, ReadOnlySpan<char> search, ReadOnlySpan<char> replace)
        {
            Span<char> buffer = writer.AsSpan();

            //If the search and replacement parameters are the same length
            if (search.Length == replace.Length)
            {
                ReplaceInPlace(buffer, search, replace);
                return;
            }

            //Search and replace are not the same length
            int searchLen = search.Length, start = buffer.IndexOf(search);

            if(start == -1)
            {
                return;
            }

            //Init new writer over the buffer
            ForwardOnlyWriter<char> writer2 = new(buffer);

            do
            {
                //Append the data before the search chars
                writer2.Append(buffer[..start]);
                //Append the replacment
                writer2.Append(replace);
                //Shift buffer to the end of the 
                buffer = buffer[(start + searchLen)..];
                //search for next index beyond current index
                start = buffer.IndexOf(search);

            } while (start > -1);

            //Write remaining data
            writer2.Append(replace);

            //Reset writer1 and advance it to the end of writer2
            writer.Reset();
            writer.Advance(writer2.Written);
        }

        /// <summary>
        /// Replaces very ocurrance of character sequence within a buffer with another sequence of the same length
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="search">The sequence to search for</param>
        /// <param name="replace">The sequence to replace the found sequence with</param>
        /// <exception cref="ArgumentException"></exception>
        public static void ReplaceInPlace(this Span<char> buffer, ReadOnlySpan<char> search, ReadOnlySpan<char> replace)
        {
            if(search.Length != replace.Length)
            {
                throw new ArgumentException("Search parameter and replacment parameter must be the same length");
            }

            int start = buffer.IndexOf(search);

            while(start > -1)
            {
                //Shift the buffer to the begining of the search parameter
                buffer = buffer[start..];

                //Overwite the search parameter
                replace.CopyTo(buffer);

                //Search for next index of the search character
                start = buffer.IndexOf(search);
            }
        }
    }
}