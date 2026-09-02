
// insert.c

static int InsertParameterValidation(void)
{
    WtlValue outVal;
    WtlCtx* cache = allocCache(NULL);

    // Ensure normal lookup without any keys is normal
    EXPECT_EQ(WtlPeek(cache, _dummyKeys[0], &outVal), WTL_ERR_NOT_FOUND);

    // Null cache
    {
        WtlValue val = dummy_value("hello world 1");
        EXPECT_EQ(WtlInsert(NULL, &val, &outVal), WTL_ERR_INVALID_ARG);
        EXPECT_EQ(WtlInsert(NULL, &val, NULL), WTL_ERR_INVALID_ARG);
    }

    // Null value
    EXPECT_EQ(WtlInsert(cache, NULL, &outVal), WTL_ERR_INVALID_ARG);
    EXPECT_EQ(WtlInsert(cache, NULL, NULL), WTL_ERR_INVALID_ARG);

    // Null key pointer with valid len, span checks should catch
    {
        WtlValue badVal = dummy_value("hello world 1");
        badVal.key = NULL;
        EXPECT_EQ(WtlInsert(cache, &badVal, &outVal), WTL_ERR_INVALID_ARG);
    }

    // Valid key pointer but empty length
    {
        WtlValue badVal = dummy_value("hello world 1");
        badVal.key = (const uint8_t*)"not null";
        badVal.keyLen = 0;
        EXPECT_EQ(WtlInsert(cache, &badVal, &outVal), WTL_ERR_INVALID_ARG);
    }

    // Null key pointer with empty length
    {
        WtlValue badVal = dummy_value("hello world 1");
        badVal.key = NULL;
        badVal.keyLen = 0;
        EXPECT_EQ(WtlInsert(cache, &badVal, &outVal), WTL_ERR_INVALID_ARG);
    }

    // Nothing should have been inserted
    EXPECT_EQ(WtlCount(cache), 0);

    free(cache);
    return 0;
}

static int InsertBasic(void)
{
    WtlCtx* cache = allocCache(NULL);

    WtlValue val1 = dummy_value("hello world 1");
    WtlValue val2 = dummy_value("hello world 2");

    EXPECT_EQ(WtlInsert(cache, &val1, NULL), WTL_SUCCESS);
    EXPECT_EQ(WtlInsert(cache, &val2, NULL), WTL_SUCCESS);

    // Cound should reflect
    EXPECT_EQ(WtlCount(cache), 2);

    // Hash table count should be the same as public count
    EXPECT_EQ(wtlHashTableCount(&cache->table), 2);

    free(cache);
    return 0;
}

static int InsertInsertsIntoWindowFirst(void)
{
    WtlCtx* cache = allocCache(NULL);

    WtlValue val1 = dummy_value("hello world 1");
    WtlValue val2 = dummy_value("hello world 2");

    EXPECT_EQ(WtlInsert(cache, &val1, NULL), WTL_SUCCESS);
    EXPECT_EQ(WtlInsert(cache, &val2, NULL), WTL_SUCCESS);

    EXPECT_EQ(lruCount(&cache->windowCache), 2);
    EXPECT_EQ(lruCount(&cache->mainCache.probation), 0);
    EXPECT_EQ(lruCount(&cache->mainCache.protected), 0);

    free(cache);
    return 0;
}

static int InsertFailsWithWillEvictWhenNull(void)
{
    WtlCtx* cache = allocCache(NULL);
    uint32_t evictsAt = cache->config.probationSize + cache->config.windowSize;

    // Eviction should be less than our 32 dummy array
    ENSURE(evictsAt < 32);
    for (int i = 0; i < evictsAt; i++)
    {
        EXPECT_EQ(WtlInsert(cache, &_dummy32[i], NULL), WTL_SUCCESS);
    }

    // At the wall, both segments must be exactly full
    EXPECT_EQ(lruCount(&cache->windowCache), cache->config.windowSize);
    EXPECT_EQ(lruCount(&cache->mainCache.probation), cache->config.probationSize);

    // Exect an eviction with one more insertion
    EXPECT_EQ(WtlInsert(cache, &_dummyValues[0], NULL), WTL_ERR_WILL_EVICT);

    free(cache);
    return 0;
}

