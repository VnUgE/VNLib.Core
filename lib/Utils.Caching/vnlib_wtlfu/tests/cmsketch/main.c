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

/* default sketch config values from internal.h */
static const WtlSketchConfig DefaultConfig = {
    .depth          = WTL_SKETCH_DEFAULT_DEPTH,
    .width          = WTL_SKETCH_DEFAULT_WIDTH,
    .seed           = WTL_SKETCH_BASE_SEED,        
    .resetThreshold = WTL_SKETCH_DEFAULT_RESET_MULT * WTL_SKETCH_DEFAULT_WIDTH
};

static WtlSketch* sketchAlloc(const WtlSketchConfig* config)
{
    uint32_t size = config->depth * config->width;

    // Alloc sketch plus table size and ensure zeroed before use
    WtlSketch* buf = (WtlSketch*)malloc(sizeof(WtlSketch) + size);
    
    if (!buf)
    { 
        return NULL; 
    }

    memset(buf, 0, sizeof(WtlSketch) + size);
    
    // assign config structure
    buf->config = *config;
    
    // Init the table to point at the buffer right after the struct header
    spanInit(&buf->table, (uint8_t*)(buf + 1), size);
    
    // Ensure valid initialization
    TASSERT(wtlSketchIsValid(buf) == 0);

    return buf;
}

/*
* Verifies that a correctly initialized sketch (config assigned, zeroed
* table sized exactly width * depth, accessCount at 0) passes
* wtlSketchIsValid. This is the baseline contract for the caller-owned
* allocation model: when the caller fulfills the layout requirements,
* the sketch is considered valid and usable.
*/
static int IsValid_ValidConfig(void)
{
    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Freshly allocated sketch is structurally valid
    {
        EXPECT_EQ(wtlSketchIsValid(sketch), 0);
    }

    // A sketch that has been in use is still structurally valid
    {
        span_t key = FromHexString("68656c6c6f", 5);
        wtlSketchRecord(sketch, spanToC(key));
        wtlSketchRecord(sketch, spanToC(key));
        EXPECT_EQ(wtlSketchIsValid(sketch), 0);
    }

    // ...and remains valid after aging
    {
        wtlSketchAge(sketch);
        EXPECT_EQ(wtlSketchIsValid(sketch), 0);
    }

    // ...and after reset
    {
        wtlSketchReset(sketch);
        EXPECT_EQ(wtlSketchIsValid(sketch), 0);
    }

    free(sketch);

    return 0;
}

/*
* Manually constructs a WtlSketch on the caller's stack for tests that
* need to inspect invalid states. Unlike sketchAlloc (which asserts the
* result is valid) this performs no validation, so any config and table
* combination can be produced, including deliberately broken ones.
*/
static void sketchOnStack(WtlSketch* sketch, WtlSketchConfig config, uint8_t* table, uint32_t tableSize)
{
    sketch->config = config;
    sketch->accessCount = 0;
    sketch->table.data = table;
    sketch->table.size = tableSize;
}

