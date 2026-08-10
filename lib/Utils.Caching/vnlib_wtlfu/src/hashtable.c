/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: hashtable.c
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
* hashtable.c - Fixed-capacity hash table implementation for vnlib_wtlfu.
*
* Open-addressing with linear probing. The slot array is caller-allocated
* and lives inline after the WtlHashTable header. The table does not own
* entry memory. Slots are empty (NULL), tombstone (WTL_TABLE_TOMBSTONE),
* or occupied (entry pointer + hash).
*
* Lookup probes from (hash & (capacity-1)) until it finds an empty slot
* or a matching hash. Tombstones are skipped during probe. Returns the
* first entry whose stored hash matches — the caller is responsible for
* full key comparison if hashes can collide.
*/

#include "hashtable.h"
#include "debug.h"
#include "span.h"

static _vn_inline WtlHashSlot* _tableSlots(WtlHashTable* table, uint32_t offset)
{
    DEBUG_ASSERT(table);
    DEBUG_ASSERT(offset < table->capacity);
    return (WtlHashSlot*)(table + 1) + offset;
}

static _vn_inline const WtlHashSlot* _tableSlotsC(const WtlHashTable* table, uint32_t offset)
{
    DEBUG_ASSERT(table);
    DEBUG_ASSERT(offset < table->capacity);
    return (const WtlHashSlot*)(table + 1) + offset;
}

static _vn_inline void _tableSetAsTombstone(WtlHashTable* table, WtlHashSlot* slot)
{
    DEBUG_ASSERT(slot->entry && slot->entry != WTL_TABLE_TOMBSTONE);
    slot->entry = WTL_TABLE_TOMBSTONE;
    slot->hash = 0;
    table->count--;
    table->tombstones++;
}

static _vn_inline uint32_t _tableMask(const WtlHashTable* table)
{
    return table->capacity - 1;
}

_VN_WTLFU_INTERNAL uint32_t wtlfuHashTableMemorySize(uint32_t capacity)
{
    return sizeof(WtlHashTable) + ((size_t)capacity * sizeof(WtlHashSlot));
}

_VN_WTLFU_INTERNAL uint32_t wtlfuHashTableCount(const WtlHashTable* table)
{
    DEBUG_ASSERT(table);
    return table ? table->count : 0;
}

_VN_WTLFU_INTERNAL uint32_t wtlfuHashTableCapacity(const WtlHashTable* table)
{
    DEBUG_ASSERT(table);
    return table ? table->capacity : 0;
}


_VN_WTLFU_INTERNAL void wtlfuHashTableInit(
    WtlHashTable* table,
    uint32_t      capacity
)
{
    DEBUG_ASSERT(table);
    DEBUG_ASSERT(capacity > 0 && (capacity & (capacity - 1)) == 0);

    if (!table || capacity == 0 || (capacity & (capacity - 1)) != 0)
    {
        return;
    }

    memset(table, 0, wtlfuHashTableMemorySize(capacity));

    table->capacity = capacity;
}

_VN_WTLFU_INTERNAL void* wtlfuHashTableLookup(
    const WtlHashTable* table,
    uint32_t            hash
)
{   
    uint32_t mask = 0;  

    DEBUG_ASSERT(table);
    if (!table || table->capacity == 0)
    {
        return NULL;
    }

    mask = _tableMask(table);

    for (uint32_t i = 0; i < table->capacity; i++)
    {
        const WtlHashSlot* slot = _tableSlotsC(table, (hash + i) & mask);

        // Empty slot — end of probe chain, not found
        if (!slot->entry)
        {
            return NULL;
        }

        // Tombstone — skip, keep probing
        if (slot->entry == WTL_TABLE_TOMBSTONE)
        {         
            continue;
        }

        // Occupied — check hash match
        if (slot->hash == hash)
        {
            return slot->entry;
        }
    }

    // Wrapped around to start — table is full of non-matching entries
    return NULL;
}

_VN_WTLFU_INTERNAL int wtlfuHashTableInsert(
    WtlHashTable* table,
    uint32_t      hash,
    void*         entry
)
{    
    uint32_t mask = 0;
    WtlHashSlot* tombSlot = NULL;

    DEBUG_ASSERT(table);
    DEBUG_ASSERT(entry);
    DEBUG_ASSERT2(entry != WTL_TABLE_TOMBSTONE, "entry must not be the tombstone sentinel");

    if (!table || !entry || table->capacity == 0)
    {
        return -1;
    }

    // Table is full (no empty or tombstone slots available)
    if (table->count + table->tombstones >= table->capacity)
    {
        return -1;
    }
   
    mask = _tableMask(table);

    for (uint32_t i = 0; i < table->capacity; i++)
    {
        WtlHashSlot* slot = _tableSlots(table, (hash + i) & mask);

        // Empty slot — end of probe chain
        if (!slot->entry)
        {
            // Prefer the tombstone if one was found earlier, leaving
            // the empty slot as a proper chain terminator
            WtlHashSlot* target = tombSlot ? tombSlot : slot;

            target->entry = entry;
            target->hash = hash;
            table->count++;

            if (tombSlot)
            {
                table->tombstones--;
            }

            return 0;
        }

        // Tombstone — remember first one, keep probing for duplicate
        if (slot->entry == WTL_TABLE_TOMBSTONE)
        {
            if (!tombSlot)
            {
                tombSlot = slot;
            }

            continue;
        }

        // Occupied with same hash — duplicate
        if (slot->hash == hash)
        {
            return 1;
        }
    }

    // Unreachable: the fullness guard guarantees at least one empty slot
    // exists, so the loop must always hit it before exhausting all slots
    DEBUG_ASSERT2(0, "Insert probe loop exhausted without finding an empty slot; fullness guard may be broken");
    return -1;
}

