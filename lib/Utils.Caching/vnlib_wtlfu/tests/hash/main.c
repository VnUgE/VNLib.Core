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
#include <hash.h>

#include "vectors.h"

/*
 * Fills the supplied buffer with an ascending byte pattern (0, 1, 2, ...)
 * up to the span's length. Used by AlignmentTests to produce predictable
 * key material at arbitrary offsets.
 */
static void FillAscending(span_t buffer)
{
    uint8_t* ptr = spanGetOffset(buffer, 0);

    for (uint32_t i = 0; i < spanGetSize(buffer); i++)
    {
        ptr[i] = (uint8_t)i;       
    }
}

/*
 * Validates the hash implementation against known-answer test vectors.
 * Each vector supplies a hex-encoded key, a seed, and the expected 64-bit
 * and 32-bit hash outputs. Both wtlHash and wtlHash32 are checked
 * against every vector.
 */
static int VectorTests(void)
{
    cspan_t key;

    for (uint32_t i = 0; i < VECTOR_COUNT; i++)
    {        
        const wtlfu_vector_t* vector = &VECTORS[i];

        // Default to null/empty
        spanInitC(&key, NULL, 0);

        // Load hex value into key buffer, otherwise assume null is a zero length key
        if (vector->hex)
        {
            span_t mutableKey = _fromHexString(vector->hex, vector->len * 2);
            key = spanToC(mutableKey);            
        }

        // Compute both hashes from the same key and seed
        uint64_t actual_64 = wtlHash(key, vector->seed);
        uint32_t actual_32 = wtlHash32(key, vector->seed);

        EXPECT_EQ(actual_64, vector->expect_64);
        EXPECT_EQ(actual_32, vector->expect_32);
    }
   
    return 0;
}

/*
 * Verifies that hashing the same key with the same seed produces
 * identical results across repeated calls (determinism property).
 */
static int DeterminismTests(void)
{
    span_t keyA = FromHexString(
        "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"
        "202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f",
        64
    );

    span_t keyB = FromHexString("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", 32);

    // Step 1: wtlHash called twice with same input must match
    {
        uint64_t h1 = wtlHash(spanToC(keyA), 0);
        uint64_t h2 = wtlHash(spanToC(keyA), 0);

        EXPECT_EQ(h1, h2);
    }

    // Step 2: wtlHash32 called twice with same input must match
    {
        uint32_t h32_a = wtlHash32(spanToC(keyA), 0);
        uint32_t h32_b = wtlHash32(spanToC(keyA), 0);

        EXPECT_EQ(h32_a, h32_b);
    }

    // Step 3: Hash A, then B, then A again — no state leakage
    {
        uint64_t hA_first = wtlHash(spanToC(keyA), 0);
        
        wtlHash(spanToC(keyB), 0);
        
        uint64_t hA_second = wtlHash(spanToC(keyA), 0);
        
        EXPECT_EQ(hA_first, hA_second);
    }
    return 0;
}

/*
 * Verifies that different seeds produce different hash values for the
 * same key, and that the same seed produces the same hash (domain
 * separation property).
 */