/*
* Verifies that wtlSketchIsValid rejects every invalid configuration and
* table-layout condition the caller can produce. Since the sketch is now
* caller-allocated and self-initialized, validation is the caller's
* responsibility; this pins the exact error codes each bad state must
* yield so the contract stays stable.
*/
static int IsValid_RejectsInvalidTable(void)
{
    // small config keeps the stack table tiny; validity logic is size-agnostic
    WtlSketchConfig baseConfig = DefaultConfig;
    baseConfig.width = 8;
    baseConfig.depth = 4;

    uint8_t table[8 * 4];

    // table span too small for config (width*depth)
    {
        WtlSketch sketch;
        WtlSketchConfig config = baseConfig;
        config.width += 1;
        sketchOnStack(&sketch, config, table, sizeof(table));
        EXPECT_EQ(wtlSketchIsValid(&sketch), -3);
    }

    // table span too large for config (required width*depth smaller than table)
    {
        WtlSketch sketch;
        WtlSketchConfig config = baseConfig;
        config.width -= 1;
        sketchOnStack(&sketch, config, table, sizeof(table));
        EXPECT_EQ(wtlSketchIsValid(&sketch), -3);
    }

    // zero width
    {
        WtlSketch sketch;
        WtlSketchConfig config = baseConfig;
        config.width = 0;
        sketchOnStack(&sketch, config, table, sizeof(table));
        EXPECT_EQ(wtlSketchIsValid(&sketch), -1);
    }

    // zero depth
    {
        WtlSketch sketch;
        WtlSketchConfig config = baseConfig;
        config.depth = 0;
        sketchOnStack(&sketch, config, table, sizeof(table));
        EXPECT_EQ(wtlSketchIsValid(&sketch), -1);
    }

    // depth exceeds the maximum allowed
    {
        WtlSketch sketch;
        WtlSketchConfig config = baseConfig;
        config.depth = WTL_SKETCH_MAX_DEPTH + 1;
        sketchOnStack(&sketch, config, table, sizeof(table));
        EXPECT_EQ(wtlSketchIsValid(&sketch), -1);
    }

    // zero reset threshold
    {
        WtlSketch sketch;
        WtlSketchConfig config = baseConfig;
        config.resetThreshold = 0;
        sketchOnStack(&sketch, config, table, sizeof(table));
        EXPECT_EQ(wtlSketchIsValid(&sketch), -1);
    }

    // empty table span
    {
        WtlSketch sketch;
        sketchOnStack(&sketch, baseConfig, NULL, 0);
        EXPECT_EQ(wtlSketchIsValid(&sketch), -1);
    }

    return 0;
}

/*
* Verifies that the exposed accessCount field tracks every
* wtlSketchRecord call and resets to zero exactly at the auto-aging
* boundary (resetThreshold). Uses a small threshold (10) so the
* boundary is reachable in a handful of records.
*/
static int Record_IncrementsAccessCount(void)
{
    span_t key = FromHexString("68656c6c6f", 5);

    WtlSketchConfig config = DefaultConfig;
    config.resetThreshold = 10;

    WtlSketch* sketch = sketchAlloc(&config);
    ENSURE(sketch);

    // a fresh sketch has not recorded any accesses
    {
        EXPECT_EQ(sketch->accessCount, 0);
    }

    // each record increments the access count by exactly one
    {
        for (uint32_t i = 1; i <= 9; i++)
        {
            wtlSketchRecord(sketch, spanToC(key));
            EXPECT_EQ(sketch->accessCount, i);
        }
    }

    // 10th record hits the threshold: aging fires and the counter resets
    {
        wtlSketchRecord(sketch, spanToC(key));
        EXPECT_EQ(sketch->accessCount, 0);
    }

    // aging really happened: 10 records halved to 5
    {
        EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(key)), 5);
    }

    // counting resumes from zero after the reset
    {
        wtlSketchRecord(sketch, spanToC(key));
        EXPECT_EQ(sketch->accessCount, 1);
    }

    free(sketch);

    return 0;
}

/*
* Verifies that both wtlSketchAge and wtlSketchReset clear the exposed
* accessCount back to zero, so a subsequent record starts a fresh aging
* cycle. Also confirms the counter is untouched by Estimate, which is a
* read-only operation.
*/
static int Age_AndReset_ClearAccessCount(void)
{
    span_t key = FromHexString("68656c6c6f", 5);

    WtlSketchConfig config = DefaultConfig;
    config.resetThreshold = 100;

    WtlSketch* sketch = sketchAlloc(&config);
    ENSURE(sketch);

    // build up a known non-zero access count
    {
        for (int i = 0; i < 7; i++)
        {
            wtlSketchRecord(sketch, spanToC(key));
        }
        EXPECT_EQ(sketch->accessCount, 7);
    }

    // estimate must not disturb the access count
    {
        wtlSketchEstimate(sketch, spanToC(key));
        EXPECT_EQ(sketch->accessCount, 7);
    }

    // manual aging clears the access count (counters are halved)
    {
        wtlSketchAge(sketch);
        EXPECT_EQ(sketch->accessCount, 0);
    }

    // a fresh cycle counts from zero
    {
        wtlSketchRecord(sketch, spanToC(key));
        wtlSketchRecord(sketch, spanToC(key));
        EXPECT_EQ(sketch->accessCount, 2);
    }

    // reset also clears the access count (and all counters)
    {
        wtlSketchReset(sketch);
        EXPECT_EQ(sketch->accessCount, 0);
        EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(key)), 0);
    }

    free(sketch);

    return 0;
}

