/*
 * Copyright (c) 2026 Vaughn Nugent
 *
 * Library: VNLib
 * Package: vnlib_wtlfu
 * File: iterate.c
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

static int MoveNextValidation(void)
{
    WtlValue value;
    WtlIterator* state = NULL;
    WtlCtx* cache = allocCache(NULL);

    EXPECT_EQ(WtlIterateNextValue(NULL, &state, NULL), WTL_ERR_INVALID_ARG);
    EXPECT_EQ(WtlIterateNextValue(cache, NULL, NULL), WTL_ERR_INVALID_ARG);

    // Should start, then move directly to end
    EXPECT_EQ(WtlIterateNextValue(cache, &state, &value), WTL_ERR_ITR_END);

    // Set to a random, but valid address outside the cache table
    state = (WtlIterator*)(&value);

    // Should be invalid
    EXPECT_EQ(WtlIterateNextValue(cache, &state, NULL), WTL_ERR_INVALID_ARG);

    free(cache);
    return 0;
}

static int MoveNextFindsAllValuesOnlyOnce(void)
{
    WtlValue outVals[DUMMY_VALUE_SIZE];
    int32_t itrRes = 0, itemCount = 0;
    WtlIterator* state = NULL;
    WtlCtx* cache = allocCache(NULL);

    _addDummyValues(cache);

    while (1)
    {
        if (itemCount == DUMMY_VALUE_SIZE)
        {
            // All values found, next call must report end
            itrRes = WtlIterateNextValue(cache, &state, NULL);
            EXPECT_EQ(itrRes, WTL_ERR_ITR_END);
            break;
        }

        itrRes = WtlIterateNextValue(cache, &state, &outVals[itemCount]);
        if (itrRes == WTL_ERR_ITR_END)
        {
            break;
        }

        // Otherwise expect success
        EXPECT_EQ(itrRes, WTL_SUCCESS);
        itemCount++;
    }

    // Expect to find the exact number of values added, were iterated
    EXPECT_EQ(itemCount, DUMMY_VALUE_SIZE);

    // Ensure each dummy was returned exactly once (1:1)
    for (int d = 0; d < DUMMY_VALUE_SIZE; d++)
    {
        int hits = 0;
        for (int i = 0; i < itemCount; i++)
        {
            if (
                outVals[i].keyLen == _dummyValues[d].keyLen &&
                memcmp(outVals[i].key, _dummyValues[d].key, outVals[i].keyLen) == 0
                )
            {
                hits++;
            }
        }
        EXPECT_EQ(hits, 1);
    }

    free(cache);
    return 0;
}

/*
* Checks that removing an item the iterator has already returned does not
* corrupt the remaining iteration. The removed item is never re-reported,
* and all other items are still found.
*/
static int MoveNextAllowsRemoveIteratedItem(void)
{
    int32_t itrRes = 0, remainderCount = 0;
    WtlValue removed;
    WtlIterator* state = NULL;
    WtlCtx* cache = allocCache(NULL);

    _addDummyValues(cache);

    // Iterate the first item
    EXPECT_EQ(WtlIterateNextValue(cache, &state, &removed), WTL_SUCCESS);  

    // Remove the item we just iterated by value
    EXPECT_EQ(WtlRemoveValue(cache, &removed), WTL_SUCCESS);

    // Continue iteration, removed item must never be re-reported
    while (1)
    {
        WtlValue next;
        itrRes = WtlIterateNextValue(cache, &state, &next);
        if (itrRes == WTL_ERR_ITR_END)
        {
            // End reached, all remaining values were reported
            break;
        }

        EXPECT_EQ(itrRes, WTL_SUCCESS);

        // Removed key must not be returned again
        EXPECT_NE(memcmp(next.key, removed.key, next.keyLen), 0);
        
        remainderCount++;
    }

    // All other items still found exactly once
    EXPECT_EQ(remainderCount, DUMMY_VALUE_SIZE - 1);

    free(cache);
    return 0;
}

static int MoveNextHitsEndOnEmptyTable(void)
{
    WtlValue value;
    WtlIterator* state = NULL;
    WtlCtx* cache = allocCache(NULL);

    EXPECT_EQ(WtlIterateNextValue(cache, &state, &value), WTL_ERR_ITR_END);

    free(cache);
    return 0;
}

