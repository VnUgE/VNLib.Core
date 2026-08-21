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

/*
* Arbitrary distinct non-zero 32-bit constants used as sketch inputs.
* The sketch is a black box over hashes; these stand in for whatever
* the caller's hash function produced. Actual key-to-hash correctness
* is covered by the hashtable and hash tests.
*/
#define HASH_HELLO  0x68656c6c
#define HASH_WORLD  0x776f726c
#define HASH_FOOBAR 0x666f6f62
#define HASH_BAZ    0x62617a62
#define HASH_QUX    0x71757871

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

#include "age.c"
#include "config.c"
#include "estimate.c"
#include "record.c"

/*
* Verifies that wtlSketchReset clears all counters and the access
* counter, returning the sketch to a fresh state. Records two hashes,
* resets, then confirms all estimates are zero, the access counter is
* cleared, and the sketch is usable again by recording and estimating a
* new hash.
*/
static int Reset_ClearsAllState(void)
{
    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Record both hashes so counters are non-zero
    for (int i = 0; i < 5; i++)
    {
        wtlSketchRecord(sketch, HASH_HELLO);
    }

    for (int i = 0; i < 3; i++)
    {
        wtlSketchRecord(sketch, HASH_WORLD);
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 5);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 3);

    // Reset all state
    wtlSketchReset(sketch);

    // All estimates should be zero after reset
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 0);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 0);

    // Access counter should also be cleared
    EXPECT_EQ(sketch->accessCount, 0);

    // Sketch should be usable again: record hash B and verify
    for (int i = 0; i < 4; i++)
    {
        wtlSketchRecord(sketch, HASH_WORLD);
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 4);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 0);

    free(sketch);

    return 0;
}

int RunTests(void)
{

    /* Validation */
    TEST_GROUP(RunConfigTests());

    /* Recording */
    TEST_GROUP(RunRecordTests());

    /* Estimation */
    TEST_GROUP(RunEstimateTests());

    /* Aging */
    TEST_GROUP(RunAgeTests());

    /* Reset */
    RUN_TEST(Reset_ClearsAllState());

    return 0;
}
