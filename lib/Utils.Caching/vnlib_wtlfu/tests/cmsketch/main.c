/*
 * Copyright (c) 2026 Vaughn Nugent
 *
 * Library: VNLib
 * Package: vnlib_wtlfu
 * File: tests/cmsketch/main.c
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

#include <stdlib.h>
#include <cmsketch.h>

#include "test.h"
#include "hex.h"

static void* _alloc(void* ctx, size_t size, size_t alignment);
static void _free(void* ctx, void* ptr, size_t size);

struct memstats_t {
    int allocatedBytes;
    int allocCount;
    int freeCount;
};

static const WtlAllocator DefaultAllocator = {
    .Alloc  = &_alloc,
    .Free   = &_free,
    .ctx    = NULL
};

/* default sketch config values from internal.h */
static const WtlSketchConfig DefaultConfig = {
    .depth          = WTL_SKETCH_DEFAULT_DEPTH,
    .width          = WTL_SKETCH_DEFAULT_WIDTH,
    .seed           = WTL_SKETCH_BASE_SEED,        
    .resetThreshold = WTL_SKETCH_DEFAULT_RESET_MULT * WTL_SKETCH_DEFAULT_WIDTH
};

static int BasicCreateTest(void)
{
    // Assert that test config just works
    {
        WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
        EXPECT_TRUE(sketch);
        
        // Cleanup so no leaks happen 
        wtlfuSketchDestroy(sketch);
    }

    // bad config variable validation
    {
        WtlSketchConfig badConfig = DefaultConfig;

        // Depth == 0
        badConfig.depth = 0;
        EXPECT_FALSE(wtlfuSketchCreate(&badConfig, &DefaultAllocator));

        // Width == 0
        badConfig = DefaultConfig;
        badConfig.width = 0;

        EXPECT_FALSE(wtlfuSketchCreate(&badConfig, &DefaultAllocator));

        // Depth > max depth
        badConfig = DefaultConfig;
        badConfig.depth = WTL_SKETCH_MAX_DEPTH + 1;

        EXPECT_FALSE(wtlfuSketchCreate(&badConfig, &DefaultAllocator));

        // max depth * width > uint32_max
        badConfig = DefaultConfig;
        badConfig.depth = WTL_SKETCH_MAX_DEPTH;
        badConfig.width = UINT32_MAX;

        EXPECT_FALSE(wtlfuSketchCreate(&badConfig, &DefaultAllocator));

        // reset threshold == 0
        badConfig = DefaultConfig;
        badConfig.resetThreshold = 0;

        EXPECT_FALSE(wtlfuSketchCreate(&badConfig, &DefaultAllocator));
    }

    return 0;
}

/*
* Does a basic test to track memory allocations and frees made directly
* by the cmksketch unit. Two allocations should be made, one for the sketch
* state and one for the internal counter table.
* 
* Adds a local stats counter to the local test allocator to track allocations
*/
static int BasicMemoryAllocFreeTest(void)
{    
    struct memstats_t memCounter = { 0 };

    WtlAllocator allocator = DefaultAllocator;

    // Store test memcounter for stats counting
    allocator.ctx = &memCounter;

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &allocator);
    EXPECT_TRUE(sketch);

    // Expect the table to be allocated + some memory for the internal sketch structure
    EXPECT_TRUE(memCounter.allocatedBytes > (DefaultConfig.width * DefaultConfig.depth));
    EXPECT_EQ(memCounter.allocCount, 2);
    EXPECT_EQ(memCounter.freeCount, 0);

    // Ensure all is freed correctly
    wtlfuSketchDestroy(sketch);

    // allocated bytes should be 0, alloc count should remain, free should increment
    EXPECT_EQ(memCounter.allocatedBytes, 0);
    EXPECT_EQ(memCounter.allocCount, 2);
    EXPECT_EQ(memCounter.freeCount, 2);

    return 0;
}

/*
* Tests are compared to this count exactly. This only holds if the sketch
* table is large enough that hashes don't collide. With the default config
* (width=1024, depth=4) and a single key, collisions are effectively
* impossible, so exact equality is safe here.
*
* May experience flakey tests if the table size is too small causing
* wraps, or reset threshold triggers an age during test.
*/
#define _SINGLE_RECORD_COUNT 10

