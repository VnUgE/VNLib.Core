/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: VnString.cs 
*
* VnString.cs is part of VNLib.Utils which is part of the larger 
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
using System.IO;
using System.Text;
using System.Buffers;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.IO;
using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory
{

    /// <summary>
    /// Provides an immutable character buffer stored on an unmanged heap. Contains handles to unmanged memory, and should be disposed
    /// </summary>
    [ComVisible(false)]
    [ImmutableObject(true)]
    public sealed class VnString : 
        VnDisposeable, 
        IEquatable<VnString>,
        IEquatable<string>,
        IEquatable<char[]>,
        IComparable<VnString>, 
        IComparable<string>
    {
        private readonly IMemoryHandle<char>? Handle;

        private readonly SubSequence<char> _stringSequence;

        /// <summary>
        /// The number of unicode characters the current instance can reference
        /// </summary>
        public int Length => _stringSequence.Size;
        
        /// <summary>
        /// Gets a value indicating if the current instance is empty
        /// </summary>
        public bool IsEmpty => Length == 0;

        private VnString(SubSequence<char> sequence) => _stringSequence = sequence;

        private VnString(IMemoryHandle<char> handle, nuint start, int length)
        {
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            //get sequence
            _stringSequence = handle.GetSubSequence(start, length);
        }

        /// <summary>
        /// Creates and empty <see cref="VnString"/>, not particularly usefull, just and empty instance.
        /// </summary>
        public VnString()
        {
            //Default string sequence is empty and does not hold any memory
        }
        
        /// <summary>
        /// Creates a new <see cref="VnString"/> around a <see cref="ReadOnlySpan{T}"/> or 
        /// a <see cref="string"/> of data
        /// </summary>
        /// <param name="data"><see cref="ReadOnlySpan{T}"/> of data to replicate</param>
        /// <param name="heap">The heap to allocate the buffer from</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <remarks>Copies the value into internal memory</remarks>
        public VnString(ReadOnlySpan<char> data, IUnmangedHeap? heap = null)
        {
            //Default to shared heap
            heap ??= MemoryUtil.Shared;

            //Create new handle and copy incoming data to it
            Handle = heap.AllocAndCopy(data);
            
            //Get subsequence over the whole copy of data
            _stringSequence = Handle.GetSubSequence(0, data.Length);
        }

        /// <summary>
        /// Creates a new <see cref="VnString"/> from the binary data, using the specified encoding
        /// and allocating the internal buffer from the desired heap, or <see cref="MemoryUtil.Shared"/>
        /// heap instance if null. If the <paramref name="data"/> is empty, an empty <see cref="VnString"/>
        /// is returned.
        /// </summary>
        /// <param name="data">The data to decode</param>
        /// <param name="encoding"></param>
        /// <param name="heap"></param>
        /// <returns>The decoded string from the binary data, or an empty string if no data was provided</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static VnString FromBinary(ReadOnlySpan<byte> data, Encoding encoding, IUnmangedHeap? heap = null)
        {
            _ = encoding ?? throw new ArgumentNullException(nameof(encoding));

            if (data.IsEmpty)
            {
                return new VnString();
            }

            //Fall back to shared heap
            heap ??= MemoryUtil.Shared;

            //Get the number of characters
            int numChars = encoding.GetCharCount(data);

            //New handle for decoded data
            MemoryHandle<char> charBuffer = heap.Alloc<char>(numChars);
            try
            {
                //Write characters to character buffer
                _ = encoding.GetChars(data, charBuffer.Span);
                //Consume the new handle
                return ConsumeHandle(charBuffer, 0, numChars);
            }
            catch
            {
                //If an error occured, dispose the buffer
                charBuffer.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates a new Vnstring from the <see cref="MemoryHandle{T}"/> buffer provided. This function "consumes"
        /// a handle, meaning it now takes ownsership of the the memory it points to.
        /// </summary>
        /// <param name="handle">The <see cref="MemoryHandle{T}"/> to consume</param>
        /// <param name="start">The offset from the begining of the buffer marking the begining of the string</param>
        /// <param name="length">The number of characters this string points to</param>
        /// <returns>The new <see cref="VnString"/></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static VnString ConsumeHandle(IMemoryHandle<char> handle, nuint start, int length)
        {
            if (handle is null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            //Check handle bounts
            MemoryUtil.CheckBounds(handle, start, (nuint)length);

            return new VnString(handle, start, length);
        }

        /// <summary>
        /// Allocates a temporary buffer to read data from the stream until the end of the stream is reached.
        /// Decodes data from the user-specified encoding
        /// </summary>
        /// <param name="stream">Active stream of data to decode to a string</param>
        /// <param name="encoding"><see cref="Encoding"/> to use for decoding</param>
        /// <param name="heap">The heap to allocate the buffer from</param>
        /// <param name="bufferSize">The size of the buffer to allocate during copying</param>
        /// <returns>The new <see cref="VnString"/> instance</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static VnString FromStream(Stream stream, Encoding encoding, IUnmangedHeap heap, int bufferSize)
        {
            _ = stream ?? throw new ArgumentNullException(nameof(stream));
            _ = encoding ?? throw new ArgumentNullException(nameof(encoding));
            _ = heap ?? throw new ArgumentNullException(nameof(heap));

            //Make sure the stream is readable
            if (!stream.CanRead)
            {
                throw new InvalidOperationException();
            }
            //See if the stream is a vn memory stream
            if (stream is VnMemoryStream vnms)
            {
               return FromBinary(vnms.AsSpan(), encoding, heap);
            }
            //Try to get the internal buffer from am memory span
            else if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> arrSeg))
            {
                return FromBinary(arrSeg.AsSpan(), encoding, heap);
            }
            //Need to read from the stream old school with buffers
            else
            {
                //Allocate a binary buffer 
                using UnsafeMemoryHandle<byte> binBuffer = heap.UnsafeAlloc<byte>(bufferSize);

                //Create a new char bufer that will expand dyanmically
                MemoryHandle<char> charBuffer = heap.Alloc<char>(bufferSize);

                try
                {
                    int length = 0;
                    //span ref to bin buffer
                    Span<byte> buffer = binBuffer.Span;
                    //Run in checked context for overflows
                    checked
                    {
                        do
                        {
                            //read
                            int read = stream.Read(buffer);
                            //guard
                            if (read <= 0)
                            {
                                break;
                            }
                            //Slice into only the read data
                            ReadOnlySpan<byte> readbytes = buffer[..read];
                            //get num chars
                            int numChars = encoding.GetCharCount(readbytes);
                            //Guard for overflow
                            if (((ulong)(numChars + length)) >= int.MaxValue)
                            {
                                throw new OverflowException();
                            }
                            //Re-alloc buffer
                            charBuffer.ResizeIfSmaller(length + numChars);
                            //Decode and update position
                            _= encoding.GetChars(readbytes, charBuffer.Span.Slice(length, numChars));
                            //Update char count
                            length += numChars;
                        } while (true);
                    }
                    return ConsumeHandle(charBuffer, 0, length);
                }
                catch
                {
                    //Free the memory allocated
                    charBuffer.Dispose();
                    //We still want the exception to be propagated!
                    throw;
                }
            }
        }       
        
        /// <summary>
        /// Asynchronously reads data from the specified stream and uses the specified encoding 
        /// to decode the binary data to a new <see cref="VnString"/> heap character buffer.
        /// </summary>
        /// <param name="stream">The stream to read data from</param>
        /// <param name="encoding">The encoding to use while decoding data</param>
        /// <param name="heap">The <see cref="IUnmangedHeap"/> to allocate buffers from</param>
        /// <param name="bufferSize">The size of the buffer to allocate</param>
        /// <returns>The new <see cref="VnString"/> containing the data</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static async ValueTask<VnString> FromStreamAsync(Stream stream, Encoding encoding, IUnmangedHeap heap, int bufferSize)
        {
            _ = stream ?? throw new ArgumentNullException(nameof(stream));
            _ = encoding ?? throw new ArgumentNullException(nameof(encoding));
            _ = heap ?? throw new ArgumentNullException(nameof(heap));
            
            //Make sure the stream is readable
            if (!stream.CanRead)
            {
                throw new IOException("The input stream is not readable");
            }

            /*
             * If the stream is some type of memory stream, we can just use 
             * the underlying buffer if possible
             */
            if (stream is VnMemoryStream vnms)
            {
                return FromBinary(vnms.AsSpan(), encoding, heap);
            }
            else if(stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> arrSeg))
            {
                return FromBinary(arrSeg.AsSpan(), encoding, heap);
            }
            else
            {
                //Rent a temp binary buffer
                using MemoryManager<byte> binBuffer = heap.DirectAlloc<byte>(bufferSize);

                //Create a new char buffer starting with the buffer size
                MemoryHandle<char> charBuffer = heap.Alloc<char>(bufferSize);

                try
                {
                    int length = 0;
                    do
                    {
                        //read async
                        int read = await stream.ReadAsync(binBuffer.Memory);

                        //guard
                        if (read <= 0)
                        {
                            break;
                        }

                        //calculate the number of characters 
                        int numChars = encoding.GetCharCount(binBuffer.Memory.Span[..read]);

                        //Guard for overflow
                        if (((ulong)(numChars + length)) >= int.MaxValue)
                        {
                            throw new OverflowException("The provided stream is larger than 2gb and is not supported");
                        }

                        //Re-alloc buffer
                        charBuffer.ResizeIfSmaller(length + numChars);

                        //Decode and update position
                        _ = encoding.GetChars(binBuffer.GetSpan()[..read], charBuffer.Span.Slice(length, numChars));

                        //Update char count
                        length += numChars;
                    } while (true);
                    return ConsumeHandle(charBuffer, 0, length);
                }
                catch
                {
                    //Free the memory allocated
                    charBuffer.Dispose();
                    //We still want the exception to be propagated!
                    throw;
                }
            }
        }

        /// <summary>
        /// Gets the value of the character at the specified index
        /// </summary>
        /// <param name="index">The index of the character to get</param>
        /// <returns>The <see cref="char"/> at the specified index within the buffer</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public char CharAt(int index)
        {
            //Check
            Check();

            //Check bounds
            return _stringSequence.Span[index];
        }

#pragma warning disable IDE0057 // Use range operator
        /// <summary>
        /// Creates a <see cref="VnString"/> that is a window within the current string,
        /// the referrence points to the same memory as the first instnace.
        /// </summary>
        /// <param name="start">The index within the current string to begin the child string</param>
        /// <param name="count">The number of characters (or length) of the child string</param>
        /// <returns>The child <see cref="VnString"/></returns>
        /// <remarks>
        /// Making substrings will reference the parents's underlying <see cref="MemoryHandle{T}"/>
        /// and all children will be set in a disposed state when the parent instance is disposed
        /// </remarks>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public VnString Substring(int start, int count)
        {
            //Check
            Check();

            //Check bounds
            if (start < 0 || (start + count) >= Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            //get sub-sequence slice for the current string
            SubSequence<char> sub = _stringSequence.Slice((nuint)start, count);

            //Create new string with offsets pointing to same internal referrence
            return new VnString(sub);
        }
        
        /// <summary>
        /// Creates a <see cref="VnString"/> that is a window within the current string,
        /// the referrence points to the same memory as the first instnace.
        /// </summary>
        /// <param name="start">The index within the current string to begin the child string</param>
        /// <returns>The child <see cref="VnString"/></returns>
        /// <remarks>
        /// Making substrings will reference the parents's underlying <see cref="MemoryHandle{T}"/>
        /// and all children will be set in a disposed state when the parent instance is disposed
        /// </remarks>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public VnString Substring(int start) => Substring(start, (Length - start));

        /// <summary>
        /// Creates a substring wrapper of the internal string designated by the 
        /// given range.
        /// </summary>
        /// <param name="range">The range of elements to create the wraper around</param>
        /// <returns>
        /// A new <see cref="VnString"/> instance pointing to the new substring window.
        /// Memory belongs to the original string instance.
        /// </returns>
        public VnString this[Range range]
        {
            get
            {
                //get start
                int start = range.Start.IsFromEnd ? Length - range.Start.Value : range.Start.Value;
                //Get end
                int end = range.End.IsFromEnd ? Length - range.End.Value : range.End.Value;
                //Handle strings with no ending range
                return (end >= start) ? Substring(start, (end - start)) : Substring(start);
            }
        }
#pragma warning restore IDE0057 // Use range operator

        /// <summary>
        /// Gets a <see cref="ReadOnlySpan{T}"/> over the internal character buffer
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<char> AsSpan()
        {
            //Check
            Check();
            return _stringSequence.Span;
        }

        /// <summary>
        /// Gets a <see cref="string"/> copy of the internal buffer
        /// </summary>
        /// <returns><see cref="string"/> representation of internal data</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public override string ToString()
        {
            //Create a new 
            return AsSpan().ToString();
        }
       
        /// <summary>
        /// Gets the value of the character at the specified index
        /// </summary>
        /// <param name="index">The index of the character to get</param>
        /// <returns>The <see cref="char"/> at the specified index within the buffer</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public char this[int index] => CharAt(index);

        //Casting to a vnstring should be explicit so the caller doesnt misuse memory managment        
        public static explicit operator ReadOnlySpan<char>(VnString? value) => Unsafe.IsNullRef(ref value) || value!.Disposed ? ReadOnlySpan<char>.Empty : value.AsSpan();       
        public static explicit operator VnString(string value) => new (value);
        public static explicit operator VnString(ReadOnlySpan<char> value) => new (value);
        public static explicit operator VnString(char[] value) => new (value);
        ///<inheritdoc/>
        public override bool Equals(object? obj)
        {
            if(obj == null)
            {
                return false;
            }
            return obj switch
            {
                VnString => Equals(obj as VnString), //Use operator overload
                string => Equals(obj as string), //Use operator overload
                char[] => Equals(obj as char[]), //Use operator overload
                _ => false,
            };
        }
        ///<inheritdoc/>
        public bool Equals(VnString? other) => !ReferenceEquals(other, null) && Equals(other.AsSpan());
        ///<inheritdoc/>
        public bool Equals(VnString? other, StringComparison stringComparison) => !ReferenceEquals(other, null) && Equals(other.AsSpan(), stringComparison);
        ///<inheritdoc/>
        public bool Equals(string? other) => Equals(other.AsSpan());
        ///<inheritdoc/>
        public bool Equals(string? other, StringComparison stringComparison) => Equals(other.AsSpan(), stringComparison);
        ///<inheritdoc/>
        public bool Equals(char[]? other) => Equals(other.AsSpan());
        ///<inheritdoc/>
        public bool Equals(char[]? other, StringComparison stringComparison) => Equals(other.AsSpan(), stringComparison);
        ///<inheritdoc/>
        public bool Equals(ReadOnlySpan<char> other, StringComparison stringComparison = StringComparison.Ordinal) => Length == other.Length && AsSpan().Equals(other, stringComparison);
        ///<inheritdoc/>
        public bool Equals(in SubSequence<char> other) => Length == other.Size && AsSpan().SequenceEqual(other.Span);
        ///<inheritdoc/>
        public int CompareTo(string? other) => AsSpan().CompareTo(other, StringComparison.Ordinal);
        ///<inheritdoc/>
        ///<exception cref="ArgumentNullException"></exception>
        public int CompareTo(VnString? other)
        {
            _ = other ?? throw new ArgumentNullException(nameof(other));
            return AsSpan().CompareTo(other.AsSpan(), StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets a hashcode for the underyling string by using the .NET <see cref="string.GetHashCode()"/>
        /// method on the character representation of the data
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// It is safe to compare hashcodes of <see cref="VnString"/> to the <see cref="string"/> class or 
        /// a character span etc
        /// </remarks>
        /// <exception cref="ObjectDisposedException"></exception>
        public override int GetHashCode() => GetHashCode(StringComparison.Ordinal);

        /// <summary>
        /// Gets a hashcode for the underyling string by using the .NET <see cref="string.GetHashCode()"/>
        /// method on the character representation of the data
        /// </summary>
        /// <param name="stringComparison">The string comperison mode</param>
        /// <returns></returns>
        /// <remarks>
        /// It is safe to compare hashcodes of <see cref="VnString"/> to the <see cref="string"/> class or 
        /// a character span etc
        /// </remarks>
        /// <exception cref="ObjectDisposedException"></exception>
        public int GetHashCode(StringComparison stringComparison) => string.GetHashCode(AsSpan(), stringComparison);

        ///<inheritdoc/>
        protected override void Free() => Handle?.Dispose();

        public static bool operator ==(VnString left, VnString right) => left is null ? right is not null : left.Equals(right, StringComparison.Ordinal);

        public static bool operator !=(VnString left, VnString right) => !(left == right);

        public static bool operator <(VnString left, VnString right) => left is null ? right is not null : left.CompareTo(right) < 0;

        public static bool operator <=(VnString left, VnString right) => left is null || left.CompareTo(right) <= 0;

        public static bool operator >(VnString left, VnString right) => left is not null && left.CompareTo(right) > 0;

        public static bool operator >=(VnString left, VnString right) => left is null ? right is null : left.CompareTo(right) >= 0;
    }
}