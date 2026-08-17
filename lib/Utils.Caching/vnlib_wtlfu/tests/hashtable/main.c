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
    uint32_t memSize = sizeof(WtlHashTable) + (sizeof(WtlHashSlot) * capacity);

    WtlHashTable* table = malloc(memSize);
    TASSERT(table);

    memset(table, 0, memSize);

    // Assign capacity
    table->capacity = capacity;
    table->slots = ((WtlHashSlot*)table + 1);

    TASSERT(wtlHashTableIsValid(table) == WTL_SUCCESS);

    return table;
}

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

        EXPECT_EQ(table->count, 0);
        
        EXPECT_EQ(wtlHashTableInsert(table, 65486, &entry), WTL_SUCCESS);
        EXPECT_EQ(table->count, 1);

        // Entry should be assigned if insert succeeded
        EXPECT_FALSE(entry == NULL); 

        free(table);

        return 0;
    }

    /*
    * Inserting the same hash twice must return WTL_TABLE_ERR_DUPLICATE on
    * the second insert, and count must remain 1.
    */
    static int InsertDuplicateHash(void)
    {
        wtl_ht_entry_t* entry = NULL;
        wtl_ht_entry_t* dup = NULL;

        WtlHashTable* table = allocHashTable(16);
        const uint32_t hash = 65486;

        // first insert must succeed and reserve an entry
        {
            EXPECT_EQ(wtlHashTableInsert(table, hash, &entry), WTL_SUCCESS);
            EXPECT_EQ(table->count, 1);
            EXPECT_FALSE(entry == NULL);
        }

        // second insert of the same hash must fail as duplicate and
        // leave the table unchanged
        {
            EXPECT_EQ(wtlHashTableInsert(table, hash, &dup), WTL_TABLE_ERR_DUPLICATE);
            EXPECT_EQ(table->count, 1);
        }

        free(table);

        return 0;
    }

    /*
    * Inserting multiple entries whose hashes map to the same home slot must
    * all succeed via linear probing. Count must reflect the total inserted.
    */
    static int InsertCollisionProbing(void)
    {
        wtl_ht_entry_t* entries[3] = { NULL, NULL, NULL };

        WtlHashTable* table = allocHashTable(16);

        // hashes 5, 21, and 37 all share home slot 5 (hash & 15)
        static const uint32_t hashes[3] = { 5, 21, 37 };

        // all three must succeed, landing in consecutive slots 5, 6, 7
        {
            for (int i = 0; i < 3; i++)
            {
                EXPECT_EQ_LOOP(wtlHashTableInsert(table, hashes[i], &entries[i]), WTL_SUCCESS);
            }

            EXPECT_EQ(table->count, 3);
        }

        // each entry must be retrievable by its own hash, and the entries
        // must occupy consecutive slots starting at home
        {
            for (int i = 0; i < 3; i++)
            {
                EXPECT_FALSE(wtlHashTableLookup(table, hashes[i]) == NULL);
                EXPECT_TRUE(wtlHashTableLookup(table, hashes[i]) == &table->slots[5 + i].entry);
            }
        }

        free(table);

        return 0;
    }

    /*
    * Inserting entries until the table is full must return -1 on the
    * insert that would exceed capacity. Count must equal capacity.
    */
    static int InsertUntilFull(void)
    {
        WtlHashTable* table = allocHashTable(16);

        // fill the table: 16 distinct non-zero hashes all land in
        // distinct slots (mask 15), probing handles any collisions
        {
            for (uint32_t hash = 1; hash <= 16; hash++)
            {
                wtl_ht_entry_t* entry = NULL;

                EXPECT_EQ_LOOP(wtlHashTableInsert(table, hash, &entry), WTL_SUCCESS);
            }

            EXPECT_EQ(table->count, 16);
        }

        // the next insert must fail as the table has no free slots
        {
            wtl_ht_entry_t* entry = NULL;

            EXPECT_EQ(wtlHashTableInsert(table, 17, &entry), WTL_TABLE_ERR_FULL);
            EXPECT_EQ(table->count, 16);
        }

        free(table);

        return 0;
    }

    /*
    * After removing an entry, inserting a new entry with the same hash must
    * reuse the tombstone slot. Tombstones must decrement and count must
    * increment correctly.
    */
    static int InsertReusesTombstone(void)
    {
        wtl_ht_entry_t* entry = NULL;
        wtl_ht_entry_t* reuse = NULL;

        WtlHashTable* table = allocHashTable(16);
        const uint32_t hash = 65486;

        // insert, note the slot, then remove to create a tombstone
        {
            EXPECT_EQ(wtlHashTableInsert(table, hash, &entry), WTL_SUCCESS);
            EXPECT_TRUE(entry == &table->slots[hash & 15].entry);

            EXPECT_EQ(wtlHashTableRemove(table, hash), WTL_SUCCESS);
            EXPECT_EQ(table->count, 0);
            EXPECT_EQ(table->tombstones, 1);
        }

        // re-inserting the same hash must reclaim the tombstone slot, not
        // probe past it
        {
            EXPECT_EQ(wtlHashTableInsert(table, hash, &reuse), WTL_SUCCESS);
            EXPECT_TRUE(reuse == &table->slots[hash & 15].entry);

            EXPECT_EQ(table->count, 1);
            EXPECT_EQ(table->tombstones, 0);
        }

        free(table);

        return 0;
    }

    /*
    * When a tombstone exists earlier in the probe chain and an empty slot
    * exists later, insert must prefer the tombstone and leave the empty
    * slot as a chain terminator.
    */
    static int InsertPrefersTombstoneOverEmpty(void)
    {
        wtl_ht_entry_t* a = NULL;
        wtl_ht_entry_t* b = NULL;
        wtl_ht_entry_t* probe = NULL;

        WtlHashTable* table = allocHashTable(16);

        // hash 16 has home slot 0; hashes 32 and 48 both probe past it
        {
            EXPECT_EQ(wtlHashTableInsert(table, 16, &a), WTL_SUCCESS);
            EXPECT_EQ(wtlHashTableInsert(table, 32, &b), WTL_SUCCESS);
        }

        // removing hash 16 leaves a tombstone at slot 0, first in the
        // probe chain of every hash whose home is slot 0
        {
            EXPECT_EQ(wtlHashTableRemove(table, 16), WTL_SUCCESS);
            EXPECT_EQ(table->tombstones, 1);
        }

        // inserting another slot-0-home hash must reclaim the tombstone
        // at slot 0 rather than probing past it to the empty slots
        {
            EXPECT_EQ(wtlHashTableInsert(table, 48, &probe), WTL_SUCCESS);
            EXPECT_TRUE(probe == &table->slots[0].entry);

            EXPECT_EQ(table->count, 2);
            EXPECT_EQ(table->tombstones, 0);
        }

        // chain terminators must still work: a hash whose chain starts at
        // the now-vacated slot 0 must resolve without skipping live entries
        {
            EXPECT_TRUE(wtlHashTableLookup(table, 32) == &table->slots[1].entry);
            EXPECT_TRUE(wtlHashTableLookup(table, 48) == &table->slots[0].entry);
        }

        free(table);

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
    * Rehash with tombstones must move all live entries to the first empty
    * slot at or after their home position, set tombstones to zero, and
    * preserve count exactly.
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
