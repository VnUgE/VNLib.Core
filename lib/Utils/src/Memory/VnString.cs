/*
* Copyright (c) 2025 Vaughn Nugent
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
using System.Diagnostics;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.IO;
using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory
{

    /// <summary>
    /// Provides an immutable character buffer stored on an Unmanaged heap. Contains handles to Unmanaged memory, and should be disposed
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
        private readonly IMemoryHandle<char>? _handle;

        private readonly SubSequence<char> _stringSequence;

        /// <summary>
        /// The number of unicode characters the current instance can reference
        /// </summary>
        public int Length => _stringSequence.Size;
        
        /// <summary>
        /// Gets a value indicating if the current instance is empty
        /// </summary>
        public bool IsEmpty => Length == 0;

        private VnString(IMemoryHandle<char>? handle, SubSequence<char> sequence)
        {
            _handle = handle;
            _stringSequence = sequence;
        }

        private VnString(IMemoryHandle<char> handle, nuint start, int length)
        {
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            //get sequence
            _stringSequence = handle.GetSubSequence(start, length);
        }

        /// <summary>
        /// Creates and empty <see cref="VnString"/>, not particularly usefull, just and empty instance.
        /// </summary>
        public VnString()
        {
            //Default string sequence is empty and does not hold any memory
            
            Debug.Assert(Length == 0 && IsEmpty);
        }
        
        /// <summary>
        /// Creates a new <see cref="VnString"/> around a <see cref="ReadOnlySpan{T}"/> or 
        /// a <see cref="string"/> of data
        /// </summary>
        /// <param name="data"><see cref="ReadOnlySpan{T}"/> of data to replicate</param>
        /// <param name="heap">The heap to allocate the buffer from</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <remarks>Copies the value into internal memory</remarks>
        public VnString(ReadOnlySpan<char> data, IUnmanagedHeap? heap = null)
        {
            //Default to shared heap
            heap ??= MemoryUtil.Shared;

            //Create new handle and copy incoming data to it
            _handle = heap.AllocAndCopy(data);
            
            //Get subsequence over the whole copy of data
            _stringSequence = _handle.GetSubSequence(offset: 0, data.Length);
        }

        /// <summary>
        /// Creates a new <see cref="VnString"/> from the binary data, using the specified encoding
        /// and allocating the internal buffer from the desired heap, or <see cref="MemoryUtil.Shared"/>
        /// heap instance if null. If the <paramref name="data"/> is empty, an empty <see cref="VnString"/>
        /// is returned.
        /// </summary>
        /// <param name="data">The data to decode</param>
        /// <param name="encoding">The encoding to use for decoding</param>
        /// <param name="heap">The heap to allocate the buffer from</param>
        /// <returns>The decoded string from the binary data, or an empty string if no data was provided</returns>
        /// <exception cref="ArgumentNullException">Thrown when encoding is null</exception>
        public static VnString FromBinary(ReadOnlySpan<byte> data, Encoding encoding, IUnmanagedHeap? heap = null)
        {
            ArgumentNullException.ThrowIfNull(encoding);

            if (data.IsEmpty)
            {
                return new VnString();
            }

            // Fall back to shared heap
            heap ??= MemoryUtil.Shared;

            // Get the number of characters
            int numChars = encoding.GetCharCount(data);

            // New handle for decoded data
            MemoryHandle<char> charBuffer = heap.Alloc<char>(numChars);
            try
            {
                // Write characters to character buffer
                numChars = encoding.GetChars(data, charBuffer.Span);

                // Consume the new handle
                return ConsumeHandle(charBuffer, 0, numChars);
            }
            catch
            {
                // If an error occurred, dispose the buffer
                charBuffer.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates a new Vnstring from the <see cref="MemoryHandle{T}"/> buffer provided. This function "consumes"
        /// a handle, meaning it now takes ownsership of the the memory it points to.
        /// </summary>
        /// <param name="handle">The <see cref="MemoryHandle{T}"/> to consume</param>
        /// <param name="start">The offset from the beginning of the buffer marking the beginning of the string</param>
        /// <param name="length">The number of characters this string points to</param>
        /// <returns>The new <see cref="VnString"/></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static VnString ConsumeHandle(IMemoryHandle<char> handle, nuint start, int length)
        {
            ArgumentNullException.ThrowIfNull(handle);
            ArgumentOutOfRangeException.ThrowIfNegative(length);

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
        public static VnString FromStream(Stream stream, Encoding encoding, IUnmanagedHeap heap, int bufferSize)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(encoding);
            ArgumentNullException.ThrowIfNull(heap);

            //Make sure the stream is readable
            if (!stream.CanRead)
            {
                throw new IOException("The input stream is not readable");
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

                    //Run in checked context for overflows
                    checked
                    {
                        do
                        {                            
                            int read = stream.Read(binBuffer.Span);
                            
                            if (read <= 0)
                            {
                                break;
                            }

                            //Slice into only the read data
                            ReadOnlySpan<byte> readbytes = binBuffer.AsSpan(0, read);

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
                            numChars = encoding.GetChars(
                                bytes: readbytes, 
                                chars: charBuffer.AsSpan(length, numChars)
                            );
                            
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
        /// <param name="heap">The <see cref="IUnmanagedHeap"/> to allocate buffers from</param>
        /// <param name="bufferSize">The size of the buffer to allocate</param>
        /// <returns>The new <see cref="VnString"/> containing the data</returns>
        /// <exception cref="IOException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public static async ValueTask<VnString> FromStreamAsync(Stream stream, Encoding encoding, IUnmanagedHeap heap, int bufferSize)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(encoding);
            ArgumentNullException.ThrowIfNull(heap);
            
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
                using MemoryManager<byte> binBuffer = heap.AllocMemory<byte>(bufferSize);

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
                        _ = encoding.GetChars(
                            bytes: binBuffer.GetSpan()[..read], 
                            chars: charBuffer.AsSpan(length, numChars)
                        );

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
            Check();

            //Check bounds
            return _stringSequence.Span[index];
        }

#pragma warning disable IDE0057 // Use range operator

        /// <summary>
        /// Creates a <see cref="VnString"/> that is a window within the current string,
        /// the reference points to the same memory as the first instance.
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
            Check();

            ArgumentOutOfRangeException.ThrowIfNegative(start, nameof(start));
            ArgumentOutOfRangeException.ThrowIfNegative(count, nameof(count));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(start + count, Length, nameof(start));

            /*
             * Slice the string and do not pass the handle even if we have it because the new 
             * instance does own the buffer
             */
            VnString str = new (
                handle: null,
                _stringSequence.Slice((nuint)start, count)
            );

            //Sanity checks
            Debug.Assert(str.Length == count);
            Debug.Assert(str._handle == null);

            return str;
        }
        
        /// <summary>
        /// Creates a <see cref="VnString"/> that is a window within the current string,
        /// the reference points to the same memory as the first instance.
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
               
                int start = range.Start.IsFromEnd 
                    ? (Length - range.Start.Value) 
                    : range.Start.Value;
            
                int end = range.End.IsFromEnd 
                    ? (Length - range.End.Value) 
                    : range.End.Value;
               
                return (end >= start) 
                    ? Substring(start, (end - start)) 
                    : Substring(start);
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
        public override string ToString() => AsSpan().ToString();

        /// <summary>
        /// Gets the value of the character at the specified index
        /// </summary>
        /// <param name="index">The index of the character to get</param>
        /// <returns>The <see cref="char"/> at the specified index within the buffer</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public char this[int index] => CharAt(index);

        //Casting to a vnstring should be explicit so the caller doesnt misuse memory managment        

        /// <summary>
        /// Explicitly casts a <see cref="VnString"/> to a <see cref="ReadOnlySpan{T}"/>
        /// </summary>
        /// <param name="value">The <see cref="VnString"/> to cast to a <see cref="ReadOnlySpan{T}"/></param>
        public static explicit operator ReadOnlySpan<char>(VnString? value)
            => value is null || value.Disposed ? [] : value.AsSpan();

        /// <summary>
        /// Explicitly casts a <see cref="ReadOnlySpan{T}"/> to a <see cref="VnString"/>
        /// </summary>
        /// <param name="value">The character sequence to cast to a <see cref="VnString"/></param>
        public static explicit operator VnString(ReadOnlySpan<char> value) 
            => value.IsEmpty ? new VnString() : new VnString(value);

        /// <summary>
        /// Explicitly casts a <see cref="string"/> to a <see cref="VnString"/>
        /// </summary>
        /// <param name="value">The string to cast to a <see cref="VnString"/></param>
        public static explicit operator VnString(string value)
            => (VnString)value.AsSpan();

        /// <summary>
        /// Explicitly casts a character array to a <see cref="VnString"/>
        /// </summary>
        /// <param name="value">The character array to cast to a <see cref="VnString"/></param>
        public static explicit operator VnString(char[] value) 
            => (VnString)(ReadOnlySpan<char>)value.AsSpan();
        
        /// <inheritdoc/>
        /// <remarks>
        /// NOTE: Avoid this overload if possible. If no explict overload is provided, 
        /// it's assumed the datatype is not supported and will return false
        /// </remarks>
        public override bool Equals(object? obj)
        {
            return obj switch
            {
                VnString => Equals(obj as VnString),
                string => Equals(obj as string),
                char[] => Equals(obj as char[]), 
                _ => false,
            };
        }

        ///<inheritdoc/>
        public bool Equals(ReadOnlySpan<char> other, StringComparison stringComparison = StringComparison.Ordinal)
            => Length == other.Length && AsSpan().Equals(other, stringComparison);

        ///<inheritdoc/>
        public bool Equals(VnString? other) 
            => Equals(other, StringComparison.Ordinal);

        ///<inheritdoc/>
        public bool Equals(VnString? other, StringComparison stringComparison) 
            => other is not null && Equals(other.AsSpan(), stringComparison);
        
        ///<inheritdoc/>
        public bool Equals(string? other) 
            => Equals(other, StringComparison.Ordinal);
        
        ///<inheritdoc/>
        public bool Equals(string? other, StringComparison stringComparison) 
            => Equals(other.AsSpan(), stringComparison);
        
        ///<inheritdoc/>
        public bool Equals(char[]? other) 
            => Equals(other, StringComparison.Ordinal);
        
        ///<inheritdoc/>
        public bool Equals(char[]? other, StringComparison stringComparison) 
            => Equals(other.AsSpan(), stringComparison);

        ///<inheritdoc/>
        public bool Equals(in SubSequence<char> other)
            => Equals(in other, StringComparison.Ordinal);

        ///<inheritdoc/>
        public bool Equals(in SubSequence<char> other, StringComparison stringComparison)
           => Length == other.Size && Equals(other.Span, stringComparison);

        ///<inheritdoc/>
        public int CompareTo(string? other) 
            => CompareTo(other, StringComparison.Ordinal);

        ///<inheritdoc/>
        ///<exception cref="ArgumentNullException"></exception>
        public int CompareTo(string? other, StringComparison stringComparison)
        {
            ArgumentNullException.ThrowIfNull(other);
            return CompareTo(other.AsSpan(), stringComparison);
        }

        ///<inheritdoc/>
        ///<exception cref="ArgumentNullException"></exception>
        public int CompareTo(VnString? other)
        {
            ArgumentNullException.ThrowIfNull(other);
            return CompareTo(other.AsSpan(), StringComparison.Ordinal);
        }

        /// <summary>
        /// Compares the current instance with another read-only span of characters using ordinal comparison.
        /// </summary>
        /// <param name="other">The read-only span of characters to compare with this instance.</param>
        /// <returns>An integer that indicates the relative order of the objects being compared.</returns>
        public int CompareTo(ReadOnlySpan<char> other)
          => AsSpan().CompareTo(other, StringComparison.Ordinal);

        /// <summary>
        /// Compares the current instance with another read-only span of characters using the specified comparison option.
        /// </summary>
        /// <param name="other">The read-only span of characters to compare with this instance.</param>
        /// <param name="comparison">One of the enumeration values that specifies the rules for the comparison.</param>
        /// <returns>An integer that indicates the relative order of the objects being compared.</returns>
        public int CompareTo(ReadOnlySpan<char> other, StringComparison comparison)
            => AsSpan().CompareTo(other, comparison);

        /// <summary>
        /// Gets a hash code for the underlying string using the specified string comparison option.
        /// </summary>
        /// <param name="stringComparison">The string comparison option to use for generating the hash code.</param>
        /// <returns>A hash code for the underlying string.</returns>
        /// <remarks>
        /// It is safe to compare hash codes of <see cref="VnString"/> to the <see cref="string"/> class or 
        /// a character span etc.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
        public int GetHashCode(StringComparison stringComparison)
            => string.GetHashCode(AsSpan(), stringComparison);

        /// <summary>
        /// Gets a hash code for the underlying string using ordinal comparison.
        /// </summary>
        /// <returns>A hash code for the underlying string.</returns>
        /// <remarks>
        /// It is safe to compare hash codes of <see cref="VnString"/> to the <see cref="string"/> class or 
        /// a character span etc.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
        public override int GetHashCode()
            => GetHashCode(StringComparison.Ordinal);

        ///<inheritdoc/>
        protected override void Free() => _handle?.Dispose();

        /// <summary>
        /// Determines whether two specified <see cref="VnString"/> objects have the same value.
        /// </summary>
        /// <param name="left">The first <see cref="VnString"/> to compare.</param>
        /// <param name="right">The second <see cref="VnString"/> to compare.</param>
        /// <returns>true if the value of <paramref name="left"/> is the same as the value of <paramref name="right"/>; otherwise, false.</returns>
        public static bool operator ==(VnString left, VnString right) => left is null ? right is not null : left.Equals(right, StringComparison.Ordinal);

        /// <summary>
        /// Determines whether two specified <see cref="VnString"/> objects have different values.
        /// </summary>
        /// <param name="left">The first <see cref="VnString"/> to compare.</param>
        /// <param name="right">The second <see cref="VnString"/> to compare.</param>
        /// <returns>true if the value of <paramref name="left"/> is different from the value of <paramref name="right"/>; otherwise, false.</returns>
        public static bool operator !=(VnString left, VnString right) => !(left == right);

        /// <summary>
        /// Determines whether the value of one <see cref="VnString"/> is less than the value of another <see cref="VnString"/>.
        /// </summary>
        /// <param name="left">The first <see cref="VnString"/> to compare.</param>
        /// <param name="right">The second <see cref="VnString"/> to compare.</param>
        /// <returns>true if the value of <paramref name="left"/> is less than the value of <paramref name="right"/>; otherwise, false.</returns>
        public static bool operator <(VnString left, VnString right) => left is null ? right is not null : left.CompareTo(right) < 0;

        /// <summary>
        /// Determines whether the value of one <see cref="VnString"/> is less than or equal to the value of another <see cref="VnString"/>.
        /// </summary>
        /// <param name="left">The first <see cref="VnString"/> to compare.</param>
        /// <param name="right">The second <see cref="VnString"/> to compare.</param>
        /// <returns>true if the value of <paramref name="left"/> is less than or equal to the value of <paramref name="right"/>; otherwise, false.</returns>
        public static bool operator <=(VnString left, VnString right) => left is null || left.CompareTo(right) <= 0;

        /// <summary>
        /// Determines whether the value of one <see cref="VnString"/> is greater than the value of another <see cref="VnString"/>.
        /// </summary>
        /// <param name="left">The first <see cref="VnString"/> to compare.</param>
        /// <param name="right">The second <see cref="VnString"/> to compare.</param>
        /// <returns>true if the value of <paramref name="left"/> is greater than the value of <paramref name="right"/>; otherwise, false.</returns>
        public static bool operator >(VnString left, VnString right) => left is not null && left.CompareTo(right) > 0;

        /// <summary>
        /// Determines whether the value of one <see cref="VnString"/> is greater than or equal to the value of another <see cref="VnString"/>.
        /// </summary>
        /// <param name="left">The first <see cref="VnString"/> to compare.</param>
        /// <param name="right">The second <see cref="VnString"/> to compare.</param>
        /// <returns>true if the value of <paramref name="left"/> is greater than or equal to the value of <paramref name="right"/>; otherwise, false.</returns>
        public static bool operator >=(VnString left, VnString right) => left is null ? right is null : left.CompareTo(right) >= 0;
    }
}