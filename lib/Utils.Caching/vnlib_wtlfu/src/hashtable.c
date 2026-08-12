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

static _vn_inline uint32_t _tableMask(const WtlHashTable* table)
{
    return table->capacity - 1;
}

static _vn_inline void _tableSetAsTombstone(WtlHashTable* table, WtlHashSlot* slot)
{
    DEBUG_ASSERT(table);
    DEBUG_ASSERT(slot);

    // Slot already tombstone
    if (slot->status == WTL_TABLE_STATUS_TOMB)
    {
        return;
    }

    DEBUG_ASSERT(slot->status == WTL_TABLE_STATUS_IN_USE);

    // Wipe slot date, then reset the tombstone status
    memset(slot, 0, sizeof(WtlHashSlot));

    slot->status = WTL_TABLE_STATUS_TOMB;
    table->tombstones++;
    table->count--;
}

static WtlHashSlot* tableFindSlot(WtlHashTable* table, uint32_t hash)
{   
    uint32_t mask = 0;  

    DEBUG_ASSERT(table);   
    if (!table || table->capacity == 0)
    {
        return NULL;
    }

    // Programmer error if hash is 0. It will match any empty slot
    DEBUG_ASSERT(hash != 0);

    mask = _tableMask(table);

    for (uint32_t i = 0; i < table->capacity; i++)
    {
        WtlHashSlot* slot = _tableSlots(table, (hash + i) & mask);

        switch (slot->status)
        {
        // Empty slot — end of probe chain, not found
        case WTL_TABLE_STATUS_EMPTY:
            return NULL;

        // Tombstone — skip, keep probing
        case WTL_TABLE_STATUS_TOMB:
            break;
        
        // slot is in use, check hash matches
        case WTL_TABLE_STATUS_IN_USE:
            {
                if (slot->entry.hash == hash)
                {
                    return slot;
                }
            }
            // Hash does not match prob next
            break;
        }
    }

    // Wrapped around to start — table is full of non-matching entries
    return NULL;
}

_VN_WTLFU_INTERNAL uint32_t wtlHashTableMemorySize(uint32_t capacity)
{
    return capacity && (!(capacity & (capacity - 1)))
        ? sizeof(WtlHashTable) + ((size_t)capacity * sizeof(WtlHashSlot))
        : 0;
}

_VN_WTLFU_INTERNAL uint32_t wtlHashTableCount(const WtlHashTable* table)
{
    DEBUG_ASSERT(table);
    return table ? table->count : 0;
}

_VN_WTLFU_INTERNAL uint32_t wtlHashTableCapacity(const WtlHashTable* table)
{
    DEBUG_ASSERT(table);
    return table ? table->capacity : 0;
}

_VN_WTLFU_INTERNAL void wtlHashTableInit(WtlHashTable* table, uint32_t capacity)
{
    DEBUG_ASSERT(table);
    DEBUG_ASSERT(capacity > 0 && (capacity & (capacity - 1)) == 0);

    if (!table || capacity == 0 || (capacity & (capacity - 1)) != 0)
    {
        return;
    }

    memset(table, 0, wtlHashTableMemorySize(capacity));

    table->capacity = capacity;
}

_VN_WTLFU_INTERNAL wtl_ht_entry_t* wtlHashTableLookup(WtlHashTable* table, uint32_t hash)
{
    WtlHashSlot* slot = tableFindSlot(table, hash);
    
    return slot ? &slot->entry : NULL;
}

_VN_WTLFU_INTERNAL int wtlHashTableInsert(WtlHashTable* table, uint32_t hash, _Out_ wtl_ht_entry_t** entry)
{    
    uint32_t mask = 0;
    WtlHashSlot* tombSlot = NULL;

    DEBUG_ASSERT(table);

    if (!table || table->capacity == 0)
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
        switch (slot->status)
        {
        case WTL_TABLE_STATUS_EMPTY:
            {
                // Prefer the tombstone if one was found earlier, leaving
                // the empty slot as a proper chain terminator
                WtlHashSlot* target = tombSlot ? tombSlot : slot;               

                target->entry.hash = hash;
                (*entry) = &target->entry;
                
                table->count++;

                if (tombSlot)
                {
                    table->tombstones--;
                }

                return WTL_SUCCESS;
            }
        case WTL_TABLE_STATUS_IN_USE:
            {
                // Occupied with same hash — duplicate
                if (slot->entry.hash == hash) 
                {
                    return 1;
                }
                break;
            }
            // Tombstone — remember first one, keep probing for duplicate
        case WTL_TABLE_STATUS_TOMB:
            {
                if (!tombSlot)
                {
                    tombSlot = slot;
                }

                continue;
            }
        }       
    }

    // Unreachable: the fullness guard guarantees at least one empty slot
    // exists, so the loop must always hit it before exhausting all slots
    DEBUG_ASSERT2(0, "Insert probe loop exhausted without finding an empty slot; fullness guard may be broken");
    return -1;
}

_VN_WTLFU_INTERNAL int wtlHashTableRemove(WtlHashTable* table, uint32_t hash)
{
    WtlHashSlot* slot = tableFindSlot(table, hash);

    if (!slot)
    {
        return -1;
    }

    // Make the slot a tombstone and update table
    _tableSetAsTombstone(table, slot);

    return WTL_SUCCESS;
}

_VN_WTLFU_INTERNAL int wtuHashTableRemoveEntry(WtlHashTable* table, wtl_ht_entry_t* entry)
{
    DEBUG_ASSERT(entry);
    
    return entry ? wtlHashTableRemove(table, entry->hash) : -1;
}

_VN_WTLFU_INTERNAL void wtlHashTableClear(WtlHashTable* table)
{
    DEBUG_ASSERT(table);
   
    if (!table)
    {
        return;
    }

    wtlHashTableInit(table, table->capacity);
}

_VN_WTLFU_INTERNAL void wtlHashTableRehash(WtlHashTable* table)
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

        if (slot->status == WTL_TABLE_STATUS_TOMB)
        {
            // Clear slot back to defaults (sets to free)
            memset(slot, 0, sizeof(WtlHashSlot));
        }
    }   

    table->tombstones = 0;

    // For each occupied slot, if the entry is not at its home position,
    // pull it out and reinsert by probing from its home slot. Repeated
    // displacement is safe because emptied slots become NULL (empty),
    // which terminates probe chains for lookups correctly.
    for (uint32_t i = 0; i < table->capacity; i++)
    {
        WtlHashSlot* curr = &slots[i];
        wtl_ht_entry_t entry;
        uint32_t hash = 0;

        // Skip empty slots
        if (curr->status == WTL_TABLE_STATUS_EMPTY)
        {
            continue;
        }

        // Already at home — no move needed
        if ((curr->entry.hash & mask) == i)
        {
            continue;
        }

        // Pull the entry out
        entry = curr->entry;

        // Clear entry
        memset(curr, 0, sizeof(WtlHashSlot));

        // Probe from home for the first empty slot
        {
            uint32_t j = hash & mask;
            uint32_t probed = 0;

            for (; probed < table->capacity; probed++)
            {
                if (!slots[j].status == WTL_TABLE_STATUS_EMPTY)
                {
                    slots[j].entry = entry;                  
                    break;
                }

                j = (j + 1) & mask;
            }

            DEBUG_ASSERT2(probed < table->capacity, "rehash reinsert failed; no empty slot found");
        }
    }    
}
