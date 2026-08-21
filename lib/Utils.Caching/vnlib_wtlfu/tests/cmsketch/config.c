/*
 * Copyright (c) 2026 Vaughn Nugent
 *
 * Library: VNLib
 * Package: vnlib_wtlfu
 * File: tests/cmsketch/config.c
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
* Verifies that a freshly allocated sketch is structurally valid:
* wtlSketchIsValid succeeds on the clean state produced by the
* caller-allocated layout.
*/
static int IsValid_FreshAllocation(void)
{
    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    EXPECT_EQ(wtlSketchIsValid(sketch), 0);

    free(sketch);

    return 0;
}

/*
* Verifies that wtlSketchIsValid continues to return valid (0) through
* normal use: after records, after aging, and after reset. A
* structurally sound sketch must not become invalid due to use.
*/
static int IsValid_RemainsValidAfterUse(void)
{
    WtlSketch* sketch = sketchAlloc(&DefaultConfig);
    ENSURE(sketch);

    // A sketch that has been in use is still structurally valid
    {
        wtlSketchRecord(sketch, HASH_HELLO);
        wtlSketchRecord(sketch, HASH_HELLO);
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
* Verifies that wtlSketchIsValid rejects every invalid configuration and
* table-layout condition the caller can produce. Since the sketch is now
* caller-allocated and self-initialized, validation is the caller's
* responsibility; this pins the exact error codes each bad state must
* yield so the contract stays stable.
*/
static int IsValid_RejectsInvalidConfigAndTable(void)
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
* Verifies that non-default config values are honored end to end. A
* sketch built from a custom depth, width, seed, and resetThreshold
* must carry those values in its exposed config, expose a table of the
* matching size, be structurally valid, and support a record/estimate
* round-trip. Guards against init paths that silently use defaults
* instead of the supplied config.
*/
static int Config_CustomConfigRespected(void)
{
    static const WtlSketchConfig customConfig = {
        .depth          = 3,
        .width          = 64,
        .seed           = 0xDEADBEEFCAFEBABEULL,
        .resetThreshold = 128
    };

    WtlSketch* sketch = sketchAlloc(&customConfig);
    ENSURE(sketch);

    // config must be carried through verbatim
    {
        EXPECT_EQ(sketch->config.depth, customConfig.depth);
        EXPECT_EQ(sketch->config.width, customConfig.width);
        EXPECT_EQ(sketch->config.seed, customConfig.seed);
        EXPECT_EQ(sketch->config.resetThreshold, customConfig.resetThreshold);
    }

    // table size must match the custom dimensions
    {
        EXPECT_EQ(spanGetSize(sketch->table), customConfig.depth * customConfig.width);
        EXPECT_EQ(wtlSketchIsValid(sketch), 0);
    }

    // record/estimate round-trip works under the custom config
    {
        for (int i = 0; i < 5; i++)
        {
            wtlSketchRecord(sketch, HASH_HELLO);
        }

        EXPECT_EQ(wtlSketchEstimate(sketch, HASH_HELLO), 5);
        EXPECT_EQ(wtlSketchEstimate(sketch, HASH_WORLD), 0);
    }

    free(sketch);

    return 0;
}
static int RunConfigTests(void)
{
    RUN_TEST(IsValid_FreshAllocation());
    RUN_TEST(IsValid_RemainsValidAfterUse());
    RUN_TEST(IsValid_RejectsInvalidConfigAndTable());
    RUN_TEST(Config_CustomConfigRespected());

    return 0;
}
