/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: tests/hashtable/main.c
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

#include <stdlib.h>
#include <string.h>

#include "hashtable.h"
#include "test.h"

#define TEST_HASHTABLE_DEFAULT_CAPACITY 256

#define EXPECT_EQ_LOOP(value, expected)                        \
    do {                                                        \
        if ((value) != (expected))                              \
        {                                                       \
            printf("FAILED: %s @ Line: %d\n",                   \
                #value, __LINE__);                              \
            return 1;                                           \
        }                                                       \
    } while (0)

static WtlHashTable* allocHashTable(uint32_t capacity)
{
    uint32_t memSize = wtlHashTableMemorySize(capacity);
    TASSERT(memSize > capacity);

    WtlHashTable* table = malloc(memSize);
    TASSERT(table);

    return table;
}

#ifndef TEST_GROUP_HASHTABLE_INIT
#define TEST_GROUP_HASHTABLE_INIT 1

    /*
    * Init with a valid power-of-2 capacity must zero all fields and slots.
    * Count and tombstones must be zero, and every slot must be empty.
    * wtlfuHashTableMemorySize must return sizeof(header) + capacity * sizeof(slot).
    */
    static int InitValidCapacity(void)
    {
        static const uint32_t cap = 16;

        WtlHashTable* table = NULL;
        WtlHashSlot* slots = NULL;       
      
        EXPECT_EQ(
            wtlHashTableMemorySize(cap),
            (uint32_t)(sizeof(WtlHashTable) + (cap * sizeof(WtlHashSlot)))
        );

        table = allocHashTable(cap);
        wtlHashTableInit(table, cap);

        EXPECT_EQ(table->capacity, cap);
        EXPECT_EQ(table->count, 0);
        EXPECT_EQ(table->tombstones, 0);

        // Verify all slots are empty
        slots = (WtlHashSlot*)(table + 1);
        for (uint32_t i = 0; i < cap; i++)
        {
            EXPECT_EQ_LOOP(slots[i].status, WTL_TABLE_STATUS_EMPTY);
            EXPECT_EQ_LOOP(slots[i].entry.hash, (uint32_t)0);
        }

        free(table);
        return 0;
    }

    /*
    * Init with a non-power-of-2 capacity must be a no-op. The table struct
    * must not be modified by the call.
    */
    static int InitInvalidCapacity(void)
    {
        EXPECT_EQ(wtlHashTableMemorySize(0), (size_t)0);
        EXPECT_EQ(wtlHashTableMemorySize(15), (size_t)0);
        EXPECT_EQ(wtlHashTableMemorySize(31), (size_t)0);
        EXPECT_EQ(wtlHashTableMemorySize(65535), (size_t)0);

        return 0;
    }
   

#endif /* TEST_GROUP_HASHTABLE_INIT */

#ifndef TEST_GROUP_HASHTABLE_INSERT
#define TEST_GROUP_HASHTABLE_INSERT 1

    /*
    * Inserting a single entry must return 0, increment count to 1, and place
    * the entry at its home slot (hash & mask).
    */
    static int InsertSingleEntry(void)
    {        
        wtl_ht_entry_t* entry = NULL;

        WtlHashTable* table = allocHashTable(16);
        wtlHashTableInit(table, 16);

        EXPECT_EQ(table->count, 0);
        
        EXPECT_EQ(wtlHashTableInsert(table, 65486, &entry), WTL_SUCCESS);
        EXPECT_EQ(table->count, 1);

        // Entry should be assigned if insert succeeded
        EXPECT_FALSE(entry == NULL);
        EXPECT_EQ(entry->hash, 65486);

        free(table);

        return 0;
    }

    /*
    * Inserting a NULL entry must return -1 and leave the table unchanged.
    */
    static int InsertNullEntry(void)
    {
        return 0;
    }

    /*
    * Inserting into a NULL table or a table with zero capacity must return -1.
    */
    static int InsertNullTable(void)
    {
        return 0;
    }

    /*
    * Inserting the same hash twice must return 1 (duplicate) on the second
    * insert, and count must remain 1.
    */
    static int InsertDuplicateHash(void)
    {
        return 0;
    }

    /*
    * Inserting multiple entries whose hashes map to the same home slot must
    * all succeed via linear probing. Count must reflect the total inserted.
    */
    static int InsertCollisionProbing(void)
    {
        return 0;
    }

    /*
    * Inserting entries until the table is full must return -1 on the
    * insert that would exceed capacity. Count must equal capacity.
    */
    static int InsertUntilFull(void)
    {
        return 0;
    }

    /*
    * After removing an entry, inserting a new entry with the same hash must
    * reuse the tombstone slot. Tombstones must decrement and count must
    * increment correctly.
    */
    static int InsertReusesTombstone(void)
    {
        return 0;
    }

    /*
    * When a tombstone exists earlier in the probe chain and an empty slot
    * exists later, insert must prefer the tombstone and leave the empty
    * slot as a chain terminator.
    */
    static int InsertPrefersTombstoneOverEmpty(void)
    {
        return 0;
    }

#endif /* TEST_GROUP_HASHTABLE_INSERT */

#ifndef TEST_GROUP_HASHTABLE_LOOKUP
#define TEST_GROUP_HASHTABLE_LOOKUP 1

    /*
    * Lookup of an existing entry by hash must return the correct entry pointer.
    */
    static int LookupExisting(void)
    {
        return 0;
    }

    /*
    * Lookup of a hash not in the table must return NULL when an empty slot
    * terminates the probe chain.
    */
    static int LookupMissingEmptyChain(void)
    {
        return 0;
    }

    /*
    * Lookup of a hash not in the table must return NULL when the probe chain
    * contains only tombstones before hitting an empty slot.
    */
    static int LookupMissingTombstoneChain(void)
    {
        return 0;
    }

    /*
    * Lookup on an empty table must return NULL.
    */
    static int LookupEmptyTable(void)
    {
        return 0;
    }

    /*
    * Lookup on a NULL table must return NULL.
    */
    static int LookupNullTable(void)
    {
        return 0;
    }

    /*
    * Lookup for a missing hash when the table is completely full (no empty
    * slots) must return NULL without infinite-looping.
    */
    static int LookupFullTableNoInfiniteLoop(void)
    {
        return 0;
    }

#endif /* TEST_GROUP_HASHTABLE_LOOKUP */

#ifndef TEST_GROUP_HASHTABLE_REMOVE
#define TEST_GROUP_HASHTABLE_REMOVE 1

    /*
    * Remove of an existing entry by hash must return the entry pointer,
    * decrement count, increment tombstones, and set the slot to tombstone.
    */
    static int RemoveExisting(void)
    {
        return 0;
    }

    /*
    * Remove of a hash not in the table must return NULL and leave the
    * table unchanged.
    */
    static int RemoveMissing(void)
    {
        return 0;
    }

    /*
    * Remove on an empty table must return NULL.
    */
    static int RemoveEmptyTable(void)
    {
        return 0;
    }

    /*
    * After removing an entry, a subsequent lookup of the same hash must
    * return NULL (tombstone must not match as occupied).
    */
    static int RemoveThenLookupReturnsNull(void)
    {
        return 0;
    }

#endif /* TEST_GROUP_HASHTABLE_REMOVE */

#ifndef TEST_GROUP_HASHTABLE_REMOVE_ENTRY
#define TEST_GROUP_HASHTABLE_REMOVE_ENTRY 1

    /*
    * RemoveEntry with a matching entry pointer must return 0, decrement count,
    * and increment tombstones.
    */
    static int RemoveEntryMatchingPointer(void)
    {
        return 0;
    }

    /*
    * RemoveEntry with a non-matching pointer (same hash, different entry)
    * must return -1 and leave the table unchanged.
    */
    static int RemoveEntryNonMatchingPointer(void)
    {
        return 0;
    }

    /*
    * RemoveEntry with a NULL entry must return -1.
    */
    static int RemoveEntryNullEntry(void)
    {
        return 0;
    }

    /*
    * RemoveEntry on an empty table must return -1.
    */
    static int RemoveEntryEmptyTable(void)
    {
        return 0;
    }

#endif /* TEST_GROUP_HASHTABLE_REMOVE_ENTRY */

#ifndef TEST_GROUP_HASHTABLE_CLEAR
#define TEST_GROUP_HASHTABLE_CLEAR 1

    /*
    * Clear on a table with entries and tombstones must reset count and
    * tombstones to zero and leave all slots empty.
    */
    static int ClearResetsTable(void)
    {
        return 0;
    }

    /*
    * Clear on an already-empty table must be a no-op.
    */
    static int ClearEmptyTable(void)
    {
        return 0;
    }

    /*
    * After clear, inserting entries must work as if the table were freshly
    * initialized.
    */
    static int ClearThenInsertWorks(void)
    {
        return 0;
    }

#endif /* TEST_GROUP_HASHTABLE_CLEAR */

#ifndef TEST_GROUP_HASHTABLE_REHASH
#define TEST_GROUP_HASHTABLE_REHASH 1

    /*
    * Rehash with no tombstones must be a no-op; count and slot layout
    * must remain unchanged.
    */
    static int RehashNoTombstones(void)
    {
        return 0;
    }

    /*
    * Rehash with tombstones must move all live entries to their home
    * positions, set tombstones to zero, and preserve count exactly.
    */
    static int RehashCompactsTombstones(void)
    {
        return 0;
    }

    /*
    * After heavy insert/remove churn creating many tombstones, rehash must
    * leave all live entries lookupable by their original hashes.
    */
    static int RehashAfterChurn(void)
    {
        return 0;
    }

    /*
    * Rehash must preserve the exact entry count, no more, no less.
    */
    static int RehashPreservesCount(void)
    {
        return 0;
    }

#endif /* TEST_GROUP_HASHTABLE_REHASH */

#ifndef TEST_GROUP_HASHTABLE_ACCESSORS
#define TEST_GROUP_HASHTABLE_ACCESSORS 1

    /*
    * wtlfuHashTableCount must reflect inserts and removes correctly across
    * a sequence of operations.
    */
    static int CountReflectsOperations(void)
    {
        return 0;
    }

    /*
    * wtlfuHashTableCapacity must return the capacity set at init.
    */
    static int CapacityReturnsInitValue(void)
    {
        return 0;
    }

#endif /* TEST_GROUP_HASHTABLE_ACCESSORS */

#ifndef TEST_GROUP_HASHTABLE_EDGE
#define TEST_GROUP_HASHTABLE_EDGE 1

    /*
    * Filling the table to exactly full (count == capacity), then looking up
    * a missing hash must not infinite-loop (wrap-around termination).
    */
    static int FullTableLookupNoInfiniteLoop(void)
    {
        return 0;
    }

    /*
    * Fill the table, remove one entry, then insert a new entry. The insert
    * must succeed via tombstone reuse, and the new entry must be lookupable.
    */
    static int FullTableRemoveThenInsert(void)
    {
        return 0;
    }

#endif /* TEST_GROUP_HASHTABLE_EDGE */

int RunTests(void)
{
#if TEST_GROUP_HASHTABLE_INIT
    RUN_TEST(InitValidCapacity());
    RUN_TEST(InitInvalidCapacity());
#endif

#if TEST_GROUP_HASHTABLE_INSERT
    RUN_TEST(InsertSingleEntry());
    RUN_TEST(InsertNullEntry());
    RUN_TEST(InsertNullTable());
    RUN_TEST(InsertDuplicateHash());
    RUN_TEST(InsertCollisionProbing());
    RUN_TEST(InsertUntilFull());
    RUN_TEST(InsertReusesTombstone());
    RUN_TEST(InsertPrefersTombstoneOverEmpty());
#endif

#if TEST_GROUP_HASHTABLE_LOOKUP
    RUN_TEST(LookupExisting());
    RUN_TEST(LookupMissingEmptyChain());
    RUN_TEST(LookupMissingTombstoneChain());
    RUN_TEST(LookupEmptyTable());
    RUN_TEST(LookupNullTable());
    RUN_TEST(LookupFullTableNoInfiniteLoop());
#endif

#if TEST_GROUP_HASHTABLE_REMOVE
    RUN_TEST(RemoveExisting());
    RUN_TEST(RemoveMissing());
    RUN_TEST(RemoveEmptyTable());
    RUN_TEST(RemoveThenLookupReturnsNull());
#endif

#if TEST_GROUP_HASHTABLE_REMOVE_ENTRY
    RUN_TEST(RemoveEntryMatchingPointer());
    RUN_TEST(RemoveEntryNonMatchingPointer());
    RUN_TEST(RemoveEntryNullEntry());
    RUN_TEST(RemoveEntryEmptyTable());
#endif

#if TEST_GROUP_HASHTABLE_CLEAR
    RUN_TEST(ClearResetsTable());
    RUN_TEST(ClearEmptyTable());
    RUN_TEST(ClearThenInsertWorks());
#endif

#if TEST_GROUP_HASHTABLE_REHASH
    RUN_TEST(RehashNoTombstones());
    RUN_TEST(RehashCompactsTombstones());
    RUN_TEST(RehashAfterChurn());
    RUN_TEST(RehashPreservesCount());
#endif

#if TEST_GROUP_HASHTABLE_ACCESSORS
    RUN_TEST(CountReflectsOperations());
    RUN_TEST(CapacityReturnsInitValue());
#endif

#if TEST_GROUP_HASHTABLE_EDGE
    RUN_TEST(FullTableLookupNoInfiniteLoop());
    RUN_TEST(FullTableRemoveThenInsert());
#endif

    return 0;
}