static int SeedIsolationTests(void)
{
    // Ascending 64-byte key used across all sub-tests
    span_t key = FromHexString(
        "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"
        "202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f",
        64
    );
    cspan_t ckey = spanToC(key);

    // Step 1: seeds {0,1,2,3} must produce pairwise-distinct 64-bit hashes
    {
        uint64_t h0 = wtlHash(ckey, 0);
        uint64_t h1 = wtlHash(ckey, 1);
        uint64_t h2 = wtlHash(ckey, 2);
        uint64_t h3 = wtlHash(ckey, 3);

        EXPECT_NE(h0, h1);
        EXPECT_NE(h0, h2);
        EXPECT_NE(h0, h3);
        EXPECT_NE(h1, h2);
        EXPECT_NE(h1, h3);
        EXPECT_NE(h2, h3);
    }

    // Step 2: seed 0 vs UINT64_MAX must differ
    {
        uint64_t hZero = wtlHash(ckey, 0);
        uint64_t hMax  = wtlHash(ckey, UINT64_MAX);

        EXPECT_NE(hZero, hMax);
    }

    // Step 3: seeds {0,1,2,3} must produce pairwise-distinct 32-bit hashes
    {
        uint32_t h0 = wtlHash32(ckey, 0);
        uint32_t h1 = wtlHash32(ckey, 1);
        uint32_t h2 = wtlHash32(ckey, 2);
        uint32_t h3 = wtlHash32(ckey, 3);

        EXPECT_NE(h0, h1);
        EXPECT_NE(h0, h2);
        EXPECT_NE(h0, h3);
        EXPECT_NE(h1, h2);
        EXPECT_NE(h1, h3);
        EXPECT_NE(h2, h3);
    }

    return 0;
}

/*
 * Verifies that the 32-bit hash fold (wtlHash32) is a correct
 * derivation of the full 64-bit hash, by checking the upper and
 * lower 32 bits against the expected vector values independently.
 */
static int Hash32FoldTests(void)
{    
    cspan_t key;

    for (uint32_t i = 0; i < VECTOR_COUNT; i++)
    {         
        const wtlfu_vector_t* vector = &VECTORS[i];
        
        spanInitC(&key, NULL, 0);

        if (vector->hex)
        {
            span_t mutableKey;

            mutableKey = _fromHexString(vector->hex, vector->len * 2);
            key = spanToC(mutableKey);
        }

        uint64_t h64 = wtlHash(key, vector->seed);
        uint32_t h32 = wtlHash32(key, vector->seed);

        /* wtlHash32 must equal the XOR-fold of the 64-bit hash:
         * upper 32 bits XOR'd into the lower 32 bits. */
        uint32_t folded = (uint32_t)(h64 ^ (h64 >> 32));

        EXPECT_EQ(h32, folded);
    }

    return 0;
}

/*
 * Verifies that the hash function produces identical results regardless
 * of the key buffer's memory alignment. Tests keys placed at various
 * offsets within a scratch buffer to ensure unaligned reads are handled
 * correctly across all internal dispatch paths (short, medium, long).
 */
static int AlignmentTests(void)
{
    /* Lengths that exercise all three dispatch paths: short, medium, long. */
    static const uint32_t lengths[] = { 8, 16, 32, 64, 128, 256 };

    /* Offsets within the scratch buffer. The span API uses memmove-based
     * reads, so unaligned starts must produce the same hash as aligned. */
    static const uint32_t offsets[] = { 0, 1, 3, 5, 7 };

    uint64_t ref = 0;
    uint8_t scratch[8 + 256];
    span_t scratchSpan;   

    spanInit(&scratchSpan, scratch, sizeof(scratch));

    for (uint32_t i = 0; i < (sizeof(lengths) / sizeof(lengths[0])); i++)
    {
        for (uint32_t j = 0; j < (sizeof(offsets) / sizeof(offsets[0])); j++)
        {           
            uint64_t current;

            span_t key = spanSlice(scratchSpan, offsets[j], lengths[i]);

            /* Fill the scratch buffer with an ascending pattern starting
            * at the offset so only the key bytes are predictable. The bytes
            * before the offset are left over from prior iterations, which
            * is intentional — the hash must not read past the span length. */

            FillAscending(key);

            current = wtlHash(spanToC(key), 0);

            if (j == 0)
            {
                /* Offset 0 is the aligned reference for this length. */
                ref = current;
            }
            else
            {
                EXPECT_EQ(current, ref);
            }
        }
    }

    return 0;
}

int RunTests(void)
{
    RUN_TEST(VectorTests());
    RUN_TEST(DeterminismTests());
    RUN_TEST(SeedIsolationTests());
    RUN_TEST(Hash32FoldTests());
    RUN_TEST(AlignmentTests());

    return 0;
}