/*
* Verifies the physical table layout behind the exposed table span:
* recording a single key must increment exactly one counter per row
* (depth non-zero bytes in total), and the sum of all counters must
* equal the record count. With the default config a single key cannot
* collide with itself, so any extra non-zero byte would mean writes
* landed outside the span the span claims to own.
*/
static int Record_WritesExactlyOneCounterPerRow(void)
{
    span_t key = FromHexString("68656c6c6f", 5);

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // scan a fresh table: every counter must be zero
    {
        uint32_t nonZero = 0;
        uint32_t sum = 0;
        for (uint32_t i = 0; i < spanGetSize(sketch->table); i++)
        {
            uint8_t v = *spanGetOffset(sketch->table, i);
            sum += v;
            if (v != 0) { nonZero++; }
        }
        EXPECT_EQ(nonZero, 0);
        EXPECT_EQ(sum, 0);
    }

    // record the key a few times
    {
        for (int i = 0; i < 6; i++)
        {
            wtlSketchRecord(sketch, spanToC(key));
        }
    }

    // scan again: exactly one counter per row is hot, each holding the record count
    {
        uint32_t nonZero = 0;
        uint32_t sum = 0;
        for (uint32_t i = 0; i < spanGetSize(sketch->table); i++)
        {
            uint8_t v = *spanGetOffset(sketch->table, i);
            sum += v;
            if (v != 0) { nonZero++; }
        }
        // exactly one counter per row is hot
        EXPECT_EQ(nonZero, DefaultConfig.depth);
        // each record increments every row, so total = records * depth
        EXPECT_EQ(sum, 6 * DefaultConfig.depth);
    }

    free(sketch);

    return 0;
}