static int RecordAndEstimate_SingleKey(void)
{
    span_t testKey1 = FromHexString("68656c6c6f20776f726c64", 11);
    span_t testKey2 = FromHexString("68656c6c6f20776f726c6432", 12);

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);  

    //Expect 0 when no keys have been recorded
    {
        EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(testKey1)), 0);
    }

    // Ensure a key recorded X times is estimated correctly
    {
        for (int i = 0; i < _SINGLE_RECORD_COUNT; i++)
        {
            wtlfuSketchRecord(sketch, spanToC(testKey1));
        }

        EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(testKey1)), _SINGLE_RECORD_COUNT);
    }

    // Ensure isolated key is not modified
    {
        EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(testKey2)), 0);
    }

    wtlfuSketchDestroy(sketch);
    return 0;
}

/* 
* Exact equality holds only when no hash collisions occur. With the
* default config (width=1024, depth=4) and a single key, the table
* is large enough that collisions are effectively impossible.
*/
static int Record_MultipleTimes(void)
{
    int counts[] = { 1, 5, 50, 100 };

    span_t testKey = FromHexString("68656c6c6f20776f726c64", 11);

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);

    // For each record count, reset the sketch so each iteration
    // starts from zero counters, then verify the estimate matches exactly.
    for (int i = 0; i < (int)(sizeof(counts) / sizeof(counts[0])); i++)
    {
        for (int j = 0; j < counts[i]; j++)
        {
            wtlfuSketchRecord(sketch, spanToC(testKey));
        }

        EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(testKey)), counts[i]);

        // Reset all counters for next iteration
        wtlfuSketchReset(sketch);
    }

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Verifies that a single key recorded N times estimates exactly N.
* With a single key there are no collisions, so the Count-Min minimum
* across rows equals the true count exactly.
*/
static int Estimate_EqualsRecordCount_SingleKey(void)
{
    span_t testKey = FromHexString("68656c6c6f20776f726c64", 11);

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);

    // Record the key an increasing number of times and verify the
    // estimate equals the actual record count at every stage.
    for (uint32_t n = 1; n <= 200; n++)
    {
        wtlfuSketchRecord(sketch, spanToC(testKey));

        EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(testKey)), n);
    }

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Verifies that a zero-length key (NULL data, size 0) is handled
* correctly by both Record and Estimate. The hash function must
* produce a deterministic value for the empty input, and the sketch
* must treat it like any other key.
*/
static int EmptyKey(void)
{
    cspan_t emptyKey;
    span_t otherKey = FromHexString("68656c6c6f", 5);

    spanInitC(&emptyKey, NULL, 0);

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);

    // Estimate of an unrecorded empty key should be 0
    EXPECT_EQ(wtlfuSketchEstimate(sketch, emptyKey), 0);

    // Record the empty key 3 times
    for (int i = 0; i < 3; i++)
    {
        wtlfuSketchRecord(sketch, emptyKey);
    }

    // Estimate should reflect the 3 records. With the default config
    // collisions are effectively impossible, so exact equality holds.
    EXPECT_EQ(wtlfuSketchEstimate(sketch, emptyKey), 3);

    // A different (non-empty) key should still be 0
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(otherKey)), 0);

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Verifies that a freshly created sketch returns 0 for any key
* before any records have been made. All counters start at zero
* so the minimum across rows must be zero.
*/
static int Record_DoesNotEstimateBeforeRecord(void)
{
    span_t key1 = FromHexString("68656c6c6f", 5);
    span_t key2 = FromHexString("776f726c64", 5);

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);

    // No records have been made; every key must estimate 0
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(key1)), 0);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(key2)), 0);

    // Also verify the empty key
    cspan_t emptyKey;
    spanInitC(&emptyKey, NULL, 0);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, emptyKey), 0);

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Records two distinct keys with different frequencies into the same
* sketch and verifies each estimates correctly, while an unrecorded
* key remains at zero. Uses the default config (width=1024, depth=4)
* so collisions are effectively impossible for three keys.
*/
static int RecordAndEstimate_MultipleKeys(void)
{
    span_t keyA = FromHexString("68656c6c6f", 5);
    span_t keyB = FromHexString("776f726c64", 5);
    span_t keyC = FromHexString("666f6f626172", 6);

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);

    // Record key A five times and key B three times
    for (int i = 0; i < 5; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyA));
    }

    for (int i = 0; i < 3; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyB));
    }

    // Estimates should match exact record counts
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), 5);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), 3);

    // Unrecorded key should be zero
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyC)), 0);

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Records ten distinct keys, each exactly once, into the same sketch
* and verifies every key estimates 1 while an unrecorded key estimates 0.
* With the default config (width=1024, depth=4) and only 10 keys,
* collisions are effectively impossible.
*/
static int MultiKey_Separation(void)
{
    /* 10 distinct 1-byte keys: 0x00 through 0x09 */
    static const char* hexKeys[10] = {
        "00", "01", "02", "03", "04",
        "05", "06", "07", "08", "09"
    };

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);

    // Record each key once
    for (int i = 0; i < 10; i++)
    {
        span_t key = _fromHexString(hexKeys[i], 2);
        wtlfuSketchRecord(sketch, spanToC(key));
    }

    // Each recorded key should estimate exactly 1
    for (int i = 0; i < 10; i++)
    {
        span_t key = _fromHexString(hexKeys[i], 2);
        EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(key)), 1);
    }

    // An unrecorded key should estimate 0
    {
        span_t unknown = FromHexString("ff", 1);
        EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(unknown)), 0);
    }

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Records a single key well beyond the uint8_t counter maximum (255)
* and verifies the estimate saturates at 255 rather than wrapping to
* zero. This exercises the saturation guard in wtlfuSketchRecord.
*
* Note: the default resetThreshold (10 * 1024 = 10240) is well above
* 300 records, so aging will not fire during this test.
*/
static int Record_Saturation(void)
{
    span_t key = FromHexString("68656c6c6f", 5);

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);

    // fill but ensure saturation does not occur until exactly UTINT8_MAX
    for (int i = 0; i < UINT8_MAX - 1; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(key));

        // ensure count remains below the max size
        ENSURE(wtlfuSketchEstimate(sketch, spanToC(key)) < UINT8_MAX);
    }

    // Final record saturates the counter
    wtlfuSketchRecord(sketch, spanToC(key));   
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(key)), UINT8_MAX);

    // Ensure any number of records after max remains at UINT8_MAX
    for (int i = 0; i < 10; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(key));
        EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(key)), UINT8_MAX);
    }

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Verifies that saturating one key's counter does not corrupt
* another key's estimate. Key A is saturated to 255, key B is
* recorded only 3 times, and both estimates must be independent.
*/
static int Saturation_DoesNotAffectOtherKeys(void)
{
    span_t keyA = FromHexString("68656c6c6f", 5);
    span_t keyB = FromHexString("776f726c64", 5);

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);

    // Saturate key A
    for (int i = 0; i < 300; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyA));
    }

    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), UINT8_MAX);

    // Record key B a small number of times
    for (int i = 0; i < 3; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyB));
    }

    // Key A remains saturated, key B estimates exactly 3
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), UINT8_MAX);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), 3);

    wtlfuSketchDestroy(sketch);

    return 0;
}

