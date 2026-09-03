/*
 * Copyright (c) 2026 Vaughn Nugent
 *
 * Library: VNLib
 * Package: vnlib_wtlfu
 * File: touch.c
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
* Confirms WtlTouch argument validation. Touch is a get with a
* discarded value, so it must reject the same bad arguments and alter
* no cache state.
*/
static int TouchParameterValidation(void)
{
    WtlCtx* cache = allocCache(NULL);

    // Null cache
    EXPECT_EQ(WtlTouch(NULL, _dummyKeys[0]), WTL_ERR_INVALID_ARG);

    // Null key pointer
    {
        WtlKey badKey = dummy_key("foo");
        badKey.key = NULL;
        EXPECT_EQ(WtlTouch(cache, badKey), WTL_ERR_INVALID_ARG);
    }

    // Non-null key pointer with empty length
    {
        WtlKey badKey = dummy_key("foo");
        badKey.key = (const uint8_t*)"not null";
        badKey.len = 0;
        EXPECT_EQ(WtlTouch(cache, badKey), WTL_ERR_INVALID_ARG);
    }

    // Null key pointer with empty length
    {
        WtlKey badKey = dummy_key("foo");
        badKey.key = NULL;
        badKey.len = 0;
        EXPECT_EQ(WtlTouch(cache, badKey), WTL_ERR_INVALID_ARG);
    }

    EXPECT_EQ(WtlCount(cache), 0);

    free(cache);
    return 0;
}

/*
* Confirms that touching a missing key reports not found and leaves
* the cache state untouched.
*/
static int TouchMissingKeyReturnsNotFound(void)
{
    WtlKey missingKey = dummy_key("no such key");
    const WtlEntry* windowHead = NULL;
    WtlCtx* cache = allocCache(NULL);  

    _addDummyValues(cache);

    windowHead = lruHeadGet(&cache->windowCache);
    ENSURE(windowHead); // fault guard

    EXPECT_EQ(WtlTouch(cache, missingKey), WTL_ERR_NOT_FOUND);

    // State is untouched: same count and same window head
    EXPECT_EQ(WtlCount(cache), DUMMY_VALUE_SIZE);
    EXPECT_TRUE(lruHeadGet(&cache->windowCache) == windowHead);

    free(cache);
    return 0;
}

/*
* Confirms that touching a window item moves it to the window head,
* the same recency behavior as WtlGet.
*/
static int TouchWindowHitMovesToWindowHead(void)
{
    // Window must be large enough that all dummies stay in the window
    WtlConfig cfg = _defaultConfig;
    cfg.capacity = 16;
    cfg.windowPct = 50;

    WtlCtx* cache = allocCache(&cfg);

    // Ensure window is not full
    ENSURE(cache->config.windowSize > DUMMY_VALUE_SIZE);

    _addDummyValues(cache);

    // LRU order after insert: tail is [0], head is [4]
    {
        const WtlEntry* head = lruHeadGet(&cache->windowCache);
        const WtlEntry* tail = lruPeek(&cache->windowCache);
        ENSURE(head && tail); // fault guard

        EXPECT_TRUE(head->value == _dummyValues[4].value);
        EXPECT_TRUE(tail->value == _dummyValues[0].value);
    }

    // Touch entry 1, it must move to the head
    EXPECT_EQ(WtlTouch(cache, _dummyKeys[1]), WTL_SUCCESS);

    {
        const WtlEntry* head = lruHeadGet(&cache->windowCache);
        const WtlEntry* tail = lruPeek(&cache->windowCache);
        ENSURE(head && tail); // fault guard

        EXPECT_TRUE(head->value == _dummyValues[1].value);
        EXPECT_TRUE(tail->value == _dummyValues[0].value);
    }

    // No segment membership or count changes
    EXPECT_EQ(lruCount(&cache->windowCache), DUMMY_VALUE_SIZE);
    EXPECT_EQ(lruCount(&cache->mainCache.probation), 0);
    EXPECT_EQ(lruCount(&cache->mainCache.protected), 0);

    free(cache);
    return 0;
}

/*
* Confirms that touching a probation item promotes it into the
* protected segment, the same behavior as WtlGet.
*/
static int TouchProbationHitPromotesToProtected(void)
{
    WtlKey testKey = _dummyKeys[0];
    WtlCtx* cache = allocCache(NULL);

    // Default window is 2, so d0..d2 overflow into probation
    EXPECT_EQ(cache->config.windowSize, 2);

    _addDummyValues(cache);
   
    // Get the entry pointer for comparison
    const WtlEntry* testEntry = findEntryByKey(cache, testKey);
    ENSURE(testEntry);

    // Should be sitting in probation
    EXPECT_EQ(testEntry->lruMember, WTL_LRU_MEMBER_PROBATION);

    // Touch d0, the lru probation entry. 
    EXPECT_EQ(WtlTouch(cache, testKey), WTL_SUCCESS);

    // Should have been promoted
    EXPECT_EQ(testEntry->lruMember, WTL_LRU_MEMBER_PROTECTED);

    // A fresh key can still be stored, the touch did not disturb the
    // admission path
    {       
        WtlValue probed = dummy_value("probe key");     

        EXPECT_EQ(WtlInsert(cache, &probed, NULL), WTL_SUCCESS);
        EXPECT_EQ(WtlCount(cache), DUMMY_VALUE_SIZE + 1);
    }

    free(cache);
    return 0;
}

/*
* Confirms that touching an item records a sketch hit, the same
* frequency tracking behavior as WtlGet.
*/
static int TouchRecordsSketchHit(void)
{
    WtlEntry* entry = NULL;
    WtlCtx* cache = allocCache(NULL);
    uint32_t sketchVal;

    _addDummyValues(cache);

    {
        // Get the last window item
        entry = lruPeek(&cache->windowCache);

        ENSURE(entry); // fault guard

        // Get current sketch on addition
        sketchVal = wtlSketchEstimate(&cache->sketch, entry->hash);
    }

    WtlKey touchKey = { .key = entry->key.data, .len = entry->key.size };
    EXPECT_EQ(WtlTouch(cache, touchKey), WTL_SUCCESS);

    // Entry should be head now
    EXPECT_FALSE(entry == lruPeek(&cache->windowCache));
    EXPECT_TRUE(entry == lruHeadGet(&cache->windowCache));

    // Sketch should have increased by 1
    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, entry->hash), sketchVal + 1);

    free(cache);
    return 0;
}

static int RunTouchTests(void)
{
    RUN_TEST(TouchParameterValidation());
    RUN_TEST(TouchMissingKeyReturnsNotFound());
    RUN_TEST(TouchWindowHitMovesToWindowHead());
    RUN_TEST(TouchProbationHitPromotesToProtected());
    RUN_TEST(TouchRecordsSketchHit());
    return 0;
}
