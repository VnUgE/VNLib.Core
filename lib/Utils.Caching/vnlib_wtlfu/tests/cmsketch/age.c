/*
 * Copyright (c) 2026 Vaughn Nugent
 *
 * Library: VNLib
 * Package: vnlib_wtlfu
 * File: tests/cmsketch/age.c
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
* Verifies that wtlSketchAge halves every counter in the table.
* Records two hashes: one to a known small value (8) and one saturated
* to 255. After each aging call, estimates are checked against the
* expected halved values. Integer division rounds down, so 8 -> 4 ->
* 2 -> 1 -> 0, and 255 -> 127 -> 63.
*/
static int Age_HalvesCounters(void)
{
    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Record hash A exactly 8 times
    for (int i = 0; i < 8; i++)
    {
        wtlSketchRecord(sketch, HASH_HELLO);
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 8);

    // Saturate hash B to 255
    for (int i = 0; i < 300; i++)
    {
        wtlSketchRecord(sketch, HASH_WORLD);
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), UINT8_MAX);

    // First aging: 8 -> 4, 255 -> 127
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 4);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 127);

    // Second aging: 4 -> 2, 127 -> 63
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 2);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 63);

    // Third aging: 2 -> 1, 63 -> 31
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 1);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 31);

    // Fourth aging: 1 -> 0, 31 -> 15
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 0);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 15);

    free(sketch);

    return 0;
}

/*
* Verifies that aging decays all hashes uniformly, not just the most
* recently recorded one. Records hash A 8 times and hash B 4 times, then
* ages and checks both are halved. An unrecorded hash C must remain at 0
* (0 >> 1 == 0, no underflow).
*/
static int Age_AffectsAllKeysUniformly(void)
{
    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Record hash A 8 times and hash B 4 times
    for (int i = 0; i < 8; i++)
    {
        wtlSketchRecord(sketch, HASH_HELLO);
    }

    for (int i = 0; i < 4; i++)
    {
        wtlSketchRecord(sketch, HASH_WORLD);
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 8);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 4);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_FOOBAR), 0);

    // Age: A -> 4, B -> 2, C stays 0
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 4);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 2);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_FOOBAR), 0);

    // Age again: A -> 2, B -> 1, C still 0
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 2);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 1);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_FOOBAR), 0);

    free(sketch);

    return 0;
}

/*
* Verifies that aging decays all hashes uniformly, preserving the
* relative popularity ordering. Records hash A 20 times and hash B
* 5 times, then ages twice. After each aging, A must still have a
* higher estimate than B. This confirms aging does not corrupt the
* frequency ordering, which is critical for the W-TinyLFU admission
* policy that compares estimates to decide which item survives.
*/
static int Age_PreservesRelativeOrdering(void)
{
    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Record hash A 20 times, hash B 5 times
    for (int i = 0; i < 20; i++)
    {
        wtlSketchRecord(sketch, HASH_HELLO);
    }

    for (int i = 0; i < 5; i++)
    {
        wtlSketchRecord(sketch, HASH_WORLD);
    }

    // A should be more popular than B before aging
    EXPECT_TRUE(wtlSketchEstimate(sketch, HASH_HELLO) > wtlSketchEstimate(sketch, HASH_WORLD));

    // First aging: A=10, B=2, ordering preserved
    wtlSketchAge(sketch);
    EXPECT_TRUE(wtlSketchEstimate(sketch, HASH_HELLO) > wtlSketchEstimate(sketch, HASH_WORLD));

    // Second aging: A=5, B=1, ordering still preserved
    wtlSketchAge(sketch);
    EXPECT_TRUE(wtlSketchEstimate(sketch, HASH_HELLO) > wtlSketchEstimate(sketch, HASH_WORLD));

    free(sketch);

    return 0;
}

