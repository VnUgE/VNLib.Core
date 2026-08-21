/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: tests/hashtable/clear.c
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
* Clear on a table with entries and tombstones must reset count and
* tombstones to zero and leave all slots empty.
*/
static int ClearResetsTable(void)
{
    wtl_ht_entry_t* a = NULL, * b = NULL;

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
    wtl_ht_entry_t* a = NULL, * b = NULL, * a2 = NULL;

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

static int RunClearTests(void)
{
    RUN_TEST(ClearResetsTable());
    RUN_TEST(ClearEmptyTable());
    RUN_TEST(ClearThenInsertWorks());

    return 0;
}
