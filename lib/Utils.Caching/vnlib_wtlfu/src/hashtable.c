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

static _vn_inline WtlHashSlot* _tableSlot(WtlHashTable* table, uint32_t index)
{
    DEBUG_ASSERT(table);
    DEBUG_ASSERT(index < table->capacity);

    return &table->slots[index];
}

static _vn_inline uint32_t _tableMask(const WtlHashTable* table)
{
    DEBUG_ASSERT(table);
    DEBUG_ASSERT(table->capacity > 0);
    return table->capacity - 1;
}

static _vn_inline void _tableSetAsTombstone(WtlHashTable* table, WtlHashSlot* slot)
{
    DEBUG_ASSERT(table);
    DEBUG_ASSERT(slot);

    // Slot already tombstone
    if (slot->hash == WTL_TABLE_STATUS_TOMB)
    {
        return;
    }

    DEBUG_ASSERT(slot->hash > WTL_TABLE_STATUS_TOMB);

    // Wipe slot date, then reset the tombstone status
    memset(slot, 0, sizeof(WtlHashSlot));

    slot->hash = WTL_TABLE_STATUS_TOMB;
    table->tombstones++;
    table->count--;
}

static _vn_inline void _tableUseTombstone(WtlHashTable* table, WtlHashSlot* slot, uint32_t hash)
{
    DEBUG_ASSERT(table);
    DEBUG_ASSERT(slot);

    DEBUG_ASSERT(slot->hash == WTL_TABLE_STATUS_TOMB);

    // Set to in use by assigning the hash
    slot->hash = hash;
    
    // inc count and decrement tombstones since we are not using it
    table->count++;
    table->tombstones--;
}

static _vn_inline void _tableUseEmpty(WtlHashTable* table, WtlHashSlot* slot, uint32_t hash)
{
    DEBUG_ASSERT(table);
    DEBUG_ASSERT(slot);

    DEBUG_ASSERT(slot->hash == WTL_TABLE_STATUS_EMPTY);

    // Set to in use by assigning the hash
    slot->hash = hash;

    // inc count only and continue
    table->count++;
}

static WtlHashSlot* tableFindSlot(WtlHashTable* table, uint32_t hash)
{   
    uint32_t mask;  

    DEBUG_ASSERT(table);   
    if (!table || table->capacity == 0 || !table->slots)
    {
        return NULL;
    }

    // Programmer error if hash is 0. It will match any empty slot
    DEBUG_ASSERT(hash != 0);

    mask = _tableMask(table);

    for (uint32_t i = 0; i < table->capacity; i++)
    {
        WtlHashSlot* slot = _tableSlot(table, (hash + i) & mask);

        switch (slot->hash)
        {
        // Empty slot — end of probe chain, not found
        case WTL_TABLE_STATUS_EMPTY:
            return NULL;

        // Tombstone — skip, keep probing
        case WTL_TABLE_STATUS_TOMB:
            break;
        
        // slot is in use, check hash matches
        default:
        {
            if (slot->hash == hash)
            {
                return slot;
            }
            // Hash does not match probe next
            break;
        }                
        }
    }

    // Wrapped around to start — table is full of non-matching entries
    return NULL;
}

_VN_WTLFU_INTERNAL int wtlHashTableIsValid(const WtlHashTable* table)
{
    DEBUG_ASSERT(table);
    if (!table || !table->slots)
    {
        return -1;
    }

    // Ensure capacity is greater than 0 and a power of two
    if (table->capacity == 0 || (table->capacity & (table->capacity - 1)))
    {
        return -2;
    }

    return WTL_SUCCESS;
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

_VN_WTLFU_INTERNAL wtl_ht_entry_t* wtlHashTableLookup(WtlHashTable* table, uint32_t hash)
{
    WtlHashSlot* slot = tableFindSlot(table, hash);
    
    return slot ? &slot->entry : NULL;
}

_VN_WTLFU_INTERNAL int wtlHashTableInsert(WtlHashTable* table, uint32_t hash, _Out_ wtl_ht_entry_t** entry)
{    
    uint32_t mask;
    WtlHashSlot* tombSlot = NULL;

    DEBUG_ASSERT(table);

    if (!table || table->capacity == 0)
    {
        return WTL_ERR_INVALID_ARG;
    }

    // A NULL entry pointer cannot receive the assigned slot
    if (!entry)
    {
        return WTL_ERR_INVALID_ARG;
    }

    // Table is full (no empty or tombstone slots available)
    if (table->count + table->tombstones >= table->capacity)
    {
        return -1;
    }
   
    mask = _tableMask(table);

    for (uint32_t i = 0; i < table->capacity; i++)
    {
        WtlHashSlot* slot = _tableSlot(table, (hash + i) & mask);

        // Empty slot — end of probe chain
        switch (slot->hash)
        {
        case WTL_TABLE_STATUS_EMPTY:
            {
                // Found empty slot. If a tombstone was set, use it and leave
                // empty slot to terminate the probe chain.
                
                if (tombSlot)
                {
                    _tableUseTombstone(table, tombSlot, hash);

                    (*entry) = &tombSlot->entry;
                }
                else
                {
                    _tableUseEmpty(table, slot, hash);

                    (*entry) = &slot->entry;
                }               

                return WTL_SUCCESS;
            }
       
            // Tombstone — remember first one, keep probing for possible duplicates
        case WTL_TABLE_STATUS_TOMB:
            {
                if (!tombSlot)
                {
                    tombSlot = slot;
                }

                continue;
            }
            // In use if not tombstone or empty (0)
        default:
            {
                // Occupied with same hash — duplicate
                if (slot->hash == hash) 
                {
                    return WTL_TABLE_ERR_DUPLICATE;
                }

                // Continue probing
                break;
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

    if (!table || !table->slots)
    {
        return;
    }

    // Wipe the entire slot array, then reset the counters
    memset(table->slots, 0, sizeof(WtlHashSlot) * table->capacity);

    table->count = 0;
    table->tombstones = 0;
}

_VN_WTLFU_INTERNAL void wtlHashTableRehash(WtlHashTable* table)
{    
    uint32_t mask = 0;

    DEBUG_ASSERT(table);
    DEBUG_ASSERT(table->slots);

    if (!table || table->capacity == 0 || table->tombstones == 0)
    {
        return;
    }
   
    mask = _tableMask(table);    

    // Convert all tombstones to empty slots so live entries can be
    // freely moved without interference from stale markers
    for (uint32_t i = 0; i < table->capacity; i++)
    {
        WtlHashSlot* slot = _tableSlot(table, i);

        if (slot->hash == WTL_TABLE_STATUS_TOMB)
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
        WtlHashSlot* curr = _tableSlot(table, i);
        wtl_ht_entry_t entry;      

        // Skip empty slots
        if (curr->hash == WTL_TABLE_STATUS_EMPTY)
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

        // Clear entry
        memset(curr, 0, sizeof(WtlHashSlot));

        // Probe from home for the first empty slot
        {
            uint32_t j = (curr->hash & mask);
            uint32_t probed = 0;

            for (; probed < table->capacity; probed++)
            {
                WtlHashSlot* slot = _tableSlot(table, j);
            
                if (slot->hash == WTL_TABLE_STATUS_EMPTY)
                {
                    slot->hash = curr->hash;
                    slot->entry = entry;
                    break;
                }

                j = (j + 1) & mask;
            }

            DEBUG_ASSERT2(probed < table->capacity, "rehash reinsert failed; no empty slot found");
        }
    }    
}