static int InsertAlwaysInsertsIntoWindowHead(void)
{
    WtlValue evicted;
    WtlCtx* cache = allocCache(NULL);

    for (int i = 0; i < 32; i++)
    {
        const WtlValue* val = &_dummy32[i];

        /*
        * We ignore evicted for this test, let it smash values. We just
        * care that no matter the state of the internals, the fresh insert
        * is pushed to the head of the window list
        */
        int32_t ret = WtlInsert(cache, val, &evicted);
        if (ret < 0) printf("	ERROR %d returned from insert with key %s\n\n", ret, (const char*)val->key);

        ENSURE(ret >= 0);

        // Values stored should match
        ENSURE(lruHeadGet(&cache->windowCache));	// fault guard
        EXPECT_TRUE(lruHeadGet(&cache->windowCache)->value == val->value);
    }

    free(cache);
    return 0;
}

static int InsertMovesLruItemToProbation(void)
{
    WtlCtx* cache = allocCache(NULL);

    WtlValue val1 = dummy_value("hello world 1");
    WtlValue val2 = dummy_value("hello world 2");
    WtlValue val3 = dummy_value("overflow");

    // Make sure our default config window size is 2 items
    ENSURE(cache->config.windowSize == 2);

    // First two succeed
    EXPECT_EQ(WtlInsert(cache, &val1, NULL), WTL_SUCCESS);
    EXPECT_EQ(WtlInsert(cache, &val2, NULL), WTL_SUCCESS);

    // 3rd causes probation overflow
    EXPECT_EQ(WtlInsert(cache, &val3, NULL), WTL_SUCCESS);

    //Probee head should be val1. Should be fine to compare value pointers
    EXPECT_TRUE(lruHeadGet(&cache->mainCache.probation)->value == val1.value);

    free(cache);
    return 0;
}

static int InsertDucpliateKeysReturnsError(void)
{
    WtlCtx* cache = allocCache(NULL);

    // Just a dummy array of duplicate keys
    WtlValue duplicates[4] = {
        dummy_value("hello world 1"),
        dummy_value("hello world 1"),
        dummy_value("hello world 1"),
        dummy_value("hello world 1"),
    };

    // Slot to be inserted 
    WtlValue val1 = dummy_value("hello world 1");

    EXPECT_EQ(WtlInsert(cache, &val1, NULL), WTL_SUCCESS);

    // All duplicate entries should fail
    for (int i = 0; i < 4; i++)
    {
        EXPECT_EQ(WtlInsert(cache, &duplicates[i], NULL), WTL_ERR_DUPLICATE);
    }

    // Only one value inserted
    EXPECT_EQ(WtlCount(cache), 1);

    free(cache);
    return 0;
}

/*
* Confirms that a duplicate insert is a complete no-op. The stored value,
* sketch frequency, and LRU position are all unchanged.
*/
static int InsertDuplicateDoesNotAlterState(void)
{
    WtlConfig conf = _defaultConfig;

    int32_t memSize = WtlGetMemorySize(&conf);
    TASSERT(memSize > 0);

    // Alloc cache memory and ensure it succeed
    WtlCtx* cache = (WtlCtx*)malloc(memSize);
    TASSERT(cache);

    // Init cache and assert success
    TASSERT(WtlInit(&conf, cache) == WTL_SUCCESS);

    // Copy buffer of cache
    void* clone = malloc(memSize);
    memmove(clone, cache, memSize);

    EXPECT_EQ(WtlInsert(cache, &_dummyValues[0], NULL), WTL_SUCCESS);

    // Memory should have changed for this update
    EXPECT_NE(memcmp(clone, cache, memSize), 0);

    // Snapshot again
    memmove(clone, cache, memSize);

    // Fail duplicate insert
    EXPECT_EQ(WtlInsert(cache, &_dummyValues[0], NULL), WTL_ERR_DUPLICATE);

    // No change in store memory
    EXPECT_EQ(memcmp(clone, cache, memSize), 0);

    free(clone);
    free(cache);
    return 0;
}

