
// insert.c

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
	ENSURE(lruPeek(&cache->mainCache.probation)); //segfualt guard
	EXPECT_TRUE(lruPeek(&cache->mainCache.probation)->value == _dummy32[0].value);

	free(cache);
	return 0;
}

static int RunInsertTests(void)
{
	RUN_TEST(InsertBasic());
	RUN_TEST(InsertFailsWithWillEvictWhenNull());
	RUN_TEST(InsertInsertsIntoWindowFirst());
	RUN_TEST(InsertAlwaysInsertsIntoWindowHead());
	RUN_TEST(InsertMovesLruItemToProbation());
	RUN_TEST(InsertDucpliateKeysReturnsError());
	RUN_TEST(InsertEvictsWhenProbationFull());
	RUN_TEST(InsertMovesToProbationWhenWindowFull());

	return 0;
}
