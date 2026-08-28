
// remove-value.c

/*
* Ensures that RemoveValue removes a value by reading/comparing 
* it's key, not by any of the value memory/addresses. 
*/
static int RemoveValueIsolated(void)
{
	WtlCtx* cache = allocCache(NULL);

	WtlValue dummy = dummy_value("hello world");
	WtlKey dummyKey = dummy_key("hello world");

	EXPECT_EQ(WtlInsert(cache, &dummy, NULL), WTL_SUCCESS);
	EXPECT_EQ(WtlCount(cache), 1);
	EXPECT_EQ(WtlPeek(cache, dummyKey, NULL), WTL_SUCCESS);

	// Remove and check 
	EXPECT_EQ(WtlRemoveValue(cache, &dummy), WTL_SUCCESS);
	EXPECT_EQ(WtlCount(cache), 0);
	EXPECT_EQ(WtlPeek(cache, dummyKey, NULL), WTL_ERR_NOT_FOUND);

	free(cache);
	return 0;
}

/*
* Ensures that calling remove again on a value that 
* has just been removed returns NOT_FOUND
*/
static int RemoveValueDoubleRemoveFails(void)
{
	WtlCtx* cache = allocCache(NULL);

	WtlValue dummy = dummy_value("hello world");
	WtlKey dummyKey = dummy_key("hello world");

	EXPECT_EQ(WtlInsert(cache, &dummy, NULL), WTL_SUCCESS);

	// Double remove should fail with not found
	EXPECT_EQ(WtlRemoveValue(cache, &dummy), WTL_SUCCESS);
	EXPECT_EQ(WtlRemoveValue(cache, &dummy), WTL_ERR_NOT_FOUND);

	free(cache);
	return 0;
}

/*
* Ensures that RemoveValue removes a value that is assigned/returned
* from the Peek function instead of using a manually assigned key
* or value.
*/
static int RemoveValueFromPeek(void)
{
	WtlCtx* cache = allocCache(NULL);

	WtlValue dummy = dummy_value("hello world");
	WtlKey dummyKey = dummy_key("hello world");

	WtlValue peekaboo; 

	EXPECT_EQ(WtlInsert(cache, &dummy, NULL), WTL_SUCCESS);
	EXPECT_EQ(WtlPeek(cache, dummyKey, &peekaboo), WTL_SUCCESS);

	// Remove by peeked value
	EXPECT_EQ(WtlRemoveValue(cache, &peekaboo), WTL_SUCCESS);
	EXPECT_EQ(WtlCount(cache), 0);
	EXPECT_EQ(WtlPeek(cache, dummyKey, NULL), WTL_ERR_NOT_FOUND);

	free(cache);
	return 0;
}

/*
* Ensures that RemoveValue removes a value that is assigned/returned
* from the Get function instead of using a manually assigned key
* or value.
*/
static int RemoveValueFromGet(void)
{
	WtlCtx* cache = allocCache(NULL);

	WtlValue dummy = dummy_value("hello world");
	WtlKey dummyKey = dummy_key("hello world");

	WtlValue getVal;

	EXPECT_EQ(WtlInsert(cache, &dummy, NULL), WTL_SUCCESS);
	EXPECT_EQ(WtlGet(cache, dummyKey, &getVal), WTL_SUCCESS);

	// Remove by get value
	EXPECT_EQ(WtlRemoveValue(cache, &getVal), WTL_SUCCESS);
	EXPECT_EQ(WtlCount(cache), 0);
	EXPECT_EQ(WtlPeek(cache, dummyKey, NULL), WTL_ERR_NOT_FOUND);

	free(cache);
	return 0;
}

/*
* Checks that remove fails when the table is completely 
* empty.
*/
static int RemoveValueEmptyTableReturnsNotFound(void)
{
	WtlCtx* cache = allocCache(NULL);

	WtlValue dummy = dummy_value("hello world");

	ENSURE(WtlCount(cache) == 0);
	EXPECT_EQ(WtlRemoveValue(cache, &dummy), WTL_ERR_NOT_FOUND);

	free(cache);
	return 0;
}

/*
* Checks that remove fails when the key is not in a partially
* loaded table.
*/
static int RemoveValueLoadedTableReturnsNotFound(void)
{
	WtlCtx* cache = allocCache(NULL);

	// Add items to the table
	_addDummyValues(cache);

	WtlValue dummy = dummy_value("hello world");

	EXPECT_EQ(WtlRemoveValue(cache, &dummy), WTL_ERR_NOT_FOUND);

	free(cache);
	return 0;
}

static int RunRemoveValueTests(void)
{
	RUN_TEST(RemoveValueIsolated());
	RUN_TEST(RemoveValueDoubleRemoveFails());
	RUN_TEST(RemoveValueFromPeek());
	RUN_TEST(RemoveValueFromGet());
	RUN_TEST(RemoveValueEmptyTableReturnsNotFound());
	RUN_TEST(RemoveValueLoadedTableReturnsNotFound());

	return 0;
}