/*
* Tests that insert properly evicts an item after pushing window + probation
* unique items into cache. Entries should never touch protected cache.
*/
static int InsertEvictsWhenProbationFull(void)
{
    uint32_t maxBeforeEvict;
    WtlValue evicted;
    WtlCtx* cache = allocCache(NULL);

    // Ensure probation size is less our dummy array
    maxBeforeEvict = cache->config.probationSize + cache->config.windowSize;
    ENSURE(maxBeforeEvict < 32);

    // Add probation - 1 entries  
    for (int i = 0; i < maxBeforeEvict; i++)
    {
        EXPECT_EQ(WtlInsert(cache, &_dummy32[i], NULL), WTL_SUCCESS);
    }

    EXPECT_EQ(WtlCount(cache), maxBeforeEvict);

    memset(&evicted, 0, sizeof(WtlValue));

    // Pushing at probation cap should evict an item, count should remain the same
    EXPECT_EQ(WtlInsert(cache, &_dummy32[15], &evicted), WTL_ITEM_EVICTED);
    EXPECT_EQ(WtlCount(cache), maxBeforeEvict);

    // Ensure internals 
    EXPECT_EQ(wtlHashTableCount(&cache->table), maxBeforeEvict);
    EXPECT_TRUE(lruCount(&cache->windowCache) > 0);
    EXPECT_TRUE(lruCount(&cache->mainCache.probation) > 0);

    // Protected should be untouched without touching get()
    EXPECT_EQ(lruCount(&cache->mainCache.protected), 0);

    free(cache);
    return 0;
}

/*
* Confirms the evicted item is reported with the correct key and value
* and is actually gone from the cache. At the maxFill every stored entry has
* estimate 1, and _chooseVictim loses ties in favor of the victim, so the
* window candidate evicts itself: the lru window tail is the victim.
*/
static int InsertEvictsCorrectItemIdentity(void)
{
    uint32_t maxBeforeEvict;
    WtlEntry* victim = NULL, victimClone;
    WtlValue evicted;
    WtlCtx* cache = allocCache(NULL);

    memset(&evicted, 0, sizeof(WtlValue));

    maxBeforeEvict = cache->config.probationSize + cache->config.windowSize;
    ENSURE(maxBeforeEvict < 32);

    // Fill to the maxFill
    for (uint32_t i = 0; i < maxBeforeEvict; i++)
    {
        EXPECT_EQ(WtlInsert(cache, &_dummy32[i], NULL), WTL_SUCCESS);
    }

    // The lru window tail (candidate) is the deterministic eviction victim
    victim = lruPeek(&cache->windowCache);
    ENSURE(victim);	// fault guard   

    // Copy the victim before modifying the table
    victimClone = *victim;

    EXPECT_EQ(WtlInsert(cache, &_dummy32[15], &evicted), WTL_ITEM_EVICTED);

    // Evicted value must be the victim exactly
    EXPECT_TRUE(evicted.keyLen == spanGetSizeC(victimClone.key));
    EXPECT_EQ(memcmp(evicted.key, spanGetOffsetC(victimClone.key, 0), evicted.keyLen), 0);
    EXPECT_TRUE(evicted.value == victimClone.value);

    free(cache);
    return 0;
}

static int InsertMovesToProbationWhenWindowFull(void)
{
    WtlCtx* cache = allocCache(NULL);

    // Ensure rest window size is smaller than our dummy buffer
    ENSURE(cache->config.windowSize < 32);

    // Add probation - 1 entries  
    for (int i = 0; i < cache->config.windowSize; i++)
    {
        EXPECT_EQ(WtlInsert(cache, &_dummy32[i], NULL), WTL_SUCCESS);
    }

    // total count should be exactly window size
    EXPECT_EQ(WtlCount(cache), cache->config.windowSize);

    // Ensure probation is still empty and window is at capacity
    EXPECT_EQ(lruCount(&cache->mainCache.probation), 0);
    EXPECT_EQ(lruCount(&cache->windowCache), cache->config.windowSize);

    // One more should overflow to probation, but not evict
    EXPECT_EQ(WtlInsert(cache, &_dummy32[15], NULL), WTL_SUCCESS);
    EXPECT_EQ(lruCount(&cache->windowCache), cache->config.windowSize);
    EXPECT_EQ(lruCount(&cache->mainCache.probation), 1);

    // The item should be the only element in the list (head and tail) so peek can see it
    ENSURE(lruPeek(&cache->mainCache.probation)); //segfault guard
    EXPECT_TRUE(lruPeek(&cache->mainCache.probation)->value == _dummy32[0].value);

    free(cache);
    return 0;
}

