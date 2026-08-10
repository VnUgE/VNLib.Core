/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: hashtable.h
*
* This library is free software; you can redistribute it and/or
* modify it under the terms of the GNU Lesser General Public License
* as published by the Free Software Foundation; either version 2.1
* of the License, or (at your option) any later version.
*
* This library is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* Lesser General Public License for more details.
*
* You should have received a copy of the GNU Lesser General Public License
* along with vnlib_wtlfu. If not, see http://www.gnu.org/licenses/.
*/

#pragma once

#ifndef _VN_WTLFU_TABLE_H
#define _VN_WTLFU_TABLE_H

#include <stdint.h>
#include <stddef.h>
#include "platform.h"

/*
* Tombstone sentinel. A slot whose entry pointer equals this value
* is a deleted slot (tombstone), not an empty slot.
*/
#define WTL_TABLE_TOMBSTONE ((void*)(intptr_t)-1)

/*
* Slot in the open-addressing table. A slot is:
*   - empty      when entry == NULL
*   - tombstone  when entry == WTL_TABLE_TOMBSTONE
*   - occupied   otherwise
*/
typedef struct WtlHashSlot
{
    void*    entry;
    uint32_t hash;
}
WtlHashSlot;

typedef struct WtlHashTable
{    
    uint32_t capacity;      /* power of 2, set at init */
    uint32_t count;         /* live entries */
    uint32_t tombstones;    /* deleted slots */
}
WtlHashTable;

/*
* Returns the total byte size for a single allocation holding
* the WtlHashTable header followed by `capacity` inline slots.
* Returns 0 if capacity is 0 or not a power of 2.
*/
_VN_WTLFU_INTERNAL uint32_t wtlfuHashTableMemorySize(uint32_t capacity);

_VN_WTLFU_INTERNAL void wtlfuHashTableInit(
    WtlHashTable* table,
    uint32_t      capacity
);

_VN_WTLFU_INTERNAL void* wtlfuHashTableLookup(
    const WtlHashTable* table,
    uint32_t            hash
);

_VN_WTLFU_INTERNAL int wtlfuHashTableInsert(
    WtlHashTable* table,
    uint32_t      hash,
    void*         entry
);

_VN_WTLFU_INTERNAL void* wtlfuHashTableRemove(
    WtlHashTable* table,
    uint32_t      hash
);

_VN_WTLFU_INTERNAL int wtlfuHashTableRemoveEntry(
    WtlHashTable* table,
    uint32_t      hash,
    const void*   entry
);

_VN_WTLFU_INTERNAL void wtlfuHashTableRehash(WtlHashTable* table);

_VN_WTLFU_INTERNAL void wtlfuHashTableClear(WtlHashTable* table);

_VN_WTLFU_INTERNAL uint32_t wtlfuHashTableCount(const WtlHashTable* table);

_VN_WTLFU_INTERNAL uint32_t wtlfuHashTableCapacity(const WtlHashTable* table);

#endif /* !_VN_WTLFU_TABLE_H */
