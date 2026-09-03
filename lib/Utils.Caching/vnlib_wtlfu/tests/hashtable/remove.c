/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: remove.c
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

static int RunRemoveTests(void)
{
    RUN_TEST(RemoveExisting());
    RUN_TEST(RemoveThenLookupReturnsNull());
    RUN_TEST(RemoveEntryMemoryNotInTableReturnsError());

    return 0;
}
