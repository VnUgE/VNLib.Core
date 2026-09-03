/*
 * Copyright (c) 2026 Vaughn Nugent
 *
 * Library: VNLib
 * Package: vnlib_wtlfu
 * File: get.c
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
* Confirms that WtlGet rejects bad arguments such as null
* pointers and invalid properties.
*/
static int GetParameterValidation(void)
{
    WtlKey emptyKey = { NULL, 0 };
    WtlValue outVal;
    WtlCtx* cache = allocCache(NULL);

    // Null cache
    EXPECT_EQ(WtlGet(NULL, _dummyKeys[0], &outVal), WTL_ERR_INVALID_ARG);

    // Null out value is not allowed on get
    EXPECT_EQ(WtlGet(cache, _dummyKeys[0], NULL), WTL_ERR_INVALID_ARG);

    // Null key pointer with valid len, span checks should catch
    {
        WtlKey badKey = _dummyKeys[0];
        badKey.key = NULL;
        EXPECT_EQ(WtlGet(cache, badKey, NULL), WTL_ERR_INVALID_ARG);
    }

    // Valid key pointer but empty length
    emptyKey.key = "not null";
    emptyKey.len = 0;
    EXPECT_EQ(WtlGet(cache, emptyKey, NULL), WTL_ERR_INVALID_ARG);

    // Null key pointer with empty length
    emptyKey.key = NULL;
    emptyKey.len = 0;
    EXPECT_EQ(WtlGet(cache, emptyKey, NULL), WTL_ERR_INVALID_ARG);

    free(cache);
    return 0;
}

/*
* Confirms that a stored value can be read back by its key.
* Inserts the global dummy values then reads them all back 
* by their corresponding keys and confirms the table returns all
* matching fields. Currently just does a pointer check.
*/
static int GetBasic(void)
{	
    WtlCtx* cache = allocCache(&_defaultConfig);

    _addDummyValues(cache);

    // Read back all dummy values from matching dummy keys
    for (int i = 0; i < DUMMY_VALUE_SIZE; i++)
    {
        WtlValue get;
        const WtlValue getVal = _dummyValues[i];

        memset(&get, 0, sizeof(WtlValue));

        EXPECT_EQ(WtlGet(cache, _dummyKeys[i], &get), WTL_SUCCESS);

        // Ensure value properties are equal to the originally stored value
        EXPECT_TRUE(get.keyLen == getVal.keyLen);
        EXPECT_EQ(memcmp(get.key, getVal.key, getVal.keyLen), 0);      
        EXPECT_TRUE(get.value == getVal.value);
    }

    free(cache);
    return 0;
}

/*
* Confirms that WtlGet returns NOT_FOUND for a key that was never
* inserted, and leaves the out value empty.
*/
static int GetNotFound(void)
{	
    WtlValue get;
    WtlCtx* cache = allocCache(&_defaultConfig);

    memset(&get, 0, sizeof(WtlValue));

    _addDummyValues(cache);

    // Dummy key does not exist	
    const WtlKey key = dummy_key("not_found");

    EXPECT_EQ(WtlGet(cache, key, &get), WTL_ERR_NOT_FOUND);

    // Ensure value properties null after failed get
    EXPECT_TRUE(get.key == NULL);
    EXPECT_TRUE(get.keyLen == 0);
    EXPECT_TRUE(get.value == NULL);

    free(cache);
    return 0;
}

/*
* Confirms that a successful get is recorded in the sketch.
* The estimate for a fresh key is 1 after insert, and must be 
* incremented by one after get().
*/
static int GetIncrementsCounterForKey(void)
{
    WtlValue outVal;
    uint32_t hashCode;
    WtlKey dummyKey = _dummyKeys[0];
    WtlCtx* cache = allocCache(&_defaultConfig);

    // Get hashcode for dummy key 
    hashCode = getKeyHashCode(cache, dummyKey);

    _addDummyValues(cache);

    // Estimate should be 1 on insert before get is called
    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, hashCode), 1);

    EXPECT_EQ(WtlGet(cache, _dummyKeys[0], &outVal), WTL_SUCCESS);

    // Estimate should have increased since get a full get
    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, hashCode), 2);

    free(cache);
    return 0;
}

/*
* Confirms that a miss does not record a sketch hit or modify the
* sketch table in any way.
*/
static int GetNotFoundDoesNotRecordSketch(void)
{  
    WtlValue get;
    WtlCtx* cache = allocCache(&_defaultConfig);
    cspan_t sketchMem = spanToC(cache->sketch.table);
    uint8_t* sketchCompBuf = (uint8_t*)malloc(spanGetSizeC(sketchMem));

    memset(&get, 0, sizeof(WtlValue));

    _addDummyValues(cache);

    // Snapshot sketch table after dummy pre-load
    ENSURE(sketchCompBuf);
    memmove(sketchCompBuf, spanGetOffsetC(sketchMem, 0), spanGetSizeC(sketchMem));

    // A miss must not record a hit or alter the sketch table
    WtlKey key = dummy_key("not_found");
    EXPECT_EQ(WtlGet(cache, key, &get), WTL_ERR_NOT_FOUND);

    EXPECT_EQ(memcmp(sketchCompBuf, spanGetOffsetC(sketchMem, 0), spanGetSizeC(sketchMem)), 0);

    free(sketchCompBuf);
    free(cache);
    return 0;
}