#define _ESTIMATE_MANY_NUM_KEYS 50

/*
* Verifies the Count-Min Sketch overestimation guarantee across many
* keys with varying frequencies: the estimate for any key is always
* greater than or equal to the number of times that key was actually
* recorded. Collisions can only inflate counters, so the minimum
* across rows is an upper bound on the true frequency.
*
* This test uses a small width (16) to create deliberate collision
* pressure, making the overestimation invariant the only guaranteed
* property.
*/
static int Estimate_ManyKeys_OverestimationGuarantee(void)
{
    /* distinct single-byte keys: 0x00 through 0x31 */
    uint8_t keyBytes[_ESTIMATE_MANY_NUM_KEYS];
    int recordCounts[_ESTIMATE_MANY_NUM_KEYS];

    WtlSketchConfig config = DefaultConfig;
    config.width = 16;

    WtlSketch* sketch = wtlfuSketchCreate(&config, &DefaultAllocator);
    ENSURE(sketch);

    // Record key N exactly N times (1..num_keys)
    for (int i = 0; i < _ESTIMATE_MANY_NUM_KEYS; i++)
    {
        keyBytes[i] = (uint8_t)i;
        recordCounts[i] = i + 1;

        for (int j = 0; j < recordCounts[i]; j++)
        {
            cspan_t key;
            spanInitC(&key, &keyBytes[i], 1);
            wtlfuSketchRecord(sketch, key);
        }
    }

    // Verify each key's estimate is at least its record count
    for (int i = 0; i < _ESTIMATE_MANY_NUM_KEYS; i++)
    {
        cspan_t key;
        uint32_t estimate;

        spanInitC(&key, &keyBytes[i], 1);
        estimate = wtlfuSketchEstimate(sketch, key);

        EXPECT_TRUE(estimate >= (uint32_t)recordCounts[i]);
    }

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Verifies that wtlfuSketchAge halves every counter in the table.
* Records two keys: one to a known small value (8) and one saturated
* to 255. After each aging call, estimates are checked against the
* expected halved values. Integer division rounds down, so 8 -> 4 ->
* 2 -> 1 -> 0, and 255 -> 127 -> 63.
*/
static int Age_HalvesCounters(void)
{
    span_t keyA = FromHexString("68656c6c6f", 5);
    span_t keyB = FromHexString("776f726c64", 5);

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);

    // Record key A exactly 8 times
    for (int i = 0; i < 8; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyA));
    }

    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), 8);

    // Saturate key B to 255
    for (int i = 0; i < 300; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyB));
    }

    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), UINT8_MAX);

    // First aging: 8 -> 4, 255 -> 127
    wtlfuSketchAge(sketch);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), 4);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), 127);

    // Second aging: 4 -> 2, 127 -> 63
    wtlfuSketchAge(sketch);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), 2);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), 63);

    // Third aging: 2 -> 1, 63 -> 31
    wtlfuSketchAge(sketch);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), 1);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), 31);

    // Fourth aging: 1 -> 0, 31 -> 15
    wtlfuSketchAge(sketch);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), 0);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), 15);

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Verifies that automatic aging fires exactly at resetThreshold and
* that the internal access counter is reset afterward, preventing
* premature re-aging. Uses a small custom threshold (10) so the test
* is deterministic without recording thousands of keys.
*
* Sequence:
*   - Record 9 times: no aging, estimate == 9
*   - 10th record: aging fires, estimate == 4 (9+1=10, halved = 5)
*   - Record 9 more: no aging, estimate == 14 (5+9)
*   - 10th again: aging fires, estimate == 7 (14 halved = 7)
*/
static int Age_ResetsAccessCounter(void)
{
    span_t key = FromHexString("68656c6c6f", 5);

    // Override default config reset threshold
    WtlSketchConfig config = DefaultConfig;
    config.resetThreshold = 10;

    WtlSketch* sketch = wtlfuSketchCreate(&config, &DefaultAllocator);
    ENSURE(sketch);

    // Record 9 times: below threshold, no aging
    for (int i = 0; i < 9; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(key));
    }

    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(key)), 9);

    // 10th record triggers aging: 10 halved = 5
    wtlfuSketchRecord(sketch, spanToC(key));
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(key)), 5);

    // Record 9 more: access counter was reset, no aging
    for (int i = 0; i < 9; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(key));
    }

    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(key)), 14);

    // 10th record since last age triggers aging again: 14 halved = 7
    wtlfuSketchRecord(sketch, spanToC(key));
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(key)), 7);

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Verifies that aging decays all keys uniformly, not just the most
* recently recorded one. Records key A 8 times and key B 4 times, then
* ages and checks both are halved. An unrecorded key C must remain at 0
* (0 >> 1 == 0, no underflow).
*/
static int Age_AffectsMultipleKeys(void)
{
    span_t keyA = FromHexString("68656c6c6f", 5);
    span_t keyB = FromHexString("776f726c64", 5);
    span_t keyC = FromHexString("666f6f626172", 6);

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);

    // Record key A 8 times and key B 4 times
    for (int i = 0; i < 8; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyA));
    }

    for (int i = 0; i < 4; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyB));
    }

    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), 8);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), 4);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyC)), 0);

    // Age: A -> 4, B -> 2, C stays 0
    wtlfuSketchAge(sketch);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), 4);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), 2);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyC)), 0);

    // Age again: A -> 2, B -> 1, C still 0
    wtlfuSketchAge(sketch);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), 2);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), 1);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyC)), 0);

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Verifies that wtlfuSketchReset clears all counters and the access
* counter, returning the sketch to a fresh state. Records two keys,
* resets, then confirms all estimates are zero and the sketch is
* usable again by recording and estimating a new key.
*/
static int Reset_ClearsAllState(void)
{
    span_t keyA = FromHexString("68656c6c6f", 5);
    span_t keyB = FromHexString("776f726c64", 5);

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);

    // Record both keys so counters are non-zero
    for (int i = 0; i < 5; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyA));
    }

    for (int i = 0; i < 3; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyB));
    }

    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), 5);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), 3);

    // Reset all state
    wtlfuSketchReset(sketch);

    // All estimates should be zero after reset
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), 0);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), 0);

    // Sketch should be usable again: record keyB and verify
    for (int i = 0; i < 4; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyB));
    }

    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyB)), 4);
    EXPECT_EQ(wtlfuSketchEstimate(sketch, spanToC(keyA)), 0);

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Verifies that two sketches created with identical configuration and
* fed the same sequence of records produce identical estimates for all
* keys. This confirms the sketch is deterministic: same config + same
* input = same output, which is essential for reproducible behavior.
*/
static int Determinism_SameConfigSameEstimates(void)
{
    span_t keyA = FromHexString("68656c6c6f", 5);
    span_t keyB = FromHexString("776f726c64", 5);
    span_t keyC = FromHexString("666f6f626172", 6);

    WtlSketch* s1 = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    WtlSketch* s2 = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(s1);
    ENSURE(s2);

    // Feed both sketches the same mixed sequence
    for (int i = 0; i < 7; i++)
    {
        wtlfuSketchRecord(s1, spanToC(keyA));
        wtlfuSketchRecord(s2, spanToC(keyA));
    }

    for (int i = 0; i < 3; i++)
    {
        wtlfuSketchRecord(s1, spanToC(keyB));
        wtlfuSketchRecord(s2, spanToC(keyB));
    }

    wtlfuSketchRecord(s1, spanToC(keyC));
    wtlfuSketchRecord(s2, spanToC(keyC));

    // Estimates must match across both sketches
    EXPECT_EQ(wtlfuSketchEstimate(s1, spanToC(keyA)), wtlfuSketchEstimate(s2, spanToC(keyA)));
    EXPECT_EQ(wtlfuSketchEstimate(s1, spanToC(keyB)), wtlfuSketchEstimate(s2, spanToC(keyB)));
    EXPECT_EQ(wtlfuSketchEstimate(s1, spanToC(keyC)), wtlfuSketchEstimate(s2, spanToC(keyC)));

    // Age both and verify they still match
    wtlfuSketchAge(s1);
    wtlfuSketchAge(s2);

    EXPECT_EQ(wtlfuSketchEstimate(s1, spanToC(keyA)), wtlfuSketchEstimate(s2, spanToC(keyA)));
    EXPECT_EQ(wtlfuSketchEstimate(s1, spanToC(keyB)), wtlfuSketchEstimate(s2, spanToC(keyB)));
    EXPECT_EQ(wtlfuSketchEstimate(s1, spanToC(keyC)), wtlfuSketchEstimate(s2, spanToC(keyC)));

    wtlfuSketchDestroy(s1);
    wtlfuSketchDestroy(s2);

    return 0;
}


