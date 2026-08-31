
// get.c

static int GetParameterValidation(void)
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
		EXPECT_TRUE(get.key == getVal.key);
		EXPECT_TRUE(get.keyLen == getVal.keyLen);
		EXPECT_TRUE(get.value == getVal.value);
	}

	free(cache);
	return 0;
}

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

static int GetIncrementsCounterForKey(void)
{
	WtlValue outVal;
	cspan_t keySpan;
	uint32_t hashCode;
	WtlKey dummyKey = _dummyKeys[0];
	WtlCtx* cache = allocCache(&_defaultConfig);

	// Get hashcode for dummy key
	spanInitC(&keySpan, dummyKey.key, dummyKey.len);
	hashCode = wtlHash32(keySpan, cache->config.keySeed);

	_addDummyValues(cache);

	// Estimate should be 1 on insert before get is called
	EXPECT_EQ(wtlSketchEstimate(&cache->sketch, hashCode), 1);

	EXPECT_EQ(WtlGet(cache, _dummyKeys[0], &outVal), WTL_SUCCESS);

	// Estimate should have increased since get a full get
	EXPECT_EQ(wtlSketchEstimate(&cache->sketch, hashCode), 2);

	free(cache);
	return 0;
}

static int GetPromotesProbeeWhenProtectedFreeSpace(void)
{
	WtlValue outVal;
	WtlCtx* cache = allocCache(&_defaultConfig);

	_addDummyValues(cache);

	// Ensure dummy 0 is on probation list

	EXPECT_EQ(WtlGet(cache, _dummyKeys[0], &outVal), WTL_SUCCESS);	

	free(cache);
	return 0;
}

static int RunGetTests(void)
{
    RUN_TEST(GetParameterValidation());
	RUN_TEST(GetBasic());
	RUN_TEST(GetNotFound());
	RUN_TEST(GetIncrementsCounterForKey());

	return 0;
}