/*
* Confirms that getting a window entry moves it to the window head
* without changing segment membership or counts.
*/
static int GetWindowHitMovesToWindowHead(void)
{
    // Window must be large enough that all dummies stay in the window
    WtlConfig cfg = _defaultConfig;
    cfg.capacity = 16;
    cfg.windowPct = 50;

    WtlValue get;
    WtlCtx* cache = allocCache(&cfg);

    // Ensure window is not full
    ENSURE(cache->config.windowSize > DUMMY_VALUE_SIZE);

    _addDummyValues(cache);
   
    EXPECT_EQ(lruCount(&cache->windowCache), DUMMY_VALUE_SIZE);
    EXPECT_EQ(lruCount(&cache->mainCache.probation), 0);

    const WtlEntry* testEntry = findEntryByKey(cache, _dummyKeys[0]);

    // dummy[0] should be the current tail
    EXPECT_TRUE(lruTailGet(&cache->windowCache) == testEntry);

    // Get dummy[0] and move to head
    EXPECT_EQ(WtlGet(cache, _dummyKeys[0], &get), WTL_SUCCESS);

    // Should be moved to head now
    EXPECT_TRUE(lruHeadGet(&cache->windowCache) == testEntry);   

    free(cache);
    return 0;
}

/*
* Confirms that getting a probation entry promotes it to the protected
* segment head.
*/
static int GetPromotesProbationEntryToProtected(void)
{   
    WtlValue get;
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

    // Get dummy[0], the lru probation entry. 
    EXPECT_EQ(WtlGet(cache, testKey, &get), WTL_SUCCESS);

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
* Confirms that promoting a probation entry into a full protected
* segment demotes the lru protected entry back to probation.
*/
static int GetDemotesLruProtectedWhenFull(void)
{
    WtlValue get;   
  
    WtlConfig cfg = _defaultConfig;
    cfg.capacity = 20;
    cfg.windowPct = 25;
    cfg.protectedPct = 25;

    WtlCtx* cache = allocCache(&cfg);

    // Window 5, protected 3, probation 12
    EXPECT_EQ(cache->config.windowSize, 5);
    EXPECT_EQ(cache->config.protectedSize, 3);
    EXPECT_EQ(cache->config.probationSize, 12); 

    // Overflow the window, d0..d3 sit in the probation tail
    for (int i = 0; i < 9; i++)
    {
        EXPECT_EQ(WtlInsert(cache, &_dummy32[i], NULL), WTL_SUCCESS);
    }

    EXPECT_EQ(lruCount(&cache->mainCache.protected), 0);

    // Get on 0..3 should promote to protected segment
    for (int i = 0; i < 3; i++)
    {
        WtlKey key = { .key = _dummy32[i].key, .len = _dummy32[i].keyLen };
        EXPECT_EQ(WtlGet(cache, key, &get), WTL_SUCCESS);
    }

    // dummy 0..2 are protected, calling get on dummy[3] should promote
    // d[3] and demote d[0]
    {
        WtlKey dummy3key = { .key = _dummy32[3].key, .len = _dummy32[3].keyLen };

        const WtlEntry* dummy0 = findEntryByValue(cache, &_dummy32[0]);
        const WtlEntry* dummy3 = findEntryByKey(cache, dummy3key);
        ENSURE(dummy0 && dummy3);

        EXPECT_EQ(dummy3->lruMember, WTL_LRU_MEMBER_PROBATION);
        
        EXPECT_EQ(WtlGet(cache, dummy3key, &get), WTL_SUCCESS);

        EXPECT_EQ(dummy0->lruMember, WTL_LRU_MEMBER_PROBATION);
        EXPECT_EQ(dummy3->lruMember, WTL_LRU_MEMBER_PROTECTED);
    }

    // No eviction, everything is still stored
    EXPECT_EQ(WtlCount(cache), 9);

    free(cache);
    return 0;
}

static int RunGetTests(void)
{
    RUN_TEST(GetParameterValidation());
    RUN_TEST(GetBasic());
    RUN_TEST(GetNotFound());
    RUN_TEST(GetIncrementsCounterForKey());
    RUN_TEST(GetNotFoundDoesNotRecordSketch());
    RUN_TEST(GetWindowHitMovesToWindowHead());
    RUN_TEST(GetPromotesProbationEntryToProtected());
    RUN_TEST(GetDemotesLruProtectedWhenFull());

    return 0;
}
