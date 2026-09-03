/*
 * Copyright (c) 2026 Vaughn Nugent
 *
 * Library: VNLib
 * Package: vnlib_wtlfu
 * File: config-setup.c
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
* Confirms that a valid config initializes correctly.
* WtlGetMemorySize reports a positive size and WtlInit succeeds,
* then the initialized cache is inspected: capacity and key seed
* are copied over, the window/protected/probation slices add up to
* the full capacity, the sketch uses the requested dimensions, and
* the hashtable is sized with load factor overhead.
*/
static int ConfigHappyPath(void)
{
    int32_t memSize = WtlGetMemorySize(&_defaultConfig);
    EXPECT_TRUE(memSize > 0);

    // Alloc cache memory
    WtlCtx* cache = (WtlCtx*)malloc(memSize);
    TASSERT(cache);

    // Init cache and assert success
    EXPECT_TRUE(WtlInit(&_defaultConfig, cache) == WTL_SUCCESS);	

    // Check global config
    {
        EXPECT_EQ(cache->config.capacity, _defaultConfig.capacity);
        EXPECT_EQ(cache->config.keySeed, _defaultConfig.seed);


        //TODO assert proper list percentage slices

        // Ensure slices add up to full table capacity
        EXPECT_EQ(
            cache->config.windowSize + cache->config.protectedSize + cache->config.probationSize,
            _defaultConfig.capacity
        );
    }

    // Check sketch
    {
        EXPECT_EQ(cache->sketch.config.depth, _defaultConfig.sketchDepth);
        EXPECT_EQ(cache->sketch.config.width, _defaultConfig.sketchWidth);
        EXPECT_EQ(cache->sketch.config.seed, _defaultConfig.sketchSeed);
        EXPECT_EQ(cache->sketch.config.resetThreshold, _defaultConfig.sketchResetThreshold);	

        // Ensure sketch was setup properly. Assumes sketch tests have good coverage
        // should ensure that the table size matches the width * depth
        EXPECT_TRUE(wtlSketchIsValid(&cache->sketch) == WTL_SUCCESS);
    }

    // Check hashtable
    {
        // Ensure the capacity is at least as big as capacity. Should inclue ht overhead
        uint64_t htWithOverhead = _htCapacityWithOverhead(_defaultConfig.capacity);
        EXPECT_TRUE(htWithOverhead >= _defaultConfig.capacity);

        EXPECT_EQ(cache->table.capacity, htWithOverhead);
        EXPECT_EQ(cache->table.count, 0);
        
        // Expects good ht coverage. Also ensures that slot memory is not null
        EXPECT_TRUE(wtlHashTableIsValid(&cache->table) == WTL_SUCCESS);
    }

    free(cache);
    return 0;
}

/*
* Confirms that invalid config values are rejected before any
* memory is allocated.
*/
static int Config_InvalidValues_Fails(void)
{

    {
        WtlConfig config = _defaultConfig;
        config.capacity = WTL_MIN_CAPACITY;

        // Min capacity should succeed
        EXPECT_TRUE(WtlGetMemorySize(&config) > 0);

        // Blow min capacity should be invalid args
        config.capacity--;
        EXPECT_EQ(WtlGetMemorySize(&config), WTL_ERR_INVALID_ARG);

        config.capacity = 0;
        EXPECT_EQ(WtlGetMemorySize(&config), WTL_ERR_INVALID_ARG);
    }

    // Window percent range
    {
        WtlConfig config = _defaultConfig;

        // Max percent should be accepted
        config.windowPct = WTL_NUM_MAX_PERCENT;
        EXPECT_TRUE(WtlGetMemorySize(&config) > 0);

        // 0, u32 max, or percent max +1 should fail
        config.windowPct++;
        EXPECT_EQ(WtlGetMemorySize(&config), WTL_ERR_INVALID_ARG);

        config.windowPct = 0;
        EXPECT_EQ(WtlGetMemorySize(&config), WTL_ERR_INVALID_ARG);

        config.windowPct = UINT32_MAX;
        EXPECT_EQ(WtlGetMemorySize(&config), WTL_ERR_INVALID_ARG);
    }

    // Protected percent range
    {
        WtlConfig config = _defaultConfig;

        // Max percent should be accepted
        config.protectedPct = WTL_NUM_MAX_PERCENT;
        EXPECT_TRUE(WtlGetMemorySize(&config) > 0);

        // 0, u32 max, or percent max +1 should fail
        config.protectedPct++;
        EXPECT_EQ(WtlGetMemorySize(&config), WTL_ERR_INVALID_ARG);

        config.protectedPct = 0;
        EXPECT_EQ(WtlGetMemorySize(&config), WTL_ERR_INVALID_ARG);

        config.protectedPct = UINT32_MAX;
        EXPECT_EQ(WtlGetMemorySize(&config), WTL_ERR_INVALID_ARG);
    }

    // Sketch tests should be mostly covered by sketch. Assuming we assign sketch (tested in happy path)
    // we should never have invalid sketch values

    return 0;
}

