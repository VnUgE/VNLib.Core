/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: MemoryExtensions.cs 
*
* MemoryExtensions.cs is part of VNLib.Utils which is part of the larger 
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
using System.Text;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using VNLib.Utils.Memory;
using VNLib.Utils.Resources;

namespace VNLib.Utils.Extensions
{

    /// <summary>
    /// Provides memory based extensions to .NET and VNLib memory abstractions
    /// </summary>
    public static class MemoryExtensions
    {
        /// <summary>
        /// Rents a new array and stores it as a resource within an <see cref="OpenResourceHandle{T}"/> to return the 
        /// array when work is completed
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pool"></param>
        /// <param name="size">The minimum size array to allocate</param>
        /// <param name="zero">Should elements from 0 to size be set to default(T)</param>
        /// <returns>A new <see cref="OpenResourceHandle{T}"/> encapsulating the rented array</returns>
        public static UnsafeMemoryHandle<T> Lease<T>(this ArrayPool<T> pool, int size, bool zero = false) where T: unmanaged
        {
            //Pool buffer handles are considered "safe" so im reusing code for now
            return new(pool, size, zero);
        }

        /// <summary>
        /// Retreives a buffer that is at least the reqested length, and clears the array from 0-size. 
        /// <br></br>
        /// The array may be larger than the requested size, and the entire buffer is zeroed
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="size">The minimum length of the array</param>
        /// <param name="zero">True if contents should be zeroed</param>
        /// <returns>The zeroed array</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Rent<T>(this ArrayPool<T> pool, int size, bool zero) 
        {
            //Rent the array
            T[] arr = pool.Rent(size);
            //If zero flag is set, zero only the used section
            if (zero)
            {
                arr.AsSpan().Clear();
            }
            return arr;
        }

        /// <summary>
        /// Copies the characters within the memory handle to a <see cref="string"/>
        /// </summary>
        /// <returns>The string representation of the buffer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToString<T>(this T charBuffer) where T: IMemoryHandle<char>
        {
            return charBuffer.Span.ToString();
        }

        /// <summary>
        /// Wraps the <see cref="MemoryHandle{T}"/> instance in System.Buffers.MemoryManager 
        /// wrapper to provide <see cref="Memory{T}"/> buffers from umanaged handles.
        /// </summary>
        /// <typeparam name="T">The unmanaged data type</typeparam>
        /// <param name="handle"></param>
        /// <param name="ownsHandle">
        /// A value that indicates if the new <see cref="MemoryManager{T}"/> owns the handle. 
        /// When <c>true</c>, the new <see cref="MemoryManager{T}"/> maintains the lifetime of the handle.
        /// </param>
        /// <returns>The <see cref="MemoryManager{T}"/> wrapper</returns>
        /// <remarks>NOTE: This wrapper now manages the lifetime of the current handle</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryManager<T> ToMemoryManager<T>(this MemoryHandle<T> handle, bool ownsHandle = true) where T : unmanaged
        {
            _ = handle ?? throw new ArgumentNullException(nameof(handle));
            return new SysBufferMemoryManager<T>(handle, ownsHandle);
        }

        /// <summary>
        /// Wraps the <see cref="VnTempBuffer{T}"/> instance in System.Buffers.MemoryManager 
        /// wrapper to provide <see cref="Memory{T}"/> buffers from umanaged handles.
        /// </summary>
        /// <typeparam name="T">The unmanaged data type</typeparam>
        /// <param name="handle"></param>
        /// <param name="ownsHandle">
        /// A value that indicates if the new <see cref="MemoryManager{T}"/> owns the handle. 
        /// When <c>true</c>, the new <see cref="MemoryManager{T}"/> maintains the lifetime of the handle.
        /// </param>
        /// <returns>The <see cref="MemoryManager{T}"/> wrapper</returns>
        /// <remarks>NOTE: This wrapper now manages the lifetime of the current handle</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryManager<T> ToMemoryManager<T>(this VnTempBuffer<T> handle, bool ownsHandle = true) where T : unmanaged
        {
            _ = handle ?? throw new ArgumentNullException(nameof(handle));
            return new SysBufferMemoryManager<T>(handle, ownsHandle);
        }

