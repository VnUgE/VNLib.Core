/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: lookup.c
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
    wtl_ht_entry_t* entry = NULL, * entry2 = NULL;

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

static int RunLookupTests(void)
{
    RUN_TEST(LookupExisting());
    RUN_TEST(LookupMissingEmptyChain());
    RUN_TEST(LookupMissingTombstoneChain());
    RUN_TEST(LookupEmptyTable());

    return 0;
}

