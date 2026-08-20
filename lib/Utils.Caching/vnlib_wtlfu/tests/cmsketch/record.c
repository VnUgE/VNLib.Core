
// record.c

#define TEST_GROUP_CMSKETCH_RECORD 1

/*
* Verifies the physical table layout behind the exposed table span:
* recording a single hash must increment exactly one counter per row
* (depth non-zero bytes in total), and the sum of all counters must
* equal the record count. With the default config a single hash cannot
* collide with itself, so any extra non-zero byte would mean writes
* landed outside the span the span claims to own.
*/
static int Record_WritesExactlyOneCounterPerRow(void)
{
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

            if (v != 0)
            {
                nonZero++;
            }
        }

        EXPECT_EQ(nonZero, 0);
        EXPECT_EQ(sum, 0);
    }

    // record the hash a few times
    {
        for (int i = 0; i < 6; i++)
        {
            wtlSketchRecord(sketch, HASH_HELLO);
        }
    }

    // scan again: exactly one counter per row is hot, each holding the record count
    {
        uint32_t nonZero = 0, sum = 0;

        for (uint32_t i = 0; i < spanGetSize(sketch->table); i++)
        {
            uint8_t v = *spanGetOffset(sketch->table, i);
            sum += v;

            if (v != 0)
            {
                nonZero++;
            }
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
* same hash sequence to the same counter distribution. Both sketches are
* recorded the identical sequence of hashes, then their raw tables are
* compared: a working seed scatters the hashes to different columns, so
* the tables must differ. The tables can only be byte-identical if every
* hash landed in the same column in both sketches, which has negligible
* probability (roughly (1/width)^hashes) when the seed is honored, while
* an ignored seed would produce identical tables with certainty.
*/
static int Record_SeedChangesBucketPlacement(void)
{
    static const WtlSketchConfig smallConfig = {
        .depth = 4,
        .width = 32,
        .seed = WTL_SKETCH_BASE_SEED,
        .resetThreshold = WTL_SKETCH_DEFAULT_RESET_MULT * 32
    };

    static const uint32_t keys[8] = {
        HASH_HELLO, HASH_WORLD, HASH_FOOBAR, HASH_BAZ,
        HASH_QUX, 0x666f7a01, 0x61626302, 0x64656603
    };

    static const uint32_t recordCounts[8] = { 3, 4, 5, 2, 3, 4, 5, 2 };

    WtlSketchConfig altConfig = smallConfig;
    altConfig.seed = WTL_SKETCH_BASE_SEED + 1;

    WtlSketch* sa = sketchAlloc(&smallConfig);
    WtlSketch* sb = sketchAlloc(&altConfig);
    ENSURE(sa);
    ENSURE(sb);

    // record the same hash sequence into both sketches
    {
        for (int i = 0; i < 8; i++)
        {
            for (uint32_t j = 0; j < recordCounts[i]; j++)
            {
                wtlSketchRecord(sa, keys[i]);
                wtlSketchRecord(sb, keys[i]);
            }
        }
    }

    // differing seeds must scatter hashes to different columns, so the
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
* Records a single hash well beyond the uint8_t counter maximum (255)
* and verifies the estimate saturates at 255 rather than wrapping to
* zero. This exercises the saturation guard in wtlSketchRecord.
*
* Note: the default resetThreshold (10 * 1024 = 10240) is well above
* 300 records, so aging will not fire during this test.
*/
static int Record_Saturation(void)
{
    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // fill but ensure saturation does not occur until exactly UINT8_MAX
    for (int i = 0; i < UINT8_MAX - 1; i++)
    {
        wtlSketchRecord(sketch, HASH_HELLO);

        // ensure count remains below the max size
        ENSURE(wtlSketchEstimate(sketch, HASH_HELLO) < UINT8_MAX);
    }

    // Final record saturates the counter
    wtlSketchRecord(sketch, HASH_HELLO);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), UINT8_MAX);

    // Ensure any number of records after max remains at UINT8_MAX
    for (int i = 0; i < 10; i++)
    {
        wtlSketchRecord(sketch, HASH_HELLO);
        EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), UINT8_MAX);
    }

    free(sketch);

    return 0;
}

/*
* Verifies that recording one hash does not affect another hash's
* estimate. Hash A is saturated to 255, hash B is recorded only 3
* times, and each must estimate independently.
*/
static int Record_KeyIsolation(void)
{
    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // Saturate hash A
    for (int i = 0; i < 300; i++)
    {
        wtlSketchRecord(sketch, HASH_HELLO);
    }

    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), UINT8_MAX);

    // Record hash B a small number of times
    for (int i = 0; i < 3; i++)
    {
        wtlSketchRecord(sketch, HASH_WORLD);
    }

    // Hash A remains saturated, hash B estimates exactly 3
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), UINT8_MAX);
    EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 3);

    free(sketch);

    return 0;
}

static int RunRecordTests(void)
{
    RUN_TEST(Record_WritesExactlyOneCounterPerRow());
    RUN_TEST(Record_SeedChangesBucketPlacement());
    RUN_TEST(Record_Saturation());
    RUN_TEST(Record_KeyIsolation());

    return 0;
}