_VN_WTLFU_INTERNAL void* wtlfuHashTableRemove(
    WtlHashTable* table,
    uint32_t      hash
)
{
    WtlHashSlot* slot = NULL;
    uint32_t mask = 0;
    void* removed = NULL;

    DEBUG_ASSERT(table);

    if (!table || table->capacity == 0)
    {
        return NULL;
    }

    mask = _tableMask(table);

    for (uint32_t i = 0; i < table->capacity; i++)
    {
        slot = _tableSlots(table, (hash + i) & mask);

        // Empty slot — end of probe chain, not found
        if (!slot->entry)
        {
            return NULL;
        }

        // Tombstone — skip, keep probing
        if (slot->entry == WTL_TABLE_TOMBSTONE)
        {
            continue;
        }

        // Occupied with matching hash — remove
        if (slot->hash == hash)
        {
            removed = slot->entry;
            
            _tableSetAsTombstone(table, slot);

            return removed;
        }
    }

    return NULL;
}

_VN_WTLFU_INTERNAL int wtlfuHashTableRemoveEntry(
    WtlHashTable* table,
    uint32_t      hash,
    const void*   entry
)
{
    WtlHashSlot* slot = NULL;
    uint32_t mask = 0;

    DEBUG_ASSERT(table);
    DEBUG_ASSERT(entry);

    if (!table || !entry || table->capacity == 0)
    {
        return -1;
    }

    mask = _tableMask(table);

    for (uint32_t i = 0; i < table->capacity; i++)
    {
        slot = _tableSlots(table, (hash + i) & mask);

        // Empty slot — end of probe chain, not found
        if (!slot->entry)
        {
            return -1;
        }

        // Tombstone — skip, keep probing
        if (slot->entry == WTL_TABLE_TOMBSTONE)
        {
            continue;
        }

        // Occupied — check pointer match
        if (slot->entry == entry)
        {
            _tableSetAsTombstone(table, slot);
            return 0;
        }
    }

    return -1;
}

_VN_WTLFU_INTERNAL void wtlfuHashTableClear(WtlHashTable* table)
{
    DEBUG_ASSERT(table);
   
    if (!table)
    {
        return;
    }

    wtlfuHashTableInit(table, table->capacity);
}


_VN_WTLFU_INTERNAL void wtlfuHashTableRehash(WtlHashTable* table)
{   
    WtlHashSlot* slots = NULL;
    uint32_t mask = 0;

    DEBUG_ASSERT(table);

    if (!table || table->capacity == 0 || table->tombstones == 0)
    {
        return;
    }
   
    mask = _tableMask(table);
    slots = _tableSlots(table, 0);

    // Convert all tombstones to empty slots so live entries can be
    // freely moved without interference from stale markers
    for (uint32_t i = 0; i < table->capacity; i++)
    {
        WtlHashSlot* slot = &slots[i];

        if (slot->entry == WTL_TABLE_TOMBSTONE)
        {
            slot->entry = NULL;
            slot->hash = 0;
        }
    }   

    // For each occupied slot, if the entry is not at its home position,
    // pull it out and reinsert by probing from its home slot. Repeated
    // displacement is safe because emptied slots become NULL (empty),
    // which terminates probe chains for lookups correctly.
    for (uint32_t i = 0; i < table->capacity; i++)
    {
        WtlHashSlot* curr = &slots[i];
        void* entry = NULL;
        uint32_t hash = 0;

        // Skip empty slots
        if (!curr->entry)
        {
            continue;
        }

        // Already at home — no move needed
        if ((curr->hash & mask) == i)
        {
            continue;
        }

        // Pull the entry out
        entry = curr->entry;
        hash = curr->hash;

        curr->entry = NULL;
        curr->hash = 0;

        // Probe from home for the first empty slot
        {
            uint32_t j = hash & mask;
            uint32_t probed = 0;

            for (; probed < table->capacity; probed++)
            {
                if (!slots[j].entry)
                {
                    slots[j].entry = entry;
                    slots[j].hash = hash;
                    break;
                }

                j = (j + 1) & mask;
            }

            DEBUG_ASSERT2(probed < table->capacity, "rehash reinsert failed; no empty slot found");
        }
    }

    table->tombstones = 0;
}