/*
* Verifies that aging decays all keys uniformly, preserving the
* relative popularity ordering. Records key A 20 times and key B
* 5 times, then ages twice. After each aging, A must still have a
* higher estimate than B. This confirms aging does not corrupt the
* frequency ordering, which is critical for the W-TinyLFU admission
* policy that compares estimates to decide which item survives.
*/
static int Aging_PreservesRelativeOrdering(void)
{
    span_t keyA = FromHexString("68656c6c6f", 5);
    span_t keyB = FromHexString("776f726c64", 5);

    WtlSketch* sketch = wtlfuSketchCreate(&DefaultConfig, &DefaultAllocator);
    ENSURE(sketch);

    // Record key A 20 times, key B 5 times
    for (int i = 0; i < 20; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyA));
    }

    for (int i = 0; i < 5; i++)
    {
        wtlfuSketchRecord(sketch, spanToC(keyB));
    }

    // A should be more popular than B before aging
    EXPECT_TRUE(wtlfuSketchEstimate(sketch, spanToC(keyA)) > wtlfuSketchEstimate(sketch, spanToC(keyB)));

    // First aging: A=10, B=2, ordering preserved
    wtlfuSketchAge(sketch);
    EXPECT_TRUE(wtlfuSketchEstimate(sketch, spanToC(keyA)) > wtlfuSketchEstimate(sketch, spanToC(keyB)));

    // Second aging: A=5, B=1, ordering still preserved
    wtlfuSketchAge(sketch);
    EXPECT_TRUE(wtlfuSketchEstimate(sketch, spanToC(keyA)) > wtlfuSketchEstimate(sketch, spanToC(keyB)));

    wtlfuSketchDestroy(sketch);

    return 0;
}

