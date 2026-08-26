/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: tests/hashtable/insert.c
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

    // With no collisions, the entry must sit at its home slot
    EXPECT_TRUE(entry == &table->slots[65486 & 15].entry);

    free(table);

    return 0;
}

/*
* Inserting the same hash twice must return WTL_ERR_DUPLICATE on
* the second insert, and count must remain 1.
*/
static int InsertDuplicateHash(void)
{
    wtl_ht_entry_t* entry = NULL, * dup = NULL;

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
        EXPECT_EQ(wtlHashTableInsert(table, hash, &dup), WTL_ERR_DUPLICATE);
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
    wtl_ht_entry_t* a = NULL, * probe = NULL;

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

static int RunInsertTests(void)
{
    RUN_TEST(InsertSingleEntry());
    RUN_TEST(InsertDuplicateHash());
    RUN_TEST(InsertCollisionProbing());
    RUN_TEST(InsertUntilFull());
    RUN_TEST(InsertReusesTombstone());
    RUN_TEST(InsertPrefersTombstoneOverEmpty());

    return 0;
}