/*
* Verifies aging is visible through the public estimate path, not just
* raw counters. Records a hash 10 times, ages once, and confirms
* wtlSketchEstimate returns exactly 5. Catches regressions where the
* table halving works but the estimate path reads stale values.
*/
static int Age_EstimateReflectsHalvedCounters(void)
{
    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Record hash A exactly 10 times
    for (int i = 0; i < 10; i++)
    {
        wtlSketchRecord(sketch, HASH_HELLO);
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 10);

    // A single aging halves 10 -> 5, visible through estimate
    wtlSketchAge(sketch);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 5);

    free(sketch);

    return 0;
}

/*
* Verifies the automatic aging lifecycle through the exposed accessCount
* field. The counter increments by one for every wtlSketchRecord call,
* resets to zero exactly when resetThreshold records are reached (triggering
* aging), and starts a fresh cycle afterward. Uses a small threshold (10)
* so both boundaries are reachable in a handful of records.
*/
static int Age_AccessCounter_AutoResetAtThreshold(void)
{
    WtlSketchConfig config = DefaultConfig;
    config.resetThreshold = 10;

    WtlSketch* sketch = sketchAlloc(&config);
    ENSURE(sketch);

    // first cycle: record 9 times, counter tracks each record
    {
        for (uint32_t i = 1; i <= 9; i++)
        {
            wtlSketchRecord(sketch, HASH_HELLO);
            EXPECT_EQ(sketch->accessCount, i);
        }
    }

    // 10th record hits the threshold: aging fires and the counter resets
    {
        wtlSketchRecord(sketch, HASH_HELLO);
        EXPECT_EQ(sketch->accessCount, 0);
        EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 5);
    }

    // second cycle: counting resumes from zero
    {
        for (uint32_t i = 1; i <= 9; i++)
        {
            wtlSketchRecord(sketch, HASH_HELLO);
            EXPECT_EQ(sketch->accessCount, i);
        }
    }

    // 10th record of the second cycle triggers aging again
    {
        wtlSketchRecord(sketch, HASH_HELLO);
        EXPECT_EQ(sketch->accessCount, 0);
        EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 7);
    }

    free(sketch);

    return 0;
}

/*
* Verifies that a manual wtlSketchAge call resets the exposed accessCount
* back to zero, so the next record starts a fresh aging cycle. Also
* confirms the counter is untouched by Estimate, which is a read-only
* operation. (Reset clearing the counter is covered by
* Reset_ClearsAllState.)
*/
static int Age_AccessCounter_Manual(void)
{
    WtlSketchConfig config = DefaultConfig;
    config.resetThreshold = 100;

    WtlSketch* sketch = sketchAlloc(&config);
    ENSURE(sketch);

    // build up a known non-zero access count
    {
        for (int i = 0; i < 7; i++)
        {
            wtlSketchRecord(sketch, HASH_HELLO);
        }
        EXPECT_EQ(sketch->accessCount, 7);
    }

    // estimate must not disturb the access count
    {
        wtlSketchEstimate(sketch, HASH_HELLO);
        EXPECT_EQ(sketch->accessCount, 7);
    }

    // manual aging clears the access count (counters are halved)
    {
        wtlSketchAge(sketch);
        EXPECT_EQ(sketch->accessCount, 0);
    }

    // a fresh cycle counts from zero
    {
        wtlSketchRecord(sketch, HASH_HELLO);
        wtlSketchRecord(sketch, HASH_HELLO);
        EXPECT_EQ(sketch->accessCount, 2);
    }

    free(sketch);

    return 0;
}

static int RunAgeTests(void)
{
    RUN_TEST(Age_HalvesCounters());
    RUN_TEST(Age_AffectsAllKeysUniformly());
    RUN_TEST(Age_PreservesRelativeOrdering());
    RUN_TEST(Age_EstimateReflectsHalvedCounters());
    RUN_TEST(Age_AccessCounter_AutoResetAtThreshold());
    RUN_TEST(Age_AccessCounter_Manual());

    return 0;
}
