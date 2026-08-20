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
    table->slots = (WtlHashSlot*)(table + 1);

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
    * Inserting entries until the table is full must return
    * WTL_TABLE_ERR_FULL on the insert that would exceed capacity.
    * Count must equal capacity.
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

        WtlHashTable* table = allocHashTable(16);
        const uint32_t hash = 65486;

        // insert, note the slot, then remove to create a tombstone
        {            
            wtl_ht_entry_t* entry = NULL;

            EXPECT_EQ(wtlHashTableInsert(table, hash, &entry), WTL_SUCCESS);
            EXPECT_TRUE(entry == &table->slots[hash & 15].entry);

            EXPECT_EQ(wtlHashTableRemove(table, entry), WTL_SUCCESS);
            EXPECT_EQ(table->count, 0);
            EXPECT_EQ(table->tombstones, 1);
        }

        // re-inserting the same hash must reclaim the tombstone slot, not
        // probe past it
        {
            wtl_ht_entry_t* reuse = NULL;

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
        wtl_ht_entry_t* probe = NULL;

        WtlHashTable* table = allocHashTable(16);

        // hash 16 has home slot 0; hashes 32 and 48 both probe past it
        {
            wtl_ht_entry_t* b;

            EXPECT_EQ(wtlHashTableInsert(table, 16, &a), WTL_SUCCESS);
            EXPECT_EQ(wtlHashTableInsert(table, 32, &b), WTL_SUCCESS);
        }

        // removing a leaves a tombstone at slot 0, first in the
        // probe chain of every hash whose home is slot 0
        {
            EXPECT_EQ(wtlHashTableRemove(table, a), WTL_SUCCESS);
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
        wtl_ht_entry_t* entry = NULL;

        WtlHashTable* table = allocHashTable(16);
        const uint32_t hash = 65486;

        EXPECT_EQ(wtlHashTableInsert(table, hash, &entry), WTL_SUCCESS);

        // lookup must return the exact reserved entry pointer
        {
            EXPECT_TRUE(wtlHashTableLookup(table, hash) == entry);
        }

        free(table);

        return 0;
    }

    /*
    * Lookup of a hash not in the table must return NULL when an empty slot
    * terminates the probe chain.
    */
    static int LookupMissingEmptyChain(void)
    {
        wtl_ht_entry_t* entry = NULL;

        WtlHashTable* table = allocHashTable(16);

        EXPECT_EQ(wtlHashTableInsert(table, 65486, &entry), WTL_SUCCESS);

        // 65502 also hashes to home slot 14, so it must probe past the
        // live entry and terminate at the empty slot, returning NULL
        {
            EXPECT_TRUE(wtlHashTableLookup(table, 65502) == NULL);
        }

        free(table);

        return 0;
    }

    /*
    * Lookup of a hash not in the table must return NULL when a tombstone is
    * present in the probe chain. Tombstones must not terminate the probe.
    */
    static int LookupMissingTombstoneChain(void)
    {
        wtl_ht_entry_t* entry = NULL;
        wtl_ht_entry_t* entry2 = NULL;

        WtlHashTable* table = allocHashTable(16);

        // hash 2 and hash 18 both hash to home slot 2, probing to
        // slots 2 and 3 respectively
        {
            EXPECT_EQ(wtlHashTableInsert(table, 2, &entry), WTL_SUCCESS);
            EXPECT_EQ(wtlHashTableInsert(table, 18, &entry2), WTL_SUCCESS);
        }

        // removing hash 2 leaves a tombstone at slot 2, first in the
        // probe chain of any hash with home slot 2
        {
            EXPECT_EQ(wtlHashTableRemove(table, entry), WTL_SUCCESS);
            EXPECT_EQ(table->tombstones, 1);

            entry = NULL;
        }

        // hash 34 also hashes to home slot 2. Its probe must skip the
        // tombstone at slot 2, skip the live entry at slot 3, and
        // terminate at the empty slot 4, returning NULL
        {
            EXPECT_TRUE(wtlHashTableLookup(table, 34) == NULL);
        }

        // the live entry past the tombstone must still be findable
        {
            EXPECT_TRUE(wtlHashTableLookup(table, 18) == entry2);
        }

        free(table);

        return 0;
    }

    /*
    * Lookup of any hash in an empty table must return NULL without
    * scanning the full table.
    */
    static int LookupEmptyTable(void)
    {
        WtlHashTable* table = allocHashTable(16);

        EXPECT_EQ(table->count, 0);

        // distinct home slots across the table
        {
            EXPECT_TRUE(wtlHashTableLookup(table, 16) == NULL);
            EXPECT_TRUE(wtlHashTableLookup(table, 32) == NULL);
            EXPECT_TRUE(wtlHashTableLookup(table, 1) == NULL);
        }

        free(table);

        return 0;
    }

#endif /* TEST_GROUP_HASHTABLE_LOOKUP */

#ifndef TEST_GROUP_HASHTABLE_REMOVE
#define TEST_GROUP_HASHTABLE_REMOVE 1

    /*
    * Remove of an existing entry by hash must return WTL_SUCCESS, decrement
    * count, increment tombstones, and set the slot to tombstone.
    */
    static int RemoveExisting(void)
    {
        wtl_ht_entry_t* entry = NULL;

        WtlHashTable* table = allocHashTable(16);
        const uint32_t hash = 65486;

        EXPECT_EQ(wtlHashTableInsert(table, hash, &entry), WTL_SUCCESS);

        // removal must succeed and move the slot to tombstone state
        {
            EXPECT_EQ(wtlHashTableRemove(table, entry), WTL_SUCCESS);
            EXPECT_EQ(table->count, 0);
            EXPECT_EQ(table->tombstones, 1);
            EXPECT_EQ(table->slots[hash & 15].hash, WTL_TABLE_STATUS_TOMB);
        }

        // removing the same address again must fail: the slot is a
        // tombstone, which the probe treats as not present
        {
            EXPECT_EQ(wtlHashTableRemove(table, entry), WTL_ERR_INVALID_ARG);
            EXPECT_EQ(table->count, 0);
            EXPECT_EQ(table->tombstones, 1);
        }

        free(table);

        return 0;
    }

    /*
    * After removing an entry, a subsequent lookup of the same hash must
    * return NULL (tombstone must not match as occupied).
    */
    static int RemoveThenLookupReturnsNull(void)
    {
        WtlHashTable* table = allocHashTable(16);
        const uint32_t hash = 65486;       

        // removal leaves the slot tombstoned
        {
            wtl_ht_entry_t* entry = NULL;

            EXPECT_EQ(wtlHashTableInsert(table, hash, &entry), WTL_SUCCESS);

            EXPECT_EQ(wtlHashTableRemove(table, entry), WTL_SUCCESS);
            EXPECT_EQ(table->tombstones, 1);
        }

        // the lookup must skip the tombstone and terminate at the next
        // empty slot, not match it
        {
            EXPECT_TRUE(wtlHashTableLookup(table, hash) == NULL);
        }

        free(table);

        return 0;
    }

    /*
     * Tests that the remove() function guards against entries with
     * memory addresses outside the internal table memory. It ensures that
     * the internal checks function to guard invalid addresses
     */
    static int RemoveEntryMemoryNotInTableReturnsError(void)
    {
        WtlHashTable* table = allocHashTable(16);

        wtl_ht_entry_t* a, * b;

        // Insert random entry to put some data in the table

        EXPECT_EQ(wtlHashTableInsert(table, 16, &a), WTL_SUCCESS);
        EXPECT_EQ(wtlHashTableInsert(table, 32, &b), WTL_SUCCESS);

        // Use address of automatic slot into remove should point outside the table
        // slot memory and remove should guard it at runtime
        {
            wtl_ht_entry_t autoEntry;

            EXPECT_EQ(wtlHashTableRemove(table, &autoEntry), WTL_ERR_INVALID_ARG);

            // Should still have two entries and no tombstones
            EXPECT_EQ(table->count, 2);
            EXPECT_EQ(table->tombstones, 0);
        }

        // Removing a and b should succeed
        EXPECT_EQ(wtlHashTableRemove(table, a), WTL_SUCCESS);
        EXPECT_EQ(wtlHashTableRemove(table, b), WTL_SUCCESS);

        free(table);

        return 0;
    }

   
#endif /* TEST_GROUP_HASHTABLE_REMOVE */

#ifndef TEST_GROUP_HASHTABLE_CLEAR
#define TEST_GROUP_HASHTABLE_CLEAR 1

    /*
    * Clear on a table with entries and tombstones must reset count and
    * tombstones to zero and leave all slots empty.
    */
    static int ClearResetsTable(void)
    {
        wtl_ht_entry_t* a = NULL;
        wtl_ht_entry_t* b = NULL;

        WtlHashTable* table = allocHashTable(16);

        // build a mixed state: two live entries and one tombstone
        {
            EXPECT_EQ(wtlHashTableInsert(table, 16, &a), WTL_SUCCESS);
            EXPECT_EQ(wtlHashTableInsert(table, 32, &b), WTL_SUCCESS);
            EXPECT_EQ(wtlHashTableRemove(table, a), WTL_SUCCESS);

            a = NULL;

            EXPECT_EQ(table->count, 1);
            EXPECT_EQ(table->tombstones, 1);
        }

        // clear must return the table to its freshly allocated state
        {
            wtlHashTableClear(table);

            EXPECT_EQ(table->count, 0);
            EXPECT_EQ(table->tombstones, 0);
        }

        // every slot must be empty again, capacity untouched
        {
            EXPECT_EQ(wtlHashTableIsValid(table), WTL_SUCCESS);
            EXPECT_EQ(table->capacity, 16);

            for (uint32_t i = 0; i < table->capacity; i++)
            {
                EXPECT_EQ_LOOP(table->slots[i].hash, WTL_TABLE_STATUS_EMPTY);
            }
        }

        free(table);

        return 0;
    }

    /*
    * Clear on an already-empty table must be a no-op.
    */
    static int ClearEmptyTable(void)
    {
        WtlHashTable* table = allocHashTable(16);

        wtlHashTableClear(table);
       
        EXPECT_EQ(wtlHashTableIsValid(table), WTL_SUCCESS);
        EXPECT_EQ(table->count, 0);
        EXPECT_EQ(table->tombstones, 0);
        EXPECT_EQ(table->capacity, 16);

        free(table);

        return 0;
    }

    /*
    * After clear, inserting entries must work as if the table were freshly
    * initialized.
    */
    static int ClearThenInsertWorks(void)
    {
        wtl_ht_entry_t* a = NULL;
        wtl_ht_entry_t* b = NULL;
        wtl_ht_entry_t* a2 = NULL;

        WtlHashTable* table = allocHashTable(16);

        // fill part of the table, then clear it
        {
            EXPECT_EQ(wtlHashTableInsert(table, 16, &a), WTL_SUCCESS);
            EXPECT_EQ(wtlHashTableInsert(table, 32, &b), WTL_SUCCESS);
            EXPECT_EQ(wtlHashTableRemove(table, a), WTL_SUCCESS);

            a = NULL;

            wtlHashTableClear(table);
        }

        // the cleared table must accept inserts at home slots, including
        // slots that were occupied before the clear
        {
            EXPECT_EQ(wtlHashTableInsert(table, 16, &a2), WTL_SUCCESS);
            EXPECT_TRUE(a2 == &table->slots[0].entry);
        }

        // and a duplicate check must still fire against the new occupants
        {
            EXPECT_EQ(wtlHashTableInsert(table, 16, &b), WTL_TABLE_ERR_DUPLICATE);
            EXPECT_EQ(table->count, 1);
        }

        free(table);

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
        wtl_ht_entry_t* a = NULL;
        wtl_ht_entry_t* b = NULL;

        WtlHashTable* table = allocHashTable(16);

        // build a displaced chain: hash 5 at home (slot 5), hash 21
        // displaced to slot 6 by the collision
        {
            EXPECT_EQ(wtlHashTableInsert(table, 5, &a), WTL_SUCCESS);
            EXPECT_EQ(wtlHashTableInsert(table, 21, &b), WTL_SUCCESS);

            EXPECT_TRUE(a == &table->slots[5].entry);
            EXPECT_TRUE(b == &table->slots[6].entry);
        }

        // no tombstones present, rehash must be a no-op
        {
            EXPECT_EQ(table->tombstones, 0);

            wtlHashTableRehash(table);

            EXPECT_EQ(table->count, 2);
            EXPECT_EQ(table->tombstones, 0);

            // layout must be untouched
            EXPECT_TRUE(a == &table->slots[5].entry);
            EXPECT_TRUE(b == &table->slots[6].entry);
            EXPECT_TRUE(wtlHashTableLookup(table, 5) == a);
            EXPECT_TRUE(wtlHashTableLookup(table, 21) == b);
        }

        free(table);

        return 0;
    }

    /*
    * Rehash with tombstones must move all live entries to the first empty
    * slot at or after their home position, set tombstones to zero, and
    * preserve count exactly.
    */
    static int RehashCompactsTombstones(void)
    {
        wtl_ht_entry_t* a = NULL;
        wtl_ht_entry_t* b = NULL;

        WtlHashTable* table = allocHashTable(16);

        // hash 5 lands at home (slot 5); hash 21 collides at home and is
        // displaced to slot 6
        {
            EXPECT_EQ(wtlHashTableInsert(table, 5, &a), WTL_SUCCESS);
            EXPECT_EQ(wtlHashTableInsert(table, 21, &b), WTL_SUCCESS);

            EXPECT_TRUE(a == &table->slots[5].entry);
            EXPECT_TRUE(b == &table->slots[6].entry);
        }

        // removing the home entry leaves a tombstone at slot 5, before the
        // displaced entry
        {
            EXPECT_EQ(wtlHashTableRemove(table, a), WTL_SUCCESS);
            EXPECT_EQ(table->count, 1);
            EXPECT_EQ(table->tombstones, 1);
        }

        // rehash must pull the displaced entry back to its home slot and
        // clear the tombstone marker. The entry physically moves, so the
        // original pointer b is stale; verify against the slot address.
        {
            wtlHashTableRehash(table);

            EXPECT_EQ(table->count, 1);
            EXPECT_EQ(table->tombstones, 0);

            EXPECT_EQ(table->slots[5].hash, 21);
            EXPECT_EQ(table->slots[6].hash, WTL_TABLE_STATUS_EMPTY);
            EXPECT_TRUE(wtlHashTableLookup(table, 21) == &table->slots[5].entry);
        }

        free(table);

        return 0;
    }

    /*
    * After heavy insert/remove churn creating many tombstones, rehash must
    * leave all live entries lookupable by their original hashes.
    */
    static int RehashAfterChurn(void)
    {
        wtl_ht_entry_t* a = NULL;
        wtl_ht_entry_t* b = NULL;
        wtl_ht_entry_t* c = NULL;
        wtl_ht_entry_t* d = NULL;

        WtlHashTable* table = allocHashTable(16);

        // build a shared probe chain at home slot 5, then churn it:
        // 5 -> slot 5, 21 -> slot 6, 37 -> slot 7
        {
            EXPECT_EQ(wtlHashTableInsert(table, 5, &a), WTL_SUCCESS);
            EXPECT_EQ(wtlHashTableInsert(table, 21, &b), WTL_SUCCESS);
            EXPECT_EQ(wtlHashTableInsert(table, 37, &c), WTL_SUCCESS);
        }

        // remove the head and tail, creating tombstones at slots 5 and 7
        {
            EXPECT_EQ(wtlHashTableRemove(table, a), WTL_SUCCESS);
            EXPECT_EQ(wtlHashTableRemove(table, c), WTL_SUCCESS);

            EXPECT_EQ(table->count, 1);
            EXPECT_EQ(table->tombstones, 2);
        }

        // a new insert into the chain must claim the first tombstone
        // (slot 5), leaving slot 7 tombstoned
        {
            EXPECT_EQ(wtlHashTableInsert(table, 53, &d), WTL_SUCCESS);

            EXPECT_EQ(table->count, 2);
            EXPECT_EQ(table->tombstones, 1);
            EXPECT_EQ(table->slots[5].hash, 53);
        }

        // rehash and verify every live entry is still lookupable and the
        // table holds exactly the live count with no tombstones
        {
            wtlHashTableRehash(table);

            EXPECT_EQ(table->count, 2);
            EXPECT_EQ(table->tombstones, 0);
            EXPECT_TRUE(wtlHashTableLookup(table, 21) != NULL);
            EXPECT_TRUE(wtlHashTableLookup(table, 53) != NULL);
            EXPECT_TRUE(wtlHashTableLookup(table, 5) == NULL);
            EXPECT_TRUE(wtlHashTableLookup(table, 37) == NULL);
        }

        free(table);

        return 0;
    }

    /*
    * Rehash must preserve the exact entry count, no more, no less.
    */
    static int RehashPreservesCount(void)
    {
        wtl_ht_entry_t* entry = NULL;

        WtlHashTable* table = allocHashTable(16);

        // build a table with several live entries and several tombstones
        {
            const uint32_t liveHashes[6] = { 1, 2, 3, 4, 5, 17 };

            for (int i = 0; i < 6; i++)
            {
                EXPECT_EQ_LOOP(wtlHashTableInsert(table, liveHashes[i], &entry), WTL_SUCCESS);
            }

            // remove every other entry to create tombstones
            {
                EXPECT_EQ(wtlHashTableRemove(table, wtlHashTableLookup(table, 1)), WTL_SUCCESS);
                EXPECT_EQ(wtlHashTableRemove(table, wtlHashTableLookup(table, 3)), WTL_SUCCESS);
                EXPECT_EQ(wtlHashTableRemove(table, wtlHashTableLookup(table, 5)), WTL_SUCCESS);
            }

            EXPECT_EQ(table->count, 3);
            EXPECT_EQ(table->tombstones, 3);
        }

        // rehash must not lose or duplicate any live entry: count stays
        // exact, and every surviving hash must remain lookupable
        {
            wtlHashTableRehash(table);

            EXPECT_EQ(table->count, 3);
            EXPECT_EQ(table->tombstones, 0);

            EXPECT_TRUE(wtlHashTableLookup(table, 2) != NULL);
            EXPECT_TRUE(wtlHashTableLookup(table, 4) != NULL);
            EXPECT_TRUE(wtlHashTableLookup(table, 17) != NULL);

            // removed hashes must not reappear
            EXPECT_TRUE(wtlHashTableLookup(table, 1) == NULL);
            EXPECT_TRUE(wtlHashTableLookup(table, 3) == NULL);
            EXPECT_TRUE(wtlHashTableLookup(table, 5) == NULL);
        }

        free(table);

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
        wtl_ht_entry_t* entries[4] = { NULL };
        wtl_ht_entry_t* tmp = NULL;

        WtlHashTable* table = allocHashTable(16);

        // empty table reports zero
        EXPECT_EQ(wtlHashTableCount(table), 0);

        // two inserts increase the count
        EXPECT_EQ(wtlHashTableInsert(table, 65486, &entries[0]), WTL_SUCCESS);
        EXPECT_EQ(wtlHashTableInsert(table, 12345, &entries[1]), WTL_SUCCESS);
        EXPECT_EQ(wtlHashTableCount(table), 2);

        // duplicate insert leaves the count unchanged
        EXPECT_EQ(wtlHashTableInsert(table, 65486, &tmp), WTL_TABLE_ERR_DUPLICATE);
        EXPECT_EQ(wtlHashTableCount(table), 2);

        // removing one entry decrements the count
        EXPECT_EQ(wtlHashTableRemove(table, entries[0]), WTL_SUCCESS);
        EXPECT_EQ(wtlHashTableCount(table), 1);

        // removing the second entry returns the count to zero
        EXPECT_EQ(wtlHashTableRemove(table, entries[1]), WTL_SUCCESS);
        EXPECT_EQ(wtlHashTableCount(table), 0);

        // re-insert after removing everything counts correctly
        EXPECT_EQ(wtlHashTableInsert(table, 999, &tmp), WTL_SUCCESS);
        EXPECT_EQ(wtlHashTableCount(table), 1);

        free(table);

        return 0;
    }

    /*
    * wtlfuHashTableCapacity must return the capacity set at init.
    */
    static int CapacityReturnsInitValue(void)
    {
        WtlHashTable* table16 = allocHashTable(16);
        WtlHashTable* table256 = allocHashTable(256);

        EXPECT_EQ(wtlHashTableCapacity(table16), 16);
        EXPECT_EQ(wtlHashTableCapacity(table256), 256);

        free(table16);
        free(table256);

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
        wtl_ht_entry_t* entries[16] = { NULL };

        WtlHashTable* table = allocHashTable(16);

        // Fill the table to exactly full. These hashes land on distinct
        // home slots, so every slot is occupied and count == capacity.
        const uint32_t fullHashes[16] = {
            3, 20, 37, 54, 71, 88, 105, 122,
            139, 156, 173, 190, 207, 224, 241, 258
        };

        for (uint32_t i = 0; i < 16; i++)
        {
            EXPECT_EQ_LOOP(wtlHashTableInsert(table, fullHashes[i], &entries[i]), WTL_SUCCESS);
        }

        EXPECT_EQ(table->count, 16);

        // No empty or tombstone slot exists, so a lookup of a missing
        // hash must exhaust a full wrap-around (capacity probes) and
        // terminate with NULL rather than looping forever.
        //
        // 17 & 15 == 1 which is occupied (hash 3), so the probe chain
        // walks all 16 slots and wraps back to its start before giving up.
        EXPECT_TRUE(wtlHashTableLookup(table, 17) == NULL);

        // An inserted entry must still be found through a full table.
        EXPECT_TRUE(wtlHashTableLookup(table, fullHashes[15]) != NULL);

        free(table);

        return 0;
    }

    /*
    * Fill the table, remove one entry, then insert a new entry. The insert
    * must succeed via tombstone reuse, and the new entry must be lookupable.
    */
    static int FullTableRemoveThenInsert(void)
    {
        wtl_ht_entry_t* entries[16] = { NULL };
        wtl_ht_entry_t* newEntry = NULL;

        WtlHashTable* table = allocHashTable(16);

        const uint32_t fullHashes[16] = {
            3, 20, 37, 54, 71, 88, 105, 122,
            139, 156, 173, 190, 207, 224, 241, 258
        };

        for (uint32_t i = 0; i < 16; i++)
        {
            EXPECT_EQ_LOOP(wtlHashTableInsert(table, fullHashes[i], &entries[i]), WTL_SUCCESS);
        }

        // Table is exactly full; nothing can be inserted
        EXPECT_EQ(wtlHashTableInsert(table, 9, &newEntry), WTL_TABLE_ERR_FULL);

        // Removing one entry frees a tombstone and makes room
        EXPECT_EQ(wtlHashTableRemove(table, entries[0]), WTL_SUCCESS);
        EXPECT_EQ(table->count, 15);
        EXPECT_EQ(table->tombstones, 1);

        // Insert must succeed via tombstone reuse
        const uint32_t insertHash = 9; // 9 & 15 == 9 -> occupied (hash 207)
        EXPECT_EQ(wtlHashTableInsert(table, insertHash, &newEntry), WTL_SUCCESS);
        EXPECT_EQ(table->count, 16);
        EXPECT_EQ(table->tombstones, 0);
        EXPECT_FALSE(newEntry == NULL);

        // New entry must be lookupable
        EXPECT_TRUE(wtlHashTableLookup(table, insertHash) != NULL);

        // The removed entry must no longer be found
        EXPECT_TRUE(wtlHashTableLookup(table, fullHashes[0]) == NULL);

        free(table);


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
    RUN_TEST(LookupEmptyTable());
#endif

#if TEST_GROUP_HASHTABLE_REMOVE
    RUN_TEST(RemoveExisting());
    RUN_TEST(RemoveThenLookupReturnsNull());
    RUN_TEST(RemoveEntryMemoryNotInTableReturnsError());
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
