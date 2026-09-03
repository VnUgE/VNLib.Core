/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: rehash.c
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
* Rehash with no tombstones must be a no-op; count and slot layout
* must remain unchanged.
*/
static int RehashNoTombstones(void)
{
    wtl_ht_entry_t* a = NULL, * b = NULL;

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
    wtl_ht_entry_t* a = NULL, * b = NULL;

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
    wtl_ht_entry_t* a = NULL, * b = NULL, * c = NULL, * d = NULL;

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

static int RunRehashTests(void)
{
    RUN_TEST(RehashNoTombstones());
    RUN_TEST(RehashCompactsTombstones());
    RUN_TEST(RehashAfterChurn());
    RUN_TEST(RehashPreservesCount());

    return 0;
}
