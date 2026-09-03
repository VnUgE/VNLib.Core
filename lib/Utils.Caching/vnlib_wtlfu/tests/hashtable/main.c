/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: main.c
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

static WtlHashTable* allocHashTableRaw(uint32_t capacity)
{
    uint32_t memSize = sizeof(WtlHashTable) + (sizeof(WtlHashSlot) * capacity);

    WtlHashTable* table = malloc(memSize);
    TASSERT(table);

    memset(table, 0, memSize);

    // Assign capacity
    table->capacity = capacity;
    table->slots = (WtlHashSlot*)(table + 1);

    return table;
}

static WtlHashTable* allocHashTable(uint32_t capacity)
{
    WtlHashTable* table = allocHashTableRaw(capacity);

    TASSERT(wtlHashTableIsValid(table) == WTL_SUCCESS);

    return table;
}

#include "validation.c"
#include "insert.c"
#include "lookup.c"
#include "remove.c"
#include "clear.c"
#include "rehash.c"

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
    EXPECT_EQ(wtlHashTableInsert(table, 65486, &tmp), WTL_ERR_DUPLICATE);
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
    WtlHashTable* table16 = allocHashTable(16), * table256 = allocHashTable(256);

    EXPECT_EQ(wtlHashTableCapacity(table16), 16);
    EXPECT_EQ(wtlHashTableCapacity(table256), 256);

    free(table16);
    free(table256);

    return 0;
}


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
    // 17 & 15 == 1 which is occupied (hash 20), so the probe chain
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
    const uint32_t insertHash = 9; // 9 & 15 == 9 -> occupied (hash 105)
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

static int RunAccessorTests(void)
{
    RUN_TEST(CountReflectsOperations());
    RUN_TEST(CapacityReturnsInitValue());

    return 0;
}

static int RunEdgeTests(void)
{
    RUN_TEST(FullTableLookupNoInfiniteLoop());
    RUN_TEST(FullTableRemoveThenInsert());

    return 0;
}

int RunTests(void)
{
    /* Validation */
    TEST_GROUP(RunValidationTests());

    /* Insert */
    TEST_GROUP(RunInsertTests());

    /* Lookup */
    TEST_GROUP(RunLookupTests());

    /* Remove */
    TEST_GROUP(RunRemoveTests());

    /* Clear */
    TEST_GROUP(RunClearTests());

    /* Rehash */
    TEST_GROUP(RunRehashTests());

    /* Accessors */
    TEST_GROUP(RunAccessorTests());

    /* Edge cases */
    TEST_GROUP(RunEdgeTests());

    return 0;
}