static int MoveNextStopsOnEnd(void)
{
    int32_t result;
    WtlValue value;
    WtlIterator* state = NULL;
    WtlCtx* cache = allocCache(NULL);

    _addDummyValues(cache);

    EXPECT_EQ(WtlIterateNextValue(cache, &state, &value), WTL_SUCCESS);

    do
    {
        result = WtlIterateNextValue(cache, &state, NULL);

    } while (result == WTL_SUCCESS);

    // Should have found end
    EXPECT_EQ(result, WTL_ERR_ITR_END);

    // All other calls should return end of list
    EXPECT_EQ(WtlIterateNextValue(cache, &state, &value), WTL_ERR_ITR_END);
    EXPECT_EQ(WtlIterateNextValue(cache, &state, &value), WTL_ERR_ITR_END);

    free(cache);
    return 0;
}

/*
* Checks that clearing the state pointer resets the iterator 
* back to the start of the table.
*/
static int MoveNextStateResetRestartsIteration(void)
{
    WtlValue value, firstVal;
    WtlIterator* state = NULL;
    WtlCtx* cache = allocCache(NULL);

    _addDummyValues(cache);

    // Move a couple times
    EXPECT_EQ(WtlIterateNextValue(cache, &state, &firstVal), WTL_SUCCESS);
    EXPECT_EQ(WtlIterateNextValue(cache, &state, &value), WTL_SUCCESS);

    // Should have moved to next value
    EXPECT_TRUE(value.value != firstVal.value);

    // Clear state, and should reset the iteration
    state = NULL;
    EXPECT_EQ(WtlIterateNextValue(cache, &state, &value), WTL_SUCCESS);
    
    // Both iterations should return the same value
    EXPECT_TRUE(value.value == firstVal.value);
    
    free(cache);
    return 0;
}


/*
* Confirms the iterator skips tombstone slots: a removed key leaves a
* tombstone in the table walk, and the iterator must not report it.
* The removal is verified to have produced exactly one tombstone slot
* before the walk, so the skip path is exercised, not just bypassed.
*/
static int MoveNextSkipsTombstoneSlots(void)
{
    int32_t res, tombstoneCount = 0;
    WtlIterator* state = NULL;
    WtlValue value;
    WtlCtx* cache = allocCache(NULL);

    _addDummyValues(cache);

    // Remove one key to leave a tombstone slot in the table
    EXPECT_EQ(WtlRemove(cache, _dummyKeys[1]), WTL_SUCCESS);
    EXPECT_EQ(WtlCount(cache), DUMMY_VALUE_SIZE - 1);

    // Verify exactly one tombstone slot exists so the skip path is
    // exercised

    for (uint32_t i = 0; i < cache->table.capacity; i++)
    {
        const WtlHashSlot* slot = &cache->table.slots[i];
        if (slot->hash == WTL_TABLE_STATUS_TOMB)
        {
            tombstoneCount++;
        }
    }

    EXPECT_EQ(tombstoneCount, 1);

    // Full walk must report exactly the surviving values, and the
    // removed value is never reported
    int32_t foundCount = 0, foundRemoved = 0;

    while ((res = WtlIterateNextValue(cache, &state, &value)) == WTL_SUCCESS)
    {
        ENSURE(value.value != _dummyValues[1].value); // removed value never reported
        foundCount++;
    }

    EXPECT_EQ(res, WTL_ERR_ITR_END);
    EXPECT_EQ(foundCount, DUMMY_VALUE_SIZE - 1);

    free(cache);
    return 0;
}

static int RunIteratorTests(void)
{
    RUN_TEST(MoveNextValidation());
    RUN_TEST(MoveNextHitsEndOnEmptyTable());
    RUN_TEST(MoveNextStopsOnEnd());
    RUN_TEST(MoveNextAllowsRemoveIteratedItem());
    RUN_TEST(MoveNextStateResetRestartsIteration());
    RUN_TEST(MoveNextFindsAllValuesOnlyOnce());
    RUN_TEST(MoveNextSkipsTombstoneSlots());

    return 0;
}
