/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Utils
* File: VnTable.cs 
*
* VnTable.cs is part of VNLib.Utils which is part of the larger 
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
using System.Diagnostics;

using VNLib.Utils.Extensions;

namespace VNLib.Utils.Memory
{
    /// <summary>
    /// Provides a Row-Major ordered table for use of storing value-types in umnaged heap memory
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class VnTable<T> : VnDisposeable, IIndexable<uint, T> where T : unmanaged
    {
        private readonly MemoryHandle<T>? BufferHandle;
        
        /// <summary>
        /// A value that indicates if the table does not contain any values
        /// </summary>
        public bool Empty { get; }
        
        /// <summary>
        /// The number of rows in the table
        /// </summary>
        public uint Rows { get; }
        
        /// <summary>
        /// The nuber of columns in the table
        /// </summary>
        public uint Cols { get; }
        
        /// <summary>
        /// Creates a new 2 dimensional table in unmanaged heap memory, using the <see cref="MemoryUtil.Shared"/> heap.
        /// User should dispose of the table when no longer in use
        /// </summary>
        /// <param name="rows">Number of rows in the table</param>
        /// <param name="cols">Number of columns in the table</param>
        public VnTable(uint rows, uint cols) : this(MemoryUtil.Shared, rows, cols) { }

        /// <summary>
        /// Creates a new 2 dimensional table in unmanaged heap memory, using the specified heap.
        /// User should dispose of the table when no longer in use
        /// </summary>
        /// <param name="heap"><see cref="IUnmangedHeap"/> to allocate table memory from</param>
        /// <param name="rows">Number of rows in the table</param>
        /// <param name="cols">Number of columns in the table</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="OverflowException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        public VnTable(IUnmangedHeap heap, uint rows, uint cols)
        {
            //empty table
            if (rows == 0 && cols == 0)
            {
                Empty = true;
            }
            else
            {
                ulong tableSize = checked((ulong)rows * (ulong)cols);

                ArgumentNullException.ThrowIfNull(heap);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tableSize, nuint.MinValue);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(MemoryUtil.ByteCount<T>((nuint)tableSize), nuint.MaxValue, nameof(rows));

                Rows = rows;
                Cols = cols;

                //Alloc a buffer with zero memory enabled, with Rows * Cols number of elements
                BufferHandle = heap.Alloc<T>((nuint)tableSize, true);
            }
        }
        
        /// <summary>
        /// Gets the value of an item in the table at the given indexes
        /// </summary>
        /// <param name="row">Row address of the item</param>
        /// <param name="col">Column address of item</param>
        /// <returns>The value of the item</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public T Get(uint row, uint col)
        {
            ValidateArgs(row, col);

            //Calculate the address in memory for the item
            //Calc row offset
            ulong address = checked(row * Cols);
            
            //Calc column offset
            address += col;
            
            unsafe
            {
                //Get the value item
                return *(BufferHandle!.GetOffset((nuint)address));
            }
        }
        
        /// <summary>
        /// Sets the value of an item in the table at the given address
        /// </summary>
        /// <param name="item">Value of item to store</param>
        /// <param name="row">Row address of the item</param>
        /// <param name="col">Column address of item</param>
        /// <returns>The value of the item</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void Set(uint row, uint col, T item)
        {
            ValidateArgs(row, col);

            //Calculate the address in memory for the item

            //Calc row offset
            ulong address = checked(Cols * row);
            
            //Calc column offset
            address += col;
            
            //Set the value item
            unsafe
            {
                *BufferHandle!.GetOffset((nuint)address) = item;
            }
        }

        private void ValidateArgs(uint row, uint col)
        {
            Check();

            if (Empty)
            {
                throw new InvalidOperationException("Table is empty");
            }

            //If not empty expect a non-null handle
            Debug.Assert(BufferHandle != null, nameof(BufferHandle) + " != null");

            ArgumentOutOfRangeException.ThrowIfGreaterThan(row, Rows);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(col, Cols);
        }

        /// <summary>
        /// Equivalent to <see cref="VnTable{T}.Get(uint, uint)"/> and <see cref="VnTable{T}.Set(uint, uint, T)"/>
        /// </summary>
        /// <param name="row">Row address of item</param>
        /// <param name="col">Column address of item</param>
        /// <returns>The value of the item</returns>
        public T this[uint row, uint col]
        {
            get => Get(row, col);
            set => Set(row, col, value);
        }
        
        /// <summary>
        /// Allows for direct addressing in the table. 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public unsafe T this[uint index]
        {
            get
            {
                Check();
                return !Empty ? *(BufferHandle!.GetOffset(index)) : throw new InvalidOperationException("Cannot index an empty table");
            }

            set
            {
                Check();
                if (Empty)
                {
                    throw new InvalidOperationException("Cannot index an empty table");
                }
                *(BufferHandle!.GetOffset(index)) = value;
            }
        }
        
        ///<inheritdoc/>
        protected override void Free()
        {
            if (!Empty)
            {
                //Dispose the buffer
                BufferHandle!.Dispose();
            }
        }
    }
}