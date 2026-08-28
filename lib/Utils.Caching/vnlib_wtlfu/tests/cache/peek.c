
// peek.c

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
* Confirms that Peek does not modify the sketch table in any way 
* on key hits or misses
*/
static int PeekDoesNotAlterSketchForAnyKey(void)
{
	WtlCtx* cache = allocCache(NULL);
	cspan_t sketchMem = spanToC(cache->sketch.table);

	// Buffer to hold initial state after insertion
	uint8_t* sketchCompBuf = (uint8_t*)malloc(spanGetSizeC(sketchMem));
	ENSURE(sketchCompBuf);

	_addDummyValues(cache);	

	// copy sketch table to local buffer
	memmove(sketchCompBuf, spanGetOffsetC(sketchMem, 0), spanGetSizeC(sketchMem));

	// sanity check, should match exactly
	EXPECT_EQ(memcmp(sketchCompBuf, spanGetOffsetC(sketchMem, 0), spanGetSizeC(sketchMem)), 0);

	// Peek all dummy keys, should all be found
	for (int i = 0; i < DUMMY_VALUE_SIZE; i++)
	{
		ENSURE(WtlPeek(cache, _dummyKeys[i], NULL) == WTL_SUCCESS);
	}

	// Memcmp should still match after peek on the sketch table
	EXPECT_EQ(memcmp(sketchCompBuf, spanGetOffsetC(sketchMem, 0), spanGetSizeC(sketchMem)), 0);

	// Peek all misses from the dummy32 table
	for (int i = 0; i < 32; i++)
	{
		WtlKey key = { .key = _dummy32[i].key, .len = _dummy32[i].keyLen };

		ENSURE(WtlPeek(cache, key, NULL) == WTL_ERR_NOT_FOUND);
	}

	// Still clean after misses
	EXPECT_EQ(memcmp(sketchCompBuf, spanGetOffsetC(sketchMem, 0), spanGetSizeC(sketchMem)), 0);

	free(sketchCompBuf);
	free(cache);
	return 0;
}

/*
* Confirms that Peek does not change the state of any internal 
* lru lists.
*/
static int PeekDoesNotAlterLists(void)
{	
	WtlCtx* cache = allocCache(NULL);	

	_addDummyValues(cache);	

	uint32_t window = lruCount(&cache->windowCache);
	uint32_t probation = lruCount(&cache->mainCache.probation);
	uint32_t protected = lruCount(&cache->mainCache.protected);

	const WtlEntry* windowTail = lruPeek(&cache->windowCache);
	const WtlEntry* probeeTail = lruPeek(&cache->mainCache.probation);
	const WtlEntry* protectedTail = lruPeek(&cache->mainCache.protected);

	for (int i = 0; i < DUMMY_VALUE_SIZE; i++)
	{
		ENSURE(WtlPeek(cache, _dummyKeys[i], NULL) == WTL_SUCCESS);
	}
	
	// Lists should be the same as before peek
	EXPECT_EQ(lruCount(&cache->windowCache), window);
	EXPECT_EQ(lruCount(&cache->mainCache.probation), probation);
	EXPECT_EQ(lruCount(&cache->mainCache.protected), protected);

	// Ensure items did not move in lists
	EXPECT_TRUE(lruPeek(&cache->windowCache) == windowTail);
	EXPECT_TRUE(lruPeek(&cache->mainCache.probation) == probeeTail);
	EXPECT_TRUE(lruPeek(&cache->mainCache.protected) == protectedTail);

	free(cache);
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
	RUN_TEST(PeekDoesNotAlterSketchForAnyKey());
	RUN_TEST(PeekDoesNotAlterLists());

	return 0;
}
