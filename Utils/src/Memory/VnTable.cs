/*
* Copyright (c) 2022 Vaughn Nugent
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
        public int Rows { get; }
        /// <summary>
        /// The nuber of columns in the table
        /// </summary>
        public int Cols { get; }
        /// <summary>
        /// Creates a new 2 dimensional table in unmanaged heap memory, using the <see cref="Memory.Shared"/> heap.
        /// User should dispose of the table when no longer in use
        /// </summary>
        /// <param name="rows">Number of rows in the table</param>
        /// <param name="cols">Number of columns in the table</param>
        public VnTable(int rows, int cols) : this(Memory.Shared, rows, cols) { }
        /// <summary>
        /// Creates a new 2 dimensional table in unmanaged heap memory, using the specified heap.
        /// User should dispose of the table when no longer in use
        /// </summary>
        /// <param name="heap"><see cref="PrivateHeap"/> to allocate table memory from</param>
        /// <param name="rows">Number of rows in the table</param>
        /// <param name="cols">Number of columns in the table</param>
        public VnTable(IUnmangedHeap heap, int rows, int cols)
        {
            if (rows < 0 || cols < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rows), "Row and coulmn number must be 0 or larger");
            }
            //empty table
            if (rows == 0 && cols == 0)
            {
                Empty = true;
                return;
            }

            _ = heap ?? throw new ArgumentNullException(nameof(heap));

            this.Rows = rows;
            this.Cols = cols;

            long tableSize = Math.BigMul(rows, cols);

            //Alloc a buffer with zero memory enabled, with Rows * Cols number of elements
            BufferHandle = heap.Alloc<T>(tableSize, true);
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
        public T Get(int row, int col)
        {
            Check();
            if (this.Empty)
            {
                throw new InvalidOperationException("Table is empty");
            }
            if (row < 0 || col < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(row), "Row or column address less than 0");
            }
            if (row > this.Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row), "Row out of range of current table");
            }
            if (col > this.Cols)
            {
                throw new ArgumentOutOfRangeException(nameof(col), "Column address out of range of current table");
            }
            //Calculate the address in memory for the item
            //Calc row offset
            long address = Cols * row;
            //Calc column offset
            address += col;
            unsafe
            {
                //Get the value item
                return *(BufferHandle!.GetOffset(address));
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
        public void Set(int row, int col, T item)
        {
            Check();
            if (this.Empty)
            {
                throw new InvalidOperationException("Table is empty");
            }
            if (row < 0 || col < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(row), "Row or column address less than 0");
            }
            if (row > this.Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row), "Row out of range of current table");
            }
            if (col > this.Cols)
            {
                throw new ArgumentOutOfRangeException(nameof(col), "Column address out of range of current table");
            }
            //Calculate the address in memory for the item
            //Calc row offset
            long address = Cols * row;
            //Calc column offset
            address += col;
            //Set the value item
            unsafe
            {
                *BufferHandle!.GetOffset(address) = item;
            }
        }
        /// <summary>
        /// Equivalent to <see cref="VnTable{T}.Get(int, int)"/> and <see cref="VnTable{T}.Set(int, int, T)"/>
        /// </summary>
        /// <param name="row">Row address of item</param>
        /// <param name="col">Column address of item</param>
        /// <returns>The value of the item</returns>
        public T this[int row, int col]
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
            if (!this.Empty)
            {
                //Dispose the buffer
                BufferHandle!.Dispose();
            }
        }
    }
}