/*
 * Copyright (c) 2026 Vaughn Nugent
 *
 * Library: VNLib
 * Package: vnlib_wtlfu
 * File: main.c
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

#include <test.h>
#include <hex.h>

#include <wtlfu.h>
#include <cache.h>	// Include cache internal library

// Include internal headers for comparing setups
#include <hash.h>
#include <cmsketch.h>
#include <lru.h>
#include <hashtable.h>

static const WtlConfig _defaultConfig = {		
    .seed			= 87862408u,

    .capacity		= 0x40u,		
    .windowPct		= 0x01u,		// 1% common value
    .protectedPct	= 0x50u,		// 80% reserved is common
        
    // sketch (16 * 1024 = table size)
    .sketchDepth	= 4u,
    .sketchWidth	= 1024u,		
    .sketchResetThreshold = 10 * 1024,	// common value is 10 x width
    .sketchSeed     = 6787612u
};

#define dummy_key(keyStr) { .key = keyStr, .len = (uint32_t)(sizeof(keyStr) - 1) }
#define dummy_value(keyStr) { .key = keyStr, .keyLen = (uint32_t)(sizeof(keyStr) - 1), .value = keyStr }

/* matchin dummy keys and values for populating test tables with */
#define DUMMY_VALUE_SIZE 5

static const WtlValue _dummyValues[DUMMY_VALUE_SIZE] = {
    dummy_value("foo"),
    dummy_value("bar"),
    dummy_value("baz"),
    dummy_value("quay"),
    dummy_value("hello world with a slightly longer key string")
};

static const WtlKey _dummyKeys[DUMMY_VALUE_SIZE] = {
    dummy_key("foo"),
    dummy_key("bar"),
    dummy_key("baz"),
    dummy_key("quay"),
    dummy_key("hello world with a slightly longer key string")
};

static const WtlValue _dummy32[32] = {
    dummy_value("foo0"),  dummy_value("foo1"),
    dummy_value("foo2"),  dummy_value("foo3"),
    dummy_value("foo4"),  dummy_value("foo5"),
    dummy_value("foo6"),  dummy_value("foo7"),
    dummy_value("foo8"),  dummy_value("foo9"),
    dummy_value("foo10"), dummy_value("foo11"),
    dummy_value("foo12"), dummy_value("foo13"),
    dummy_value("foo14"), dummy_value("foo15"),
    dummy_value("foo16"), dummy_value("foo17"),
    dummy_value("foo18"), dummy_value("foo19"),
    dummy_value("foo20"), dummy_value("foo21"),
    dummy_value("foo22"), dummy_value("foo23"),
    dummy_value("foo24"), dummy_value("foo25"),
    dummy_value("foo26"), dummy_value("foo27"),
    dummy_value("foo28"), dummy_value("foo29"),
    dummy_value("foo30"), dummy_value("foo31"),
};

static _vn_inline void _addDummyValues(WtlCtx* cache)
{
    // Evicted dummy for fulfilling contract. Assertion assumes not 
    // evictions occur.
    WtlValue evictedDummy;

    for (int i = 0; i < DUMMY_VALUE_SIZE; i++)
    {
        TASSERT(WtlInsert(cache, &_dummyValues[i], &evictedDummy) == WTL_SUCCESS);
    }

    TASSERT(WtlCount(cache) == DUMMY_VALUE_SIZE);
}

static WtlCtx* allocCache(const WtlConfig* config)
{
    if (!config)
    {
        config = &_defaultConfig;
    }

    int32_t memSize = WtlGetMemorySize(config);
    TASSERT(memSize > 0);

    // Alloc cache memory and ensure it succeed
    WtlCtx* cache = (WtlCtx*)malloc(memSize);
    TASSERT(cache);

    // Init cache and assert success
    TASSERT(WtlInit(config, cache) == WTL_SUCCESS);

    return cache;
}

static uint32_t getKeyHashCode(const WtlCtx* cache, WtlKey key)
{    
    cspan_t keySpan;
    spanInitC(&keySpan, key.key, key.len);
    return wtlHash32(keySpan, cache->config.keySeed);
}

static WtlEntry* findEntryByKey(WtlCtx* cache, WtlKey key)
{   
    cspan_t keySpan;
    spanInitC(&keySpan, key.key, key.len);
    return wtlFindEntryFromKey(cache, keySpan);
}

static WtlEntry* findEntryByValue(WtlCtx* cache, const WtlValue* value)
{
    WtlKey key = { .key = value->key, .len = value->keyLen };

    return findEntryByKey(cache, key);
}

#include "config-setup.c"
#include "get.c"
#include "insert.c"
#include "peek.c"
#include "remove.c"
#include "remove-value.c"
#include "iterate.c"
#include "touch.c"
#include "age.c"

static int VersionStringIsSet(void)
{
    const char* version = WtlGetVersionString();
    EXPECT_TRUE(version);

    printf("    Testing library version %s\n", version);

    return 0;
}

int RunTests(void)
{

    RUN_TEST(VersionStringIsSet());

    TEST_GROUP(RunConfigTests());
    
    TEST_GROUP(RunGetTests());

    TEST_GROUP(RunInsertTests());	

    TEST_GROUP(RunPeekTests());

    TEST_GROUP(RunRemoveTests());

    TEST_GROUP(RunRemoveValueTests());

    TEST_GROUP(RunIteratorTests());

    TEST_GROUP(RunTouchTests());

    TEST_GROUP(RunAgeTests());

    return 0;
}