/*
* Verifies the core Count-Min Sketch overestimation guarantee:
* the estimated frequency of any key is always >= its true record
* count. Collisions can only inflate counters, never deflate them,
* so the minimum across rows is still an upper bound on true count.
*
* Uses a deliberately tiny width (16) to create heavy collision pressure
* with 30 keys, making overestimation likely and exercising the
* invariant that the sketch is designed to guarantee.
*/
static int Estimate_OverestimationGuarantee(void)
{
    uint8_t keyBytes[30];
    uint32_t recordCounts[30];

    WtlSketchConfig tinyConfig = DefaultConfig;
    tinyConfig.width = 16;
    tinyConfig.depth = 4;
    tinyConfig.resetThreshold = 10000;

    WtlSketch* sketch = wtlfuSketchCreate(&tinyConfig, &DefaultAllocator);
    ENSURE(sketch);

    // Record key N exactly N+1 times (1..30)
    for (int i = 0; i < 30; i++)
    {
        cspan_t key;

        keyBytes[i] = (uint8_t)i;
        recordCounts[i] = (uint32_t)(i + 1);

        spanInitC(&key, &keyBytes[i], 1);

        for (int j = 0; j < recordCounts[i]; j++)
        {
            wtlfuSketchRecord(sketch, key);
        }
    }

    // Verify each key's estimate is >= its true record count
    for (int i = 0; i < 30; i++)
    {
        cspan_t key;

        spanInitC(&key, &keyBytes[i], 1);

        EXPECT_TRUE(wtlfuSketchEstimate(sketch, key) >= recordCounts[i]);
    }

    wtlfuSketchDestroy(sketch);

    return 0;
}

