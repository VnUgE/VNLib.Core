
// estimate.c

#define TEST_GROUP_CMSKETCH_ESTIMATE 1

/*
* Verifies that two sketches created with identical configuration and
* fed the same sequence of records produce identical estimates for all
* hashes. This confirms the sketch is deterministic: same config + same
* input = same output, which is essential for reproducible behavior.
*/
static int Estimate_Determinism(void)
{
    WtlSketch* s1 = sketchAlloc(&DefaultConfig);
    WtlSketch* s2 = sketchAlloc(&DefaultConfig);
    ENSURE(s1);
    ENSURE(s2);

    // Feed both sketches the same mixed sequence
    for (int i = 0; i < 7; i++)
    {
        wtlSketchRecord(s1, HASH_HELLO);
        wtlSketchRecord(s2, HASH_HELLO);
    }

    for (int i = 0; i < 3; i++)
    {
        wtlSketchRecord(s1, HASH_WORLD);
        wtlSketchRecord(s2, HASH_WORLD);
    }

    {
        wtlSketchRecord(s1, HASH_FOOBAR);
        wtlSketchRecord(s2, HASH_FOOBAR);

        // Estimates must match across both sketches
        EXPECT_EQ(wtlSketchEstimate(s1, HASH_HELLO), wtlSketchEstimate(s2, HASH_HELLO));
        EXPECT_EQ(wtlSketchEstimate(s1, HASH_WORLD), wtlSketchEstimate(s2, HASH_WORLD));
        EXPECT_EQ(wtlSketchEstimate(s1, HASH_FOOBAR), wtlSketchEstimate(s2, HASH_FOOBAR));

    }

    // Age both and verify they still match
    {
        wtlSketchAge(s1);
        wtlSketchAge(s2);

        EXPECT_EQ(wtlSketchEstimate(s1, HASH_HELLO), wtlSketchEstimate(s2, HASH_HELLO));
        EXPECT_EQ(wtlSketchEstimate(s1, HASH_WORLD), wtlSketchEstimate(s2, HASH_WORLD));
        EXPECT_EQ(wtlSketchEstimate(s1, HASH_FOOBAR), wtlSketchEstimate(s2, HASH_FOOBAR));
    }

    free(s1);
    free(s2);

    return 0;
}

/*
* Verifies that a freshly created sketch returns 0 for any hash
* before any records have been made. All counters start at zero
* so the minimum across rows must be zero.
*/
static int Estimate_UnrecordedReturnsZero(void)
{
    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // No records have been made; every hash must estimate 0
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 0);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 0);

    free(sketch);

    return 0;
}

/*
* Verifies exact equality between record count and estimate for a single
* hash when no collisions are possible. Records the same hash several
* discrete counts (including up to 200), asserting the estimate equals the
* count after each stage. Also confirms an unrecorded hash estimates 0 and
* that resetting between stages clears the table.
*/
static int Estimate_SingleKeyExact(void)
{
    static const int counts[5] = { 1, 5, 50, 100, 200 };

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // before any records, every hash estimates 0
    {
        EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 0);
        EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 0);
    }

    for (int i = 0; i < 5; i++)
    {
        // record the hash up to the target count
        for (int j = 0; j < counts[i]; j++)
        {
            wtlSketchRecord(sketch, HASH_HELLO);
        }

        // estimate must match the exact record count
        EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), counts[i]);

        // an unrelated hash must remain untouched
        EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 0);

        // reset the sketch for the next iteration
        wtlSketchReset(sketch);
    }

    free(sketch);

    return 0;
}

/*
* Verifies that distinct hashes estimate their own record counts exactly
* and do not pollute one another. Combines two frequencies against a
* third unrecorded hash, then records ten additional distinct hashes once
* each and confirms each estimates 1.
*/
static int Estimate_MultipleKeysExact(void)
{
    static const uint32_t keys[10] = {
        0x00000001, 0x00000002, 0x00000003, 0x00000004, 0x00000005,
        0x00000006, 0x00000007, 0x00000008, 0x00000009, 0x0000000a
    };

    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // record two hashes with different frequencies
    for (int i = 0; i < 5; i++)
    {
        wtlSketchRecord(sketch, HASH_HELLO);
    }

    for (int i = 0; i < 3; i++)
    {
        wtlSketchRecord(sketch, HASH_WORLD);
    }

    // estimates should match exact record counts
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 5);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 3);

    // an unrecorded hash remains at zero
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_FOOBAR), 0);

    // record ten additional distinct hashes once each
    for (int i = 0; i < 10; i++)
    {
        wtlSketchRecord(sketch, keys[i]);
    }

    // each recorded hash should estimate exactly 1
    for (int i = 0; i < 10; i++)
    {
        EXPECT_EQ(wtlSketchEstimate(sketch, keys[i]), 1);
    }

    // an unrecorded hash still estimates 0
    EXPECT_EQ(wtlSketchEstimate(sketch, 0x000000ff), 0);

    free(sketch);

    return 0;
}

/*
* Verifies the Count-Min overestimation guarantee on a small table
* (width=16) where collisions are guaranteed. Each of 50 distinct hashes
* is recorded exactly i+1 times; every estimate must be at least the true
* record count. The test deliberately uses a collision-heavy layout so any
* broken minimum logic would show up as an underestimate.
*/
#define _ESTIMATE_MANY_NUM_KEYS 50
#define _MANY_KEY_HASH(i) (0x9E3779B9u * (uint32_t)((i) + 1))

static int Estimate_OverestimationGuarantee(void)
{
    int recordCounts[_ESTIMATE_MANY_NUM_KEYS];

    WtlSketchConfig config = DefaultConfig;
    config.width = 16;

    WtlSketch* sketch = sketchAlloc(&config);
    ENSURE(sketch);

    // Record hash i exactly i+1 times (1..num_keys)
    for (int i = 0; i < _ESTIMATE_MANY_NUM_KEYS; i++)
    {
        recordCounts[i] = i + 1;

        for (int j = 0; j < recordCounts[i]; j++)
        {
            wtlSketchRecord(sketch, _MANY_KEY_HASH(i));
        }
    }

    // Verify each hash's estimate is at least its record count
    for (int i = 0; i < _ESTIMATE_MANY_NUM_KEYS; i++)
    {
        uint32_t estimate = wtlSketchEstimate(sketch, _MANY_KEY_HASH(i));

        EXPECT_TRUE(estimate >= (uint32_t)recordCounts[i]);
    }

    free(sketch);

    return 0;
}

static int RunEstimateTests(void)
{
    RUN_TEST(Estimate_UnrecordedReturnsZero());
    RUN_TEST(Estimate_SingleKeyExact());
    RUN_TEST(Estimate_MultipleKeysExact());
    RUN_TEST(Estimate_OverestimationGuarantee());
    RUN_TEST(Estimate_Determinism());

    return 0;
}