/*
* Confirms the W-TinyLFU admission tie rule: when the window candidate
* and the probation victim estimate equally, the candidate is rejected
* and the victim survives. During startup both items should have sketch
* of 1 which should cause the candidate to get evicted and victim 
* survives.
*/
static int InsertAdmissionColdTieEvictsCandidate(void)
{
    uint32_t maxFill;
    WtlValue evicted;
    WtlEntry* candidate = NULL, * victim = NULL;
    WtlCtx* cache = allocCache(NULL);

    memset(&evicted, 0, sizeof(WtlValue));

    // Fill window and probation to the max before overflow
    maxFill = cache->config.windowSize + cache->config.probationSize;
    ENSURE(maxFill < 32);

    for (uint32_t i = 0; i < maxFill; i++)
    {
        EXPECT_EQ(WtlInsert(cache, &_dummy32[i], NULL), WTL_SUCCESS);
    }

    // Candidate is the lru window entry, victim the lru probation entry.
    // Both are cold (one sketch record from insert) so estimates tie at 1.
    candidate = lruPeek(&cache->windowCache);
    victim = lruPeek(&cache->mainCache.probation);
    ENSURE(candidate && victim); // fault guard

    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, candidate->hash), 1u);
    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, victim->hash), 1u);   

    WtlKey candKey =
    {
        .key = spanGetOffsetC(candidate->key, 0),
        .len = spanGetSizeC(candidate->key)
    };

    WtlKey vicKey =
    {
        .key = spanGetOffsetC(victim->key, 0),
        .len = spanGetSizeC(victim->key)
    }; 

    // Spec: on a tie the new candidate is rejected, the victim stays
    EXPECT_EQ(WtlInsert(cache, &_dummyValues[0], &evicted), WTL_ITEM_EVICTED);
    EXPECT_EQ(memcmp(evicted.key, candKey.key, candKey.len), 0);  

    // Candidate is gone, victim and the new entry survive
    EXPECT_EQ(WtlPeek(cache, vicKey, NULL), WTL_SUCCESS);
    EXPECT_EQ(WtlPeek(cache, _dummyKeys[0], NULL), WTL_SUCCESS);
    EXPECT_EQ(WtlPeek(cache, candKey, NULL), WTL_ERR_NOT_FOUND);      

    EXPECT_EQ(WtlCount(cache), maxFill);
    EXPECT_EQ(lruCount(&cache->windowCache), cache->config.windowSize);
    EXPECT_EQ(lruCount(&cache->mainCache.probation), cache->config.probationSize);

    free(cache);
    return 0;
}

static int RunInsertTests(void)
{
    RUN_TEST(InsertParameterValidation());
    RUN_TEST(InsertBasic());
    RUN_TEST(InsertFailsWithWillEvictWhenNull());
    RUN_TEST(InsertInsertsIntoWindowFirst());
    RUN_TEST(InsertAlwaysInsertsIntoWindowHead());
    RUN_TEST(InsertMovesLruItemToProbation());
    RUN_TEST(InsertDucpliateKeysReturnsError());
    RUN_TEST(InsertDuplicateDoesNotAlterState());
    RUN_TEST(InsertEvictsWhenProbationFull());
    RUN_TEST(InsertEvictsCorrectItemIdentity());
    RUN_TEST(InsertMovesToProbationWhenWindowFull());
    RUN_TEST(InsertAdmissionColdTieEvictsCandidate());

    return 0;
}
