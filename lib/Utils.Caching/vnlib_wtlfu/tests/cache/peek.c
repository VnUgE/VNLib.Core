/*
 * Copyright (c) 2026 Vaughn Nugent
 *
 * Library: VNLib
 * Package: vnlib_wtlfu
 * File: peek.c
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

static int PeekParameterValidation(void)
{
    WtlKey emptyKey = { NULL, 0 };
    WtlValue outVal;
    WtlCtx* cache = allocCache(NULL);

    // Ensure normal lookup without any keys is normal
    EXPECT_EQ(WtlPeek(cache, _dummyKeys[0], &outVal), WTL_ERR_NOT_FOUND);

    EXPECT_EQ(WtlPeek(NULL, emptyKey, &outVal), WTL_ERR_INVALID_ARG);
    EXPECT_EQ(WtlPeek(cache, emptyKey, &outVal), WTL_ERR_INVALID_ARG);
    
    // Peek allows null out pointer for quick checks
    EXPECT_EQ(WtlPeek(cache, _dummyKeys[0], NULL), WTL_ERR_NOT_FOUND);    

    // Valid key pointer but empty length
    emptyKey.key = "not null";
    emptyKey.len = 0;

    EXPECT_EQ(WtlPeek(cache, emptyKey, &outVal), WTL_ERR_INVALID_ARG);

    // Null key pointer with valid len, span checks should catch
    emptyKey.key = NULL;
    emptyKey.len = 5;
    EXPECT_EQ(WtlPeek(cache, emptyKey, &outVal), WTL_ERR_INVALID_ARG);

    // Peek with null out pointer on a HIT must still succeed
    {
        _addDummyValues(cache);
        EXPECT_EQ(WtlPeek(cache, _dummyKeys[0], NULL), WTL_SUCCESS);
        EXPECT_EQ(WtlCount(cache), DUMMY_VALUE_SIZE);
    }

    free(cache);
    return 0;
}

/*
* Confirms that peek assigns the outValue properties to the 
* discovered key. Ensures that when outValue is set the correct
* key and value are returned from the cache
*/
static int PeekReturnsDesiredValue(void)
{
    WtlValue outVal;
    WtlKey dummyKey = _dummyKeys[0];
    WtlCtx* cache = allocCache(NULL);	

    _addDummyValues(cache);	
    memset(&outVal, 0, sizeof(outVal));

    EXPECT_EQ(WtlPeek(cache, dummyKey, &outVal), WTL_SUCCESS);

    ENSURE(outVal.key);	//Fault guard
    EXPECT_TRUE(strcmp(outVal.key, dummyKey.key) == 0);
    EXPECT_TRUE(outVal.value == _dummyValues[0].value);

    free(cache);
    return 0;
}

/*
* Test that peek on a loaded table with a random key also
* returns NOT_FOUND
*/
static int PeekLoadedTableReturnsNotFound(void)
{
    WtlValue outVal;
    WtlCtx* cache = allocCache(NULL);
    WtlKey notFound = dummy_key("not found");

    // Add dummy vals
    _addDummyValues(cache);

    EXPECT_NE(WtlCount(cache), 0);

    EXPECT_EQ(WtlPeek(cache, notFound, &outVal), WTL_ERR_NOT_FOUND);
    EXPECT_EQ(WtlPeek(cache, notFound, NULL), WTL_ERR_NOT_FOUND);

    free(cache);
    return 0;
}

/*
* Confirms that calling peek on an empty cache store 
* returns NOT_FOUND 
*/
static int PeekEmptyTableReturnsNotFound(void)
{
    WtlValue outVal;
    WtlCtx* cache = allocCache(NULL);

    EXPECT_EQ(WtlCount(cache), 0);

    EXPECT_EQ(WtlPeek(cache, _dummyKeys[0], &outVal), WTL_ERR_NOT_FOUND);
    EXPECT_EQ(WtlPeek(cache, _dummyKeys[0], NULL), WTL_ERR_NOT_FOUND);

    free(cache);
    return 0;
}

/*
* Confirms that peek does not alter the cmsketch table 
* for it's down key
*/
static int PeekDoesNotIncrementsCounterForKey(void)
{
    cspan_t keySpan;
    uint32_t hashCode;
    WtlKey dummyKey = _dummyKeys[0];
    WtlCtx* cache = allocCache(NULL);

    // Get hashcode for dummy key
    spanInitC(&keySpan, dummyKey.key, dummyKey.len);
    hashCode = wtlHash32(keySpan, cache->config.keySeed);

    _addDummyValues(cache);

    // Estimate should be 1 on insert before get is called
    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, hashCode), 1);

    EXPECT_EQ(WtlPeek(cache, dummyKey, NULL), WTL_SUCCESS);

    // Estimate should have increased since get a full get
    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, hashCode), 1);

    free(cache);
    return 0;
}

/*
* Confirms that Peek does not change _any_ table 
* state. For now this is a quick check, we can know that
* currently peek does not modify any internals, lists, 
* hashtable or sketch. 
*/
static int PeekDoesNotAlterCache(void)
{	
    int32_t memSize = WtlGetMemorySize(&_defaultConfig);
    TASSERT(memSize > 0);

    // Alloc cache memory and ensure it succeed
    WtlCtx* cache = (WtlCtx*)malloc(memSize);
    WtlCtx* copy  = (WtlCtx*)malloc(memSize);
    TASSERT(cache && copy);

    // Init cache and assert success
    TASSERT(WtlInit(&_defaultConfig, cache) == WTL_SUCCESS);

    _addDummyValues(cache);
 
    // After setup, copy the entire table memory 
    memmove(copy, cache, memSize);

    for (int i = 0; i < DUMMY_VALUE_SIZE; i++)
    {
        ENSURE(WtlPeek(cache, _dummyKeys[i], NULL) == WTL_SUCCESS);
    }

    // Expect the table memory should be identical after move
    EXPECT_EQ(memcmp(cache, copy, memSize), 0);

    free(cache);
    free(copy);
    return 0;
}

/*
* Confirms that all independent keys find the correct values 
* with independent key structures. 
*/
static int PeekReliesOnKeyStrings(void)
{
    WtlCtx* cache = allocCache(NULL);

    WtlKey key1 = dummy_key("hello world");
    WtlKey key2 = dummy_key("hello world");

    WtlValue val = dummy_value("hello world");

    EXPECT_EQ(WtlInsert(cache, &val, NULL), WTL_SUCCESS);

    EXPECT_EQ(WtlPeek(cache, key1, NULL), WTL_SUCCESS);
    EXPECT_EQ(WtlPeek(cache, key2, NULL), WTL_SUCCESS);

    free(cache);
    return 0;
}

static int RunPeekTests(void)
{
    RUN_TEST(PeekParameterValidation());
    RUN_TEST(PeekReturnsDesiredValue());
    RUN_TEST(PeekLoadedTableReturnsNotFound());
    RUN_TEST(PeekEmptyTableReturnsNotFound());	
    RUN_TEST(PeekDoesNotIncrementsCounterForKey());
    RUN_TEST(PeekDoesNotAlterCache());

    return 0;
}