int RunTests(void)
{
    RUN_TEST(BasicCreateTest());
    RUN_TEST(BasicMemoryAllocFreeTest());
    RUN_TEST(RecordAndEstimate_SingleKey());
    RUN_TEST(Record_MultipleTimes());
    RUN_TEST(Estimate_EqualsRecordCount_SingleKey());
    RUN_TEST(EmptyKey());
    RUN_TEST(Record_DoesNotEstimateBeforeRecord());
    RUN_TEST(RecordAndEstimate_MultipleKeys());
    RUN_TEST(MultiKey_Separation());
    RUN_TEST(Record_Saturation());
    RUN_TEST(Saturation_DoesNotAffectOtherKeys());
    RUN_TEST(Estimate_ManyKeys_OverestimationGuarantee());
    RUN_TEST(Age_HalvesCounters());
    RUN_TEST(Age_ResetsAccessCounter());
    RUN_TEST(Age_AffectsMultipleKeys());
    RUN_TEST(Reset_ClearsAllState());
    RUN_TEST(Determinism_SameConfigSameEstimates());
    RUN_TEST(Aging_PreservesRelativeOrdering());
    RUN_TEST(Estimate_OverestimationGuarantee());

    return 0;
}

static void* _alloc(void* ctx, size_t size, size_t alignment)
{
    void* ptr = malloc(size);

    if (ptr && ctx)
    {
        struct memstats_t* stats = (struct memstats_t*)ctx;

        stats->allocatedBytes += size;
        stats->allocCount++;
    }

    return ptr;
}

static void _free(void* ctx, void* ptr, size_t size)
{
    free(ptr);

    if (ctx) 
    {
        struct memstats_t* stats = (struct memstats_t*)ctx;

        stats->allocatedBytes -= size;
        stats->freeCount++;
    }
}