/*
* Verifies the config seed actually influences counter placement. Two
* sketches configured identically except for the seed must not map the
* same key sequence to the same counter distribution. Both sketches are
* recorded the identical sequence of keys, then their raw tables are
* compared: a working seed scatters the keys to different columns, so
* the tables must differ. The tables can only be byte-identical if every
* key landed in the same column in both sketches, which has negligible
* probability (roughly (1/width)^keys) when the seed is honored, while
* an ignored seed would produce identical tables with certainty.
*/
static int Seed_ChangesBucketPlacement(void)
{
    static const WtlSketchConfig smallConfig = {
        .depth          = 4,
        .width          = 32,
        .seed           = WTL_SKETCH_BASE_SEED,
        .resetThreshold = WTL_SKETCH_DEFAULT_RESET_MULT * 32
    };

    WtlSketchConfig altConfig = smallConfig;
    altConfig.seed = WTL_SKETCH_BASE_SEED + 1;

    span_t keys[8];
    static const uint32_t recordCounts[8] = { 3, 4, 5, 2, 3, 4, 5, 2 };

    // The FromHexString macro expands into a statement, so the keys
    // are assigned individually rather than in an initializer
    {
        keys[0] = FromHexString("68656c6c6f", 5);
        keys[1] = FromHexString("776f726c64", 5);
        keys[2] = FromHexString("666f6f626172", 6);
        keys[3] = FromHexString("62617a", 3);
        keys[4] = FromHexString("717578", 3);
        keys[5] = FromHexString("666f7a", 3);
        keys[6] = FromHexString("616263", 3);
        keys[7] = FromHexString("646566", 3);
    }

    WtlSketch* sa = sketchAlloc(&smallConfig);
    WtlSketch* sb = sketchAlloc(&altConfig);
    ENSURE(sa);
    ENSURE(sb);

    // record the same key sequence into both sketches
    {
        for (int i = 0; i < 8; i++)
        {
            for (uint32_t j = 0; j < recordCounts[i]; j++)
            {
                wtlSketchRecord(sa, spanToC(keys[i]));
                wtlSketchRecord(sb, spanToC(keys[i]));
            }
        }
    }

    // differing seeds must scatter keys to different columns, so the
    // raw tables cannot be byte-identical
    {
        const uint8_t* ta = (const uint8_t*)sa->table.data;
        const uint8_t* tb = (const uint8_t*)sb->table.data;
        const uint32_t size = spanGetSize(sa->table);
        uint32_t diff = 0;

        for (uint32_t i = 0; i < size; i++)
        {
            if (ta[i] != tb[i]) { diff++; }
        }
        
        EXPECT_TRUE(diff > 0);
    }

    free(sa);
    free(sb);

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

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);  

    //Expect 0 when no keys have been recorded
    {
        EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(testKey1)), 0);
    }

    // Ensure a key recorded X times is estimated correctly
    {
        for (int i = 0; i < _SINGLE_RECORD_COUNT; i++)
        {
            wtlSketchRecord(sketch, spanToC(testKey1));
        }

        EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(testKey1)), _SINGLE_RECORD_COUNT);
    }

    // Ensure isolated key is not modified
    {
        EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(testKey2)), 0);
    }

    free(sketch);
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

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // For each record count, reset the sketch so each iteration
    // starts from zero counters, then verify the estimate matches exactly.
    for (int i = 0; i < (int)(sizeof(counts) / sizeof(counts[0])); i++)
    {
        for (int j = 0; j < counts[i]; j++)
        {
            wtlSketchRecord(sketch, spanToC(testKey));
        }

        EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(testKey)), counts[i]);

        // Reset all counters for next iteration
        wtlSketchReset(sketch);
    }

    free(sketch);

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

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Record the key an increasing number of times and verify the
    // estimate equals the actual record count at every stage.
    for (uint32_t n = 1; n <= 200; n++)
    {
        wtlSketchRecord(sketch, spanToC(testKey));

        EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(testKey)), n);
    }

    free(sketch);

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

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Estimate of an unrecorded empty key should be 0
    EXPECT_EQ(wtlSketchEstimate(sketch, emptyKey), 0);

    // Record the empty key 3 times
    for (int i = 0; i < 3; i++)
    {
        wtlSketchRecord(sketch, emptyKey);
    }

    // Estimate should reflect the 3 records. With the default config
    // collisions are effectively impossible, so exact equality holds.
    EXPECT_EQ(wtlSketchEstimate(sketch, emptyKey), 3);

    // A different (non-empty) key should still be 0
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(otherKey)), 0);

    free(sketch);

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

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // No records have been made; every key must estimate 0
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(key1)), 0);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(key2)), 0);

    // Also verify the empty key
    cspan_t emptyKey;
    spanInitC(&emptyKey, NULL, 0);
    EXPECT_EQ(wtlSketchEstimate(sketch, emptyKey), 0);

    free(sketch);

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

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Record key A five times and key B three times
    for (int i = 0; i < 5; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyA));
    }

    for (int i = 0; i < 3; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyB));
    }

    // Estimates should match exact record counts
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), 5);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), 3);

    // Unrecorded key should be zero
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyC)), 0);

    free(sketch);

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

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Record each key once
    for (int i = 0; i < 10; i++)
    {
        span_t key = _fromHexString(hexKeys[i], 2);
        wtlSketchRecord(sketch, spanToC(key));
    }

    // Each recorded key should estimate exactly 1
    for (int i = 0; i < 10; i++)
    {
        span_t key = _fromHexString(hexKeys[i], 2);
        EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(key)), 1);
    }

    // An unrecorded key should estimate 0
    {
        span_t unknown = FromHexString("ff", 1);
        EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(unknown)), 0);
    }

    free(sketch);

    return 0;
}

