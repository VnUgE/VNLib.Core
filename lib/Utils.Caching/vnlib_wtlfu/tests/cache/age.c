
// age.c

/*
* Confirms WtlAgeSketch argument validation.
*/
static int AgeSketchParameterValidation(void)
{
    EXPECT_EQ(WtlAgeSketch(NULL), WTL_ERR_INVALID_ARG);

    return 0;
}

/*
* Confirms that WtlAgeSketch halves every sketch counter (rounding
* down) and clears the access counter. A warm key decays by roughly
* half while an cold key stays at zero.
*/
static int AgeSketchHalvesEstimates(void)
{
    WtlCtx* cache = allocCache(NULL);

    EXPECT_EQ(WtlInsert(cache, &_dummyValues[0], NULL), WTL_SUCCESS);   

    // The stored entry carries the derived key hash
    const WtlEntry* entry = lruHeadGet(&cache->windowCache);
    ENSURE(entry); // fault guard

    // The insert records 1 access; 20 gets record 20 more
    for (int i = 0; i < 20; i++)
    {
        WtlValue outVal;
        EXPECT_EQ(WtlGet(cache, _dummyKeys[0], &outVal), WTL_SUCCESS);
    }

    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, entry->hash), 21u);

    // Manual age halves every counter, 21 >>= 1 is 10
    EXPECT_EQ(WtlAgeSketch(cache), WTL_SUCCESS);
    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, entry->hash), 10u);

    // Access counter restarts, cold key stays at zero
    EXPECT_EQ(cache->sketch.accessCount, 0u);
    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, getKeyHashCode(cache, _dummyKeys[1])), 0u);

    free(cache);
    return 0;
}

/*
* Confirms the automatic aging trigger: once the sketch records
* resetThreshold total accesses it ages itself and restarts the access
* counter, decaying every counter, without a manual call.
*/
static int AgeSketchAgesAutomaticallyAtThreshold(void)
{
    // Low threshold so the auto-age is reachable with a few gets
    WtlValue outVal;
    WtlConfig cfg = _defaultConfig;
    cfg.sketchResetThreshold = 20;
   
    WtlCtx* cache = allocCache(&cfg);

    // Always ads 1 to sketch for the key
    EXPECT_EQ(WtlInsert(cache, &_dummyValues[0], NULL), WTL_SUCCESS);

    const WtlEntry* entry = findEntryByKey(cache, _dummyKeys[0]);
    ENSURE(entry); // fault guard

    // Increment to threshold-1 should trigger age. 
    for (int i = 0; i < 18; i++)
    {
        EXPECT_EQ(WtlGet(cache, _dummyKeys[0], &outVal), WTL_SUCCESS);
    }

    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, entry->hash), 19u);

    // Should trigger an age on next get
    EXPECT_EQ(WtlGet(cache, _dummyKeys[0], &outVal), WTL_SUCCESS);

    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, entry->hash), 10u);
    EXPECT_EQ(cache->sketch.accessCount, 0u);

    // Further gets accumulate normally above the decayed base
    EXPECT_EQ(WtlGet(cache, _dummyKeys[0], &outVal), WTL_SUCCESS);
    EXPECT_EQ(wtlSketchEstimate(&cache->sketch, entry->hash), 11u);
    EXPECT_EQ(cache->sketch.accessCount, 1u);

    free(cache);
    return 0;
}

static int RunAgeTests(void)
{
    RUN_TEST(AgeSketchParameterValidation());
    RUN_TEST(AgeSketchHalvesEstimates());
    RUN_TEST(AgeSketchAgesAutomaticallyAtThreshold());

    return 0;
}
