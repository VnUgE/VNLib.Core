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

/*
* wtlHashTable - Serves as canonical memory data structure for all
* runtime storage. 
* 
* The table serves to "allocate" or reserve slots during insertion, returning
* a pointer to a "reserved" or newly in-use wtl_ht_entry_t for the rest of 
* the cache environment to use. It can be looked-up at any time by it's hash
* and a pointer to the same structure should always be returned. Hashes 
* are stored on the entry itself and not the internal slot within the table. 
* 
* Entries may be removed by a pointer to an existing entry, or by it's hash. Once
* removed the slot is "returned" to be ready for reuse. 
* 
* USE-AFTER-FREE-NOTICE: Since the table serves as a canonical data structure
* all references to the entries that have been removed from the table become
* must be destroyed. Entries that have been "removed" become eligible for reuse
* by future entries. 
* 
* The table guarantees that so long as an entry is in-use it may never be used
* by another hash. Lookup may be used at any time to return a pointer only if the 
* hashes match and the entry remains in use.
*/

#pragma once

#ifndef _VN_WTLFU_TABLE_H
#define _VN_WTLFU_TABLE_H

#include <stdint.h>
#include <stddef.h>
#include "internal.h"
#include "platform.h"

#define WTL_TABLE_STATUS_EMPTY   0
#define WTL_TABLE_STATUS_TOMB    1

#define WTL_TABLE_ERR_FULL       -1
#define WTL_TABLE_ERR_DUPLICATE  -10

#define wtl_ht_entry_t WtlEntry

typedef struct WtlHashSlot
{    
    uint32_t        hash;           // hashTable hash value AND tombstone/empty flag
    wtl_ht_entry_t entry;           // Canonical memory for wtlEntries
} WtlHashSlot;

typedef struct WtlHashTable
{
    WtlHashSlot*    slots;
    uint32_t        capacity;      /* power of 2, set at init */
    uint32_t        count;         /* live entries */
    uint32_t        tombstones;    /* deleted slots */   
} WtlHashTable;


_VN_WTLFU_INTERNAL int wtlHashTableIsValid(const WtlHashTable* table);

_VN_WTLFU_INTERNAL wtl_ht_entry_t* wtlHashTableLookup(WtlHashTable* table, uint32_t hash);

/*
* Inserts a new entry for the given hash, reserving a slot and returning
* a pointer to the reserved wtl_ht_entry_t via entry. The caller owns
* populating the returned entry (hash, key, value, LRU links).
*
* @param table   Pointer to the table to insert into
* @param hash    32-bit hash of the key; must be non-zero
* @param entry   Receives a pointer to the reserved entry; must not be NULL
* @return WTL_SUCCESS on success, WTL_TABLE_ERR_FULL if the table has no
*         free slots, WTL_TABLE_ERR_DUPLICATE if the hash is already
*         present, or WTL_ERR_INVALID_ARG if table or entry is NULL
*/
_VN_WTLFU_INTERNAL int wtlHashTableInsert(WtlHashTable* table, uint32_t hash, _Out_ wtl_ht_entry_t** entry);

_VN_WTLFU_INTERNAL int wtlHashTableRemove(WtlHashTable* table, uint32_t hash);

_VN_WTLFU_INTERNAL int wtuHashTableRemoveEntry(WtlHashTable* table, wtl_ht_entry_t* entry);

/*
* Removes all entries from the table, leaving it in the same state as
* a freshly allocated one: every slot empty, count and tombstones zero.
* Capacity is unchanged and the slot array is wiped with a single memset.
*
* NOTE: All entry pointers previously returned by wtlHashTableInsert become
* invalid after this call.
*
* @param table  Pointer to the table to clear
*/
_VN_WTLFU_INTERNAL void wtlHashTableClear(WtlHashTable* table);

_VN_WTLFU_INTERNAL void wtlHashTableRehash(WtlHashTable* table);

_VN_WTLFU_INTERNAL uint32_t wtlHashTableCount(const WtlHashTable* table);

_VN_WTLFU_INTERNAL uint32_t wtlHashTableCapacity(const WtlHashTable* table);

#endif /* !_VN_WTLFU_TABLE_H */