/*
* Confirms that the computed memory layout is correct for a valid
* config. The sketch and slot regions are the expected sizes, every
* region starts on a cache line boundary after the header, the
* regions never overlap, and the layout total stays in sync with
* what WtlGetMemorySize reports.
*/
static int Config_MemoryLayout(void)
{
    WtlConfig config = _defaultConfig;
    struct wtl_cache_layout layout;

    // Get hashtable load factor overhead 
    uint64_t htWithOverhead = _htCapacityWithOverhead(config.capacity);

    // Init layout from default config
    wtlConfigGetMemoryLayout(&config, &layout);

    // Region sizes match the per-region math
    EXPECT_EQ(layout.sketchBytes, (uint64_t)config.sketchWidth * config.sketchDepth);
    EXPECT_EQ(layout.slotsBytes, ((uint64_t)htWithOverhead * sizeof(WtlHashSlot)));

    // Every region starts on a cache line boundary, after the header
    EXPECT_TRUE(layout.slotsOffset % WTL_CACHE_LINE == 0);
    EXPECT_TRUE(layout.sketchOffset % WTL_CACHE_LINE == 0);
    EXPECT_TRUE(layout.slotsOffset >= sizeof(WtlCtx));

    // Regions do not overlap and total is the sketch region's end
    EXPECT_TRUE(layout.sketchOffset >= layout.slotsOffset + layout.slotsBytes);
    EXPECT_EQ(layout.total, (uint64_t)(layout.sketchOffset + layout.sketchBytes));

    // The size formula and the public sizing API cannot drift apart
    EXPECT_EQ((uint64_t)WtlGetMemorySize(&config), layout.total);

    return 0;
}

/*
* Finds a capacity whose layout total exceeds INT32_MAX. The layout
* total grows monotonically with capacity, so a single walk suffices.
*/
static uint32_t _findOversizedCapacity(WtlConfig* config, struct wtl_cache_layout* layout)
{
    uint32_t capacity = 0;

    do
    {
        capacity += 0x100000u;
        config->capacity = capacity;
        wtlConfigGetMemoryLayout(config, layout);
    }
    while (layout->total <= (uint64_t)INT32_MAX);

    // A crossing capacity must exist on 64-bit platforms
    ENSURE(layout->total > (uint64_t)INT32_MAX);
    return capacity;
}

/*
* Confirms WtlGetMemorySize rejects a config whose layout total
* exceeds INT32_MAX (the return type is int32), and that a config
* whose total sits just below the cap still reports its exact size.
*/
static int Config_MemorySizeCappedAtInt32Max(void)
{   
    WtlConfig config = _defaultConfig;
    struct wtl_cache_layout layout;

    // Just below the cap: the exact total must be reported
    _findOversizedCapacity(&config, &layout);

    do
    {
        config.capacity -= 0x1000u;
        wtlConfigGetMemoryLayout(&config, &layout);
    }
    while (layout.total > (uint64_t)INT32_MAX);

    EXPECT_EQ((uint64_t)WtlGetMemorySize(&config), layout.total);

    // Over the cap gets rejected
    EXPECT_EQ(_findOversizedCapacity(&config, &layout), config.capacity);
    EXPECT_EQ(WtlGetMemorySize(&config), WTL_ERR_INVALID_ARG);

    return 0;
}

/*
* Confirms WtlInit rejects configurations where the total memory size
* surpasses INT32_MAX before continuing with initialization.
*/
static int InitRejectsOversizedLayout(void)
{
    uint8_t block[WTL_CACHE_LINE];
    WtlConfig config = _defaultConfig;
    struct wtl_cache_layout layout;

    memset(block, 0xFF, sizeof(block));

    _findOversizedCapacity(&config, &layout);

    // Init must fail before the memset inside WtlInit runs
    EXPECT_EQ(WtlInit(&config, (WtlCtx*)block), WTL_ERR_INVALID_ARG);

    for (uint32_t i = 0; i < sizeof(block); i++)
    {
        EXPECT_EQ(block[i], 0xFF);
    }

    return 0;
}

static int RunConfigTests(void)
{
    RUN_TEST(ConfigHappyPath());
    RUN_TEST(Config_InvalidValues_Fails());
    RUN_TEST(Config_MemoryLayout());
    RUN_TEST(Config_MemorySizeCappedAtInt32Max());
    RUN_TEST(InitRejectsOversizedLayout());
    return 0;
}