        /// <summary>
        /// Allows direct allocation of a fixed size <see cref="MemoryManager{T}"/> from a <see cref="Win32PrivateHeap"/> instance
        /// of the specified number of elements
        /// </summary>
        /// <typeparam name="T">The unmanaged data type</typeparam>
        /// <param name="heap"></param>
        /// <param name="size">The number of elements to allocate on the heap</param>
        /// <param name="zero">Optionally zeros conents of the block when allocated</param>
        /// <returns>The <see cref="MemoryManager{T}"/> wrapper around the block of memory</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryManager<T> DirectAlloc<T>(this IUnmangedHeap heap, nuint size, bool zero = false) where T : unmanaged
        {
            return new SysBufferMemoryManager<T>(heap, size, zero);
        }

        /// <summary>
        /// Gets the integer length (number of elements) of the <see cref="IMemoryHandle{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle"></param>
        /// <returns>
        /// The integer length of the handle, or throws <see cref="OverflowException"/> if 
        /// the platform is 64bit and the handle is larger than <see cref="int.MaxValue"/>
        /// </returns>
        /// <exception cref="OverflowException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIntLength<T>(this IMemoryHandle<T> handle) => Convert.ToInt32(handle.Length);

        /// <summary>
        /// Gets the integer length (number of elements) of the <see cref="UnsafeMemoryHandle{T}"/>
        /// </summary>
        /// <typeparam name="T">The unmanaged type</typeparam>
        /// <param name="handle"></param>
        /// <returns>
        /// The integer length of the handle, or throws <see cref="OverflowException"/> if 
        /// the platform is 64bit and the handle is larger than <see cref="int.MaxValue"/>
        /// </returns>
        //Method only exists for consistancy since unsafe handles are always 32bit
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIntLength<T>(this in UnsafeMemoryHandle<T> handle) where T: unmanaged => handle.IntLength;