/*
* Records a single key well beyond the uint8_t counter maximum (255)
* and verifies the estimate saturates at 255 rather than wrapping to
* zero. This exercises the saturation guard in wtlSketchRecord.
*
* Note: the default resetThreshold (10 * 1024 = 10240) is well above
* 300 records, so aging will not fire during this test.
*/
static int Record_Saturation(void)
{
    span_t key = FromHexString("68656c6c6f", 5);

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // fill but ensure saturation does not occur until exactly UINT8_MAX
    for (int i = 0; i < UINT8_MAX - 1; i++)
    {
        wtlSketchRecord(sketch, spanToC(key));

        // ensure count remains below the max size
        ENSURE(wtlSketchEstimate(sketch, spanToC(key)) < UINT8_MAX);
    }

    // Final record saturates the counter
    wtlSketchRecord(sketch, spanToC(key));   
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(key)), UINT8_MAX);

    // Ensure any number of records after max remains at UINT8_MAX
    for (int i = 0; i < 10; i++)
    {
        wtlSketchRecord(sketch, spanToC(key));
        EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(key)), UINT8_MAX);
    }

    free(sketch);

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

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Saturate key A
    for (int i = 0; i < 300; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyA));
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), UINT8_MAX);

    // Record key B a small number of times
    for (int i = 0; i < 3; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyB));
    }

    // Key A remains saturated, key B estimates exactly 3
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), UINT8_MAX);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), 3);

    free(sketch);

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

    WtlSketch* sketch = sketchAlloc(&config);
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
            wtlSketchRecord(sketch, key);
        }
    }

    // Verify each key's estimate is at least its record count
    for (int i = 0; i < _ESTIMATE_MANY_NUM_KEYS; i++)
    {
        cspan_t key;
        uint32_t estimate;

        spanInitC(&key, &keyBytes[i], 1);
        estimate = wtlSketchEstimate(sketch, key);

        EXPECT_TRUE(estimate >= (uint32_t)recordCounts[i]);
    }

    free(sketch);

    return 0;
}

/*
* Verifies that wtlSketchAge halves every counter in the table.
* Records two keys: one to a known small value (8) and one saturated
* to 255. After each aging call, estimates are checked against the
* expected halved values. Integer division rounds down, so 8 -> 4 ->
* 2 -> 1 -> 0, and 255 -> 127 -> 63.
*/
static int Age_HalvesCounters(void)
{
    span_t keyA = FromHexString("68656c6c6f", 5);
    span_t keyB = FromHexString("776f726c64", 5);

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Record key A exactly 8 times
    for (int i = 0; i < 8; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyA));
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), 8);

    // Saturate key B to 255
    for (int i = 0; i < 300; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyB));
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), UINT8_MAX);

    // First aging: 8 -> 4, 255 -> 127
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), 4);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), 127);

    // Second aging: 4 -> 2, 127 -> 63
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), 2);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), 63);

    // Third aging: 2 -> 1, 63 -> 31
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), 1);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), 31);

    // Fourth aging: 1 -> 0, 31 -> 15
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), 0);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), 15);

    free(sketch);

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
*   - 10th record: aging fires, estimate == 5 (9+1=10, halved = 5)
*   - Record 9 more: no aging, estimate == 14 (5+9)
*   - 10th again: aging fires, estimate == 7 (14 halved = 7)
*/
static int Age_ResetsAccessCounter(void)
{
    span_t key = FromHexString("68656c6c6f", 5);

    // Override default config reset threshold
    WtlSketchConfig config = DefaultConfig;
    config.resetThreshold = 10;

    WtlSketch* sketch = sketchAlloc(&config);
    ENSURE(sketch);

    // Record 9 times: below threshold, no aging
    for (int i = 0; i < 9; i++)
    {
        wtlSketchRecord(sketch, spanToC(key));
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(key)), 9);

    // 10th record triggers aging: 10 halved = 5
    wtlSketchRecord(sketch, spanToC(key));
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(key)), 5);

    // Record 9 more: access counter was reset, no aging
    for (int i = 0; i < 9; i++)
    {
        wtlSketchRecord(sketch, spanToC(key));
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(key)), 14);

    // 10th record since last age triggers aging again: 14 halved = 7
    wtlSketchRecord(sketch, spanToC(key));
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(key)), 7);

    free(sketch);

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

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Record key A 8 times and key B 4 times
    for (int i = 0; i < 8; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyA));
    }

    for (int i = 0; i < 4; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyB));
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), 8);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), 4);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyC)), 0);

    // Age: A -> 4, B -> 2, C stays 0
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), 4);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), 2);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyC)), 0);

    // Age again: A -> 2, B -> 1, C still 0
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), 2);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), 1);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyC)), 0);

    free(sketch);

    return 0;
}