        /// <summary>
        /// Allows direct allocation of a fixed size <see cref="MemoryManager{T}"/> from a <see cref="Win32PrivateHeap"/> instance
        /// of the specified number of elements
        /// </summary>
        /// <typeparam name="T">The unmanaged data type</typeparam>
        /// <param name="heap"></param>
        /// <param name="size">The number of elements to allocate on the heap</param>
        /// <param name="zero">Optionally zeros conents of the block when allocated</param>
        /// <returns>The <see cref="MemoryManager{T}"/> wrapper around the block of memory</returns>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryManager<T> DirectAlloc<T>(this IUnmangedHeap heap, nint size, bool zero = false) where T : unmanaged
        {
            return size >= 0 ? DirectAlloc<T>(heap, (nuint)size, zero) : throw new ArgumentOutOfRangeException(nameof(size), "The size paramter must be a positive integer");
        }
        /// <summary>
        /// Gets an offset pointer from the base postion to the number of bytes specified. Performs bounds checks
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="elements">Number of elements of type to offset</param>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <returns><typeparamref name="T"/> pointer to the memory offset specified</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* GetOffset<T>(this MemoryHandle<T> memory, nint elements) where T : unmanaged
        {
            return elements >= 0 ? memory.GetOffset((nuint)elements) : throw new ArgumentOutOfRangeException(nameof(elements), "The elements paramter must be a positive integer");
        }
        /// <summary>
        /// Resizes the current handle on the heap
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="elements">Positive number of elemnts the current handle should referrence</param>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Resize<T>(this MemoryHandle<T> memory, nint elements) where T : unmanaged
        {
            if (elements < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elements));
            }
            memory.Resize((nuint)elements);
        }

        /// <summary>
        /// Resizes the target handle only if the handle is smaller than the requested element count
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle"></param>
        /// <param name="count">The number of elements to resize to</param>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResizeIfSmaller<T>(this MemoryHandle<T> handle, nint count) where T : unmanaged
        {
            if(count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            ResizeIfSmaller(handle, (nuint)count);
        }

        /// <summary>
        /// Resizes the target handle only if the handle is smaller than the requested element count
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle"></param>
        /// <param name="count">The number of elements to resize to</param>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ResizeIfSmaller<T>(this MemoryHandle<T> handle, nuint count) where T : unmanaged
        {
            //Check handle size
            if(handle.Length < count)
            {
                //handle too small, resize
                handle.Resize(count);
            }
        }


        /// <summary>
        /// Gets a 64bit friendly span offset for the current <see cref="MemoryHandle{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="block"></param>
        /// <param name="offset">The offset (in elements) from the begining of the block</param>
        /// <param name="size">The size of the block (in elements)</param>
        /// <returns>The offset span</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Span<T> GetOffsetSpan<T>(this MemoryHandle<T> block, nuint offset, int size) where T: unmanaged
        {
            _ = block ?? throw new ArgumentNullException(nameof(block));
            
            if(size < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }
            if(size == 0)
            {
                return Span<T>.Empty;
            }
           
            //Check bounds
            MemoryUtil.CheckBounds(block, offset, (nuint)size);
            
            //Get long offset from the destination handle
            void* ofPtr = block.GetOffset(offset);
            return new Span<T>(ofPtr, size);
        }
        /// <summary>
        /// Gets a 64bit friendly span offset for the current <see cref="MemoryHandle{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="block"></param>
        /// <param name="offset">The offset (in elements) from the begining of the block</param>
        /// <param name="size">The size of the block (in elements)</param>
        /// <returns>The offset span</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Span<T> GetOffsetSpan<T>(this MemoryHandle<T> block, nint offset, int size) where T : unmanaged
        {
            return offset >= 0 ? block.GetOffsetSpan((nuint)offset, size) : throw new ArgumentOutOfRangeException(nameof(offset));
        }

        /// <summary>
        /// Gets a <see cref="SubSequence{T}"/> window within the current block
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="block"></param>
        /// <param name="offset">An offset within the handle</param>
        /// <param name="size">The size of the window</param>
        /// <returns>The new <see cref="SubSequence{T}"/> within the block</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SubSequence<T> GetSubSequence<T>(this MemoryHandle<T> block, nuint offset, int size) where T : unmanaged => new (block, offset, size);
        
        /// <summary>
        /// Gets a <see cref="SubSequence{T}"/> window within the current block
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="block"></param>
        /// <param name="offset">An offset within the handle</param>
        /// <param name="size">The size of the window</param>
        /// <returns>The new <see cref="SubSequence{T}"/> within the block</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SubSequence<T> GetSubSequence<T>(this MemoryHandle<T> block, nint offset, int size) where T : unmanaged
        {
            return offset >= 0 ? new (block, (nuint)offset, size) : throw new ArgumentOutOfRangeException(nameof(offset));
        }

        /// <summary>
        /// Wraps the current instance with a <see cref="MemoryPool{T}"/> wrapper
        /// to allow System.Memory buffer rentals.
        /// </summary>
        /// <typeparam name="T">The unmanged data type to provide allocations from</typeparam>
        /// <returns>The new <see cref="MemoryPool{T}"/> heap wrapper.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryPool<T> ToPool<T>(this IUnmangedHeap heap) where T : unmanaged
        {
            return new PrivateBuffersMemoryPool<T>(heap);
        }

        /// <summary>
        /// Allocates a structure of the specified type on the current unmanged heap and zero's its memory
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="heap"></param>
        /// <returns>A pointer to the structure ready for use.</returns>
        /// <remarks>Allocations must be freed with <see cref="StructFree{T}(IUnmangedHeap, T*)"/></remarks>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* StructAlloc<T>(this IUnmangedHeap heap) where T : unmanaged
        {
            //Allocate the struct on the heap and zero memory it points to
            IntPtr handle = heap.Alloc(1, (nuint)sizeof(T), true);
            //returns the handle
            return (T*)handle;
        }
        
        /// <summary>
        /// Frees a structure at the specified address from the this heap. 
        /// This must be the same heap the structure was allocated from
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="heap"></param>
        /// <param name="structPtr">A pointer to the structure</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void StructFree<T>(this IUnmangedHeap heap, T* structPtr) where T : unmanaged
        {
            IntPtr block = new(structPtr);
            //Free block from heap
            heap.Free(ref block);
            //Clear ref
            *structPtr = default;
        }
        
        /// <summary>
        /// Allocates a block of unmanaged memory of the number of elements to store of an unmanged type
        /// </summary>
        /// <typeparam name="T">Unmanaged data type to create a block of</typeparam>
        /// <param name="heap"></param>
        /// <param name="elements">The size of the block (number of elements)</param>
        /// <param name="zero">A flag that zeros the allocated block before returned</param>
        /// <returns>The unmanaged <see cref="MemoryHandle{T}"/></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe MemoryHandle<T> Alloc<T>(this IUnmangedHeap heap, nuint elements, bool zero = false) where T : unmanaged
        {
            //Minimum of one element
            elements = Math.Max(elements, 1);
            //Get element size
            nuint elementSize = (nuint)sizeof(T);
            //If zero flag is set then specify zeroing memory
            IntPtr block = heap.Alloc(elements, elementSize, zero);
            //Return handle wrapper
            return new MemoryHandle<T>(heap, block, elements, zero);
        }

        /// <summary>
        /// Allocates a block of unmanaged memory of the number of elements to store of an unmanged type
        /// </summary>
        /// <typeparam name="T">Unmanaged data type to create a block of</typeparam>
        /// <param name="heap"></param>
        /// <param name="elements">The size of the block (number of elements)</param>
        /// <param name="zero">A flag that zeros the allocated block before returned</param>
        /// <returns>The unmanaged <see cref="MemoryHandle{T}"/></returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryHandle<T> Alloc<T>(this IUnmangedHeap heap, nint elements, bool zero = false) where T : unmanaged
        {
            return elements >= 0 ? Alloc<T>(heap, (nuint)elements, zero) : throw new ArgumentOutOfRangeException(nameof(elements));
        }
        
        /// <summary>
        /// Allocates a buffer from the current heap and initialzies it by copying the initial data buffer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="heap"></param>
        /// <param name="initialData">The initial data to set the buffer to</param>
        /// <returns>The initalized <see cref="MemoryHandle{T}"/> block</returns>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemoryHandle<T> AllocAndCopy<T>(this IUnmangedHeap heap, ReadOnlySpan<T> initialData) where T:unmanaged
        {
            //Aloc block
            MemoryHandle<T> handle = heap.Alloc<T>(initialData.Length);
            
            //Copy initial data
            MemoryUtil.Copy(initialData, handle, 0);

            return handle;
        }

        /// <summary>
        /// Copies data from the input buffer to the current handle and resizes the handle to the 
        /// size of the buffer
        /// </summary>
        /// <typeparam name="T">The unamanged value type</typeparam>
        /// <param name="handle"></param>
        /// <param name="input">The input buffer to copy data from</param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteAndResize<T>(this MemoryHandle<T> handle, ReadOnlySpan<T> input) where T: unmanaged
        {
            handle.Resize(input.Length);
            MemoryUtil.Copy(input, handle, 0);
        }

        /// <summary>
        /// Allocates a block of unamanged memory of the number of elements of an unmanaged type, and 
        /// returns the <see cref="UnsafeMemoryHandle{T}"/> that must be used cautiously. 
        /// If elements is less than 1 an empty handle is returned
        /// </summary>
        /// <typeparam name="T">The unamanged value type</typeparam>
        /// <param name="heap">The heap to allocate block from</param>
        /// <param name="elements">The number of elements to allocate</param>
        /// <param name="zero">A flag to zero the initial contents of the buffer</param>
        /// <returns>The allocated handle of the specified number of elements</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe UnsafeMemoryHandle<T> UnsafeAlloc<T>(this IUnmangedHeap heap, int elements, bool zero = false) where T : unmanaged
        {
            if (elements < 1)
            {
                //Return an empty handle
                return new UnsafeMemoryHandle<T>();
            }
            
            //Get element size
            nuint elementSize = (nuint)sizeof(T);
            
            //If zero flag is set then specify zeroing memory (safe case because of the above check)
            IntPtr block = heap.Alloc((nuint)elements, elementSize, zero);
            
            //handle wrapper
            return new (heap, block, elements);
        }

        #region VnBufferWriter

        /// <summary>
        /// Formats and appends a value type to the writer with proper endianess
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="value">The value to format and append to the buffer</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Append<T>(this ref ForwardOnlyWriter<byte> buffer, T value) where T: unmanaged
        {
            //Calc size of structure and fix te size of the buffer
            int size = Unsafe.SizeOf<T>();            
            Span<byte> output = buffer.Remaining[..size];

            //Format value and write to buffer
            MemoryMarshal.Write(output, ref value);

            //If byte order is reversed, reverse elements
            if (!BitConverter.IsLittleEndian)
            {
                output.Reverse();
            }

            //Update written posiion
            buffer.Advance(size);
        }

        /// <summary>
        /// Formats and appends a value type to the writer with proper endianess
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="value">The value to format and append to the buffer</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static void Append<T>(this ref ForwardOnlyMemoryWriter<byte> buffer, T value) where T : struct
        {
            //Format value and write to buffer
            int size = Unsafe.SizeOf<T>();
            Span<byte> output = buffer.Remaining.Span[..size];

            //Format value and write to buffer
            MemoryMarshal.Write(output, ref value);

            //If byte order is reversed, reverse elements
            if (BitConverter.IsLittleEndian)
            {
                output.Reverse();
            }

            //Update written posiion
            buffer.Advance(size);
        }

        /// <summary>
        /// Formats and appends the value to end of the buffer
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="value">The value to format and append to the buffer</param>
        /// <param name="format">An optional format argument</param>
        /// <param name="formatProvider"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Append<T>(this ref ForwardOnlyWriter<char> buffer, T value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = default) where T : ISpanFormattable
        {
            //Format value and write to buffer
            if (!value.TryFormat(buffer.Remaining, out int charsWritten, format, formatProvider))
            {
                throw new ArgumentOutOfRangeException(nameof(buffer), "The value could not be formatted and appended to the buffer, because there is not enough available space");
            }
            //Update written posiion
            buffer.Advance(charsWritten);
        }

        /// <summary>
        /// Formats and appends the value to end of the buffer
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="value">The value to format and append to the buffer</param>
        /// <param name="format">An optional format argument</param>
        /// <param name="formatProvider"></param>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Append<T>(this ref ForwardOnlyMemoryWriter<char> buffer, T value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = default) where T : ISpanFormattable
        {
            //Format value and write to buffer
            if (!value.TryFormat(buffer.Remaining.Span, out int charsWritten, format, formatProvider))
            {
                throw new ArgumentOutOfRangeException(nameof(buffer), "The value could not be formatted and appended to the buffer, because there is not enough available space");
            }
            //Update written posiion
            buffer.Advance(charsWritten);
        }

        /// <summary>
        /// Encodes a set of characters in the input characters span and any characters
        /// in the internal buffer into a sequence of bytes that are stored in the input
        /// byte span. A parameter indicates whether to clear the internal state of the 
        /// encoder after the conversion.
        /// </summary>
        /// <param name="enc"></param>
        /// <param name="chars">Character buffer to encode</param>
        /// <param name="offset">The offset in the char buffer to begin encoding chars from</param>
        /// <param name="charCount">The number of characers to encode</param>
        /// <param name="writer">The buffer writer to use</param>
        /// <param name="flush">true to clear the internal state of the encoder after the conversion; otherwise, false.</param>
        /// <returns>The actual number of bytes written at the location indicated by the bytes parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBytes(this Encoder enc, char[] chars, int offset, int charCount, ref ForwardOnlyWriter<byte> writer, bool flush)
        {
            return GetBytes(enc, chars.AsSpan(offset, charCount), ref writer, flush);
        }
        /// <summary>
        /// Encodes a set of characters in the input characters span and any characters
        /// in the internal buffer into a sequence of bytes that are stored in the input
        /// byte span. A parameter indicates whether to clear the internal state of the 
        /// encoder after the conversion.
        /// </summary>
        /// <param name="enc"></param>
        /// <param name="chars">The character buffer to encode</param>
        /// <param name="writer">The buffer writer to use</param>
        /// <param name="flush">true to clear the internal state of the encoder after the conversion; otherwise, false.</param>
        /// <returns>The actual number of bytes written at the location indicated by the bytes parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBytes(this Encoder enc, ReadOnlySpan<char> chars, ref ForwardOnlyWriter<byte> writer, bool flush)
        {
            //Encode the characters
            int written = enc.GetBytes(chars, writer.Remaining, flush);
            //Update the writer position
            writer.Advance(written);
            return written;
        }
        /// <summary>
        /// Encodes a set of characters in the input characters span and any characters
        /// in the internal buffer into a sequence of bytes that are stored in the input
        /// byte span.
        /// </summary>
        /// <param name="encoding"></param>
        /// <param name="chars">The character buffer to encode</param>
        /// <param name="writer">The buffer writer to use</param>
        /// <returns>The actual number of bytes written at the location indicated by the bytes parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static int GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, ref ForwardOnlyWriter<byte> writer)
        {
            //Encode the characters
            int written = encoding.GetBytes(chars, writer.Remaining);
            //Update the writer position
            writer.Advance(written);
            return written;
        }
        /// <summary>
        /// Decodes a character buffer in the input characters span and any characters
        /// in the internal buffer into a sequence of bytes that are stored in the input
        /// byte span.
        /// </summary>
        /// <param name="encoding"></param>
        /// <param name="bytes">The binary buffer to decode</param>
        /// <param name="writer">The buffer writer to use</param>
        /// <returns>The actual number of *characters* written at the location indicated by the chars parameter.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static int GetChars(this Encoding encoding, ReadOnlySpan<byte> bytes, ref ForwardOnlyWriter<char> writer)
        {
            int charCount = encoding.GetCharCount(bytes);
            //Encode the characters
            _ = encoding.GetChars(bytes, writer.Remaining);
            //Update the writer position
            writer.Advance(charCount);
            return charCount;
        }

        /// <summary>
        /// Converts the buffer data to a <see cref="PrivateString"/>
        /// </summary>
        /// <returns>A <see cref="PrivateString"/> instance that owns the underlying string memory</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrivateString ToPrivate(this ref ForwardOnlyWriter<char> buffer) => new(buffer.ToString(), true);
        /// <summary>
        /// Gets a <see cref="Span{T}"/> over the modified section of the internal buffer
        /// </summary>
        /// <returns>A <see cref="Span{T}"/> over the modified data</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(this ref ForwardOnlyWriter<T> buffer) => buffer.Buffer[..buffer.Written];


        #endregion

        /// <summary>
        /// Slices the current array by the specified starting offset to the end 
        /// of the array
        /// </summary>
        /// <typeparam name="T">The array type</typeparam>
        /// <param name="arr"></param>
        /// <param name="start">The start offset of the new array slice</param>
        /// <returns>The sliced array</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static T[] Slice<T>(this T[] arr, int start)
        {
            if(start < 0 || start > arr.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }
            Range sliceRange = new(start, arr.Length - start);
            return RuntimeHelpers.GetSubArray(arr, sliceRange);
        }
        /// <summary>
        /// Slices the current array by the specified starting offset to including the 
        /// speciifed number of items
        /// </summary>
        /// <typeparam name="T">The array type</typeparam>
        /// <param name="arr"></param>
        /// <param name="start">The start offset of the new array slice</param>
        /// <param name="count">The size of the new array</param>
        /// <returns>The sliced array</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static T[] Slice<T>(this T[] arr, int start, int count)
        {
            if(start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }
            if(count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if(start + count >= arr.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if(count == 0)
            {
                return Array.Empty<T>();
            }
            //Calc the slice range
            Range sliceRange = new(start, start + count);
            return RuntimeHelpers.GetSubArray(arr, sliceRange);
        }

        /// <summary>
        /// Creates a new sub-sequence over the target handle. (allows for convient sub span)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle"></param>
        /// <param name="start">Intial offset into the handle</param>
        /// <returns>The sub-sequence of the current handle</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(this IMemoryHandle<T> handle, int start) => handle.Span[start..];

        /// <summary>
        /// Creates a new sub-sequence over the target handle. (allows for convient sub span)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle"></param>
        /// <param name="start">Intial offset into the handle</param>
        /// <param name="count">The number of elements within the new sequence</param>
        /// <returns>The sub-sequence of the current handle</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(this IMemoryHandle<T> handle, int start, int count) => handle.Span.Slice(start, count);

        /// <summary>
        /// Creates a new sub-sequence over the target handle. (allows for convient sub span)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle"></param>
        /// <param name="start">Intial offset into the handle</param>
        /// <returns>The sub-sequence of the current handle</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(this in UnsafeMemoryHandle<T> handle, int start) where T: unmanaged => handle.Span[start..];

        /// <summary>
        /// Creates a new sub-sequence over the target handle. (allows for convient sub span)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handle"></param>
        /// <param name="start">Intial offset into the handle</param>
        /// <param name="count">The number of elements within the new sequence</param>
        /// <returns>The sub-sequence of the current handle</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(this in UnsafeMemoryHandle<T> handle, int start, int count) where T : unmanaged => handle.Span.Slice(start, count);

        /// <summary>
        /// Raises an <see cref="ObjectDisposedException"/> if the current handle 
        /// has been disposed or set as invalid
        /// </summary>
        /// <param name="handle"></param>
        /// <exception cref="ObjectDisposedException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfClosed(this SafeHandle handle)
        {
            if (handle.IsClosed || handle.IsInvalid)
            {
                throw new ObjectDisposedException(handle.GetType().Name);
            }
        }
    }
}