/*
* Verifies that wtlSketchReset clears all counters and the access
* counter, returning the sketch to a fresh state. Records two keys,
* resets, then confirms all estimates are zero and the sketch is
* usable again by recording and estimating a new key.
*/
static int Reset_ClearsAllState(void)
{
    span_t keyA = FromHexString("68656c6c6f", 5);
    span_t keyB = FromHexString("776f726c64", 5);

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Record both keys so counters are non-zero
    for (int i = 0; i < 5; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyA));
    }

    for (int i = 0; i < 3; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyB));
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), 5);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), 3);

    // Reset all state
    wtlSketchReset(sketch);

    // All estimates should be zero after reset
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), 0);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), 0);

    // Sketch should be usable again: record keyB and verify
    for (int i = 0; i < 4; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyB));
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyB)), 4);
    EXPECT_EQ(wtlSketchEstimate(sketch, spanToC(keyA)), 0);

    free(sketch);

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

    WtlSketch* s1 = sketchAlloc(&DefaultConfig);
    WtlSketch* s2 = sketchAlloc(&DefaultConfig);
    ENSURE(s1);
    ENSURE(s2);

    // Feed both sketches the same mixed sequence
    for (int i = 0; i < 7; i++)
    {
        wtlSketchRecord(s1, spanToC(keyA));
        wtlSketchRecord(s2, spanToC(keyA));
    }

    for (int i = 0; i < 3; i++)
    {
        wtlSketchRecord(s1, spanToC(keyB));
        wtlSketchRecord(s2, spanToC(keyB));
    }

    wtlSketchRecord(s1, spanToC(keyC));
    wtlSketchRecord(s2, spanToC(keyC));

    // Estimates must match across both sketches
    EXPECT_EQ(wtlSketchEstimate(s1, spanToC(keyA)), wtlSketchEstimate(s2, spanToC(keyA)));
    EXPECT_EQ(wtlSketchEstimate(s1, spanToC(keyB)), wtlSketchEstimate(s2, spanToC(keyB)));
    EXPECT_EQ(wtlSketchEstimate(s1, spanToC(keyC)), wtlSketchEstimate(s2, spanToC(keyC)));

    // Age both and verify they still match
    wtlSketchAge(s1);
    wtlSketchAge(s2);

    EXPECT_EQ(wtlSketchEstimate(s1, spanToC(keyA)), wtlSketchEstimate(s2, spanToC(keyA)));
    EXPECT_EQ(wtlSketchEstimate(s1, spanToC(keyB)), wtlSketchEstimate(s2, spanToC(keyB)));
    EXPECT_EQ(wtlSketchEstimate(s1, spanToC(keyC)), wtlSketchEstimate(s2, spanToC(keyC)));

    free(s1);
    free(s2);

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

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Record key A 20 times, key B 5 times
    for (int i = 0; i < 20; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyA));
    }

    for (int i = 0; i < 5; i++)
    {
        wtlSketchRecord(sketch, spanToC(keyB));
    }

    // A should be more popular than B before aging
    EXPECT_TRUE(wtlSketchEstimate(sketch, spanToC(keyA)) > wtlSketchEstimate(sketch, spanToC(keyB)));

    // First aging: A=10, B=2, ordering preserved
    wtlSketchAge(sketch);
    EXPECT_TRUE(wtlSketchEstimate(sketch, spanToC(keyA)) > wtlSketchEstimate(sketch, spanToC(keyB)));

    // Second aging: A=5, B=1, ordering still preserved
    wtlSketchAge(sketch);
    EXPECT_TRUE(wtlSketchEstimate(sketch, spanToC(keyA)) > wtlSketchEstimate(sketch, spanToC(keyB)));

    free(sketch);

    return 0;
}

int RunTests(void)
{
    RUN_TEST(IsValid_ValidConfig());
    RUN_TEST(IsValid_RejectsInvalidTable());
    RUN_TEST(Record_IncrementsAccessCount());
    RUN_TEST(Age_AndReset_ClearAccessCount());
    RUN_TEST(Record_WritesExactlyOneCounterPerRow());
    RUN_TEST(Seed_ChangesBucketPlacement());
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

    return 0;
}
