/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: hash.c
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
* hash.c - xxHash64-style non-cryptographic 64-bit hash for cache keys.
*
* Produces a 64-bit hash satisfying the strict avalanche criterion
* (SAC): flipping any single input bit changes each output bit with
* probability ~0.5. This decorrelates similar keys so they distribute
* uniformly across hash table buckets and Count-Min Sketch rows.
*
* The algorithm folds input bytes into accumulator state through
* repeated multiply-rotate-multiply rounds (the ROUND macro). Each
* round is an irreversible mixing step that spreads new input bits
* across the full accumulator width. Multiple rounds over different
* input words build up a state where every output bit depends on
* every input bit. A final avalanche pass enforces SAC on the
* accumulator output.
*
* Three-tier dispatch by input length:
*   - Short  (0-16 B):   one accumulator, reads widest chunk that fits
*   - Medium (17-128 B):  one accumulator, 32 bytes chunks, strip end
*   - Long   (>128 B):   four lane accumulators, striped lanes, merge
*
* The mixing constants (PRIME_1..PRIME_5), the ROUND/MERGE
* structure, and the final avalanche sequence are derived from the
* xxHash64 reference implementation by Yann Collet, used under the
* BSD-2-Clause license. See licenses/xxhash.txt for the full text.
*
* Reference: https://github.com/Cyan4973/xxHash
*/

#include "hash.h"

/*
* Odd prime constants derived from the golden ratio (2^64 / phi).
* Multiplication by an odd constant is a bijection on uint64_t — it
* preserves all information while redistributing bits. The different
* primes serve specific mixing roles:
*   PRIME_1, PRIME_2: primary multipliers in the ROUND mixing step
*   PRIME_3:          avalanche multiplier (final output scrambling)
*   PRIME_4:          additive seed offset for long-path accumulators
*   PRIME_5:          multiplier for single-byte tail folding
*/
#define VN_SKETCH_PRIME_1  0x9E3779B185EBCA87ULL
#define VN_SKETCH_PRIME_2  0xC2B2AE3D27D4EB4FULL
#define VN_SKETCH_PRIME_3  0x165667B19E3779F9ULL
#define VN_SKETCH_PRIME_4  0x85EBCA77C2B2AE63ULL
#define VN_SKETCH_PRIME_5  0x27D4EB2F165667C5ULL

/*
* Circular bit rotation (shift-wrap). Moves bits from the high end
* to the low end so that repeated multiplications don't concentrate
* new information in only the high word.
*
* perf: All rotation amounts are compile-time constants, so compilers will
* fold this into a single ROL instruction on x86-64.
*/
#define VN_SKETCH_ROTL(x, r) (((x) << (r)) | ((x) >> (64 - (r))))

/*
* ROUND: the core mixing step. Folds one 64-bit input word (val) into
* the accumulator (acc). The multiply-rotate-multiply sequence is
* irreversible — after enough rounds, each accumulator bit depends on
* every bit of every input word processed so far.
*/
#define VN_SKETCH_ROUND(acc, val) \
    { \
        (acc) += (val) * VN_SKETCH_PRIME_2; \
        (acc)  = VN_SKETCH_ROTL((acc), 31); \
        (acc) *= VN_SKETCH_PRIME_1; \
    }

/*
* MERGE: combines one accumulator lane into the result. The lane is
* scrambled the same way as a ROUND, then XOR'd into the result. XOR
* (rather than addition) produces an unbiased union of the two bit
* distributions — if both lanes have bit b set, the result clears it
* rather than carrying into the next bit.
*/
#define VN_SKETCH_MERGE(acc, val) \
    { \
        (val) *= VN_SKETCH_PRIME_2; \
        (val)  = VN_SKETCH_ROTL((val), 31); \
        (val) *= VN_SKETCH_PRIME_1; \
        (acc) ^= (val); \
        (acc)  = ((acc) * VN_SKETCH_PRIME_1) + VN_SKETCH_PRIME_4; \
    }

/*
* Read 8 bytes as a little-endian uint64_t from a sub-span at the
* given offset. Uses spanReadC which dispatches to memmove (or
* memmove_s on Windows) so the compiler can emit a single unaligned
* load on targets that support it.
*/
static _vn_inline uint64_t _wtlRead64(cspan_t key, uint32_t offset)
{
    uint64_t val = 0;

    spanReadC(
        spanSliceC(key, offset, sizeof(uint64_t)), 
        (uint8_t*)&val,
        sizeof(uint64_t)
    );

    return val;
}

/*
* Read 4 bytes as a little-endian uint32_t from a sub-span at the
* given offset. Same span-based strategy as _wtlRead64.
*/
static _vn_inline uint32_t _wtlRead32(cspan_t key, uint32_t offset)
{
    uint32_t val = 0;

    spanReadC(
        spanSliceC(key, offset, sizeof(uint32_t)), 
        (uint8_t*)&val,
        sizeof(uint32_t)
    );
    
    return val;
}

/*
* Avalanche: the final mixing step applied to the accumulator output.
* Three rounds of multiply-shift ensure every input bit influences
* every output bit with P~0.5, satisfying the strict avalanche
* criterion. Without this, bits in the high word of the accumulator
* have disproportionate influence on the hash output.
*/
static _vn_inline uint64_t _wtlAvalanche(uint64_t hash)
{
    hash ^= hash >> 33;
    hash *= VN_SKETCH_PRIME_2;
    hash ^= hash >> 29;
    hash *= VN_SKETCH_PRIME_3;
    hash ^= hash >> 32;
    return hash;
}

/*
* Hash inputs of 0-16 bytes. Uses a single accumulator seeded with
* PRIME_5 + len, then folds in the widest chunk(s) that fit.
*
* Two tiers by length:
*   >  8: first 8 + last 8  (overlap for 9-15 is harmless)
*   4- 8: first 4 + last 4  (overlap for 5-8 is harmless)
*   1- 3: individual bytes via fall-through switch
*/
static uint64_t _wtlHashShort(cspan_t key, uint64_t seed)
{
    uint64_t hash;

    hash = seed + VN_SKETCH_PRIME_5 + spanGetSizeC(key);

    if (spanGetSizeC(key) > 8)
    {
        /* First 8 + last 8: overlap for 9-15 bytes is harmless.
         * Both endpoints are folded into the accumulator so all
         * bytes are captured without branching on exact length. */
        hash += _wtlRead64(key, 0);
        VN_SKETCH_ROUND(hash, _wtlRead64(key, spanGetSizeC(key) - 8));
    }
    else if (spanGetSizeC(key) >= 4)
    {
        /* First 4 + last 4: overlap for 5-8 bytes is harmless.
         * The first 4 are multiplied into the accumulator, the
         * last 4 go through a full ROUND. */
        hash += (uint64_t)_wtlRead32(key, 0) * VN_SKETCH_PRIME_1;
        VN_SKETCH_ROUND(hash, (uint64_t)_wtlRead32(key, spanGetSizeC(key) - 4));
    }
    else if (spanGetSizeC(key) > 0)
    {
        /* 1-3 bytes: fold each byte individually with shift by position. */
        switch (spanGetSizeC(key))
        {
            case 3: hash += (uint64_t)spanGetOffsetC(key, 2)[0] << 16;  /* fall-through */
            case 2: hash += (uint64_t)spanGetOffsetC(key, 1)[0] << 8;   /* fall-through */
            case 1: hash += (uint64_t)spanGetOffsetC(key, 0)[0];         /* fall-through */
                    hash  = VN_SKETCH_ROTL(hash, 11) * VN_SKETCH_PRIME_1; break;
            default: break;
        }
    }

    return _wtlAvalanche(hash);
}

/*
* Hash inputs of 17-128 bytes. Processes 32-byte blocks through one
* accumulator, then uses last-stretch overlap to cover the remainder.
*/
static uint64_t _wtlHashMedium(cspan_t key, uint64_t seed)
{
    uint32_t offset     = 0;
    uint64_t acc        =  seed + VN_SKETCH_PRIME_1;

    /* Process 32-byte blocks: 4 words of 8 bytes each per round.
     * Offset-based iteration preserves the original span for the
     * last-stretch overlap below. */

    while (offset + 32 <= spanGetSizeC(key))
    {
        VN_SKETCH_ROUND(acc, _wtlRead64(key, offset));
        VN_SKETCH_ROUND(acc, _wtlRead64(key, offset + 8));
        VN_SKETCH_ROUND(acc, _wtlRead64(key, offset + 16));
        VN_SKETCH_ROUND(acc, _wtlRead64(key, offset + 24));
        offset += 32;
    }

    /* Last stretch: always process the final 16 bytes. Input is
     * >= 17 bytes so the last 16 is always valid. Overlap with
     * already-processed blocks is harmless (ROUND is a bijection). */
    VN_SKETCH_ROUND(acc, _wtlRead64(key, spanGetSizeC(key) - 16));
    VN_SKETCH_ROUND(acc, _wtlRead64(key, spanGetSizeC(key) - 8));

    /* If more than 16 bytes remain after the block loop, cover the
     * gap between offset and the last stretch. */
    if (offset + 16 < spanGetSizeC(key))
    {
        VN_SKETCH_ROUND(acc, _wtlRead64(key, offset));

        if (offset + 24 < spanGetSizeC(key))
        {
            VN_SKETCH_ROUND(acc, _wtlRead64(key, offset + 8));
        }
    }

    /* Fold the total length so inputs with identical content but
     * different sizes produce different hashes. */
    acc += spanGetSizeC(key);

    return _wtlAvalanche(acc);
}

/*
* Hash inputs >128 bytes. Uses four parallel accumulators (lanes) that
* each consume a different 16-byte slice of every 64-byte block. The
* lanes are seeded with offset values so identical 16-byte blocks
* appearing in different lanes produce different states. After all
* blocks are consumed, the lanes are merged into a single result.
*/
static uint64_t _wtlHashLong(cspan_t key, uint64_t seed)
{
    uint32_t offset = 0;
    uint64_t lane0, lane1, lane2, lane3, result;

    /* Four lanes seeded with different offsets so they diverge immediately. */

    lane0 = seed + VN_SKETCH_PRIME_1 + VN_SKETCH_PRIME_2;
    lane1 = seed + VN_SKETCH_PRIME_2;
    lane2 = seed;
    lane3 = seed - VN_SKETCH_PRIME_1;

    /* Consume 64-byte blocks: each lane gets a different 8-byte word.
     * Offset-based iteration preserves the original span for the
     * last-stretch overlap below. */

    while (offset + 64 <= spanGetSizeC(key))
    {
        VN_SKETCH_ROUND(lane0, _wtlRead64(key, offset));
        VN_SKETCH_ROUND(lane1, _wtlRead64(key, offset + 8));
        VN_SKETCH_ROUND(lane2, _wtlRead64(key, offset + 16));
        VN_SKETCH_ROUND(lane3, _wtlRead64(key, offset + 24));
        VN_SKETCH_ROUND(lane0, _wtlRead64(key, offset + 32));
        VN_SKETCH_ROUND(lane1, _wtlRead64(key, offset + 40));
        VN_SKETCH_ROUND(lane2, _wtlRead64(key, offset + 48));
        VN_SKETCH_ROUND(lane3, _wtlRead64(key, offset + 56));
        offset += 64;
    }

    /* Merge the four lanes into a single accumulator. Each lane is
     * scrambled and XOR'd into the result. Different rotation amounts
     * per lane prevent bit alignment between the merged states. */

    result = VN_SKETCH_ROTL(lane0, 1) + VN_SKETCH_ROTL(lane1, 7)
            + VN_SKETCH_ROTL(lane2, 12) + VN_SKETCH_ROTL(lane3, 18);

    /* always process the final 32 bytes. Covers remaining 0-63 bytes — overlap 
     * with already-processed blocks is harmless because ROUND is a bijection. 
     * Fixed offsets from the end of the original span, no cascading tail loops.
     */

    VN_SKETCH_ROUND(result, _wtlRead64(key, spanGetSizeC(key) - 32));
    VN_SKETCH_ROUND(result, _wtlRead64(key, spanGetSizeC(key) - 24));
    VN_SKETCH_ROUND(result, _wtlRead64(key, spanGetSizeC(key) - 16));
    VN_SKETCH_ROUND(result, _wtlRead64(key, spanGetSizeC(key) - 8));

    /* If more than 32 bytes remain after the block loop, cover the
     * gap between offset and the last stretch with another 32 bytes. */

    if (offset + 32 < spanGetSizeC(key))
    {
        VN_SKETCH_ROUND(result, _wtlRead64(key, offset));
        VN_SKETCH_ROUND(result, _wtlRead64(key, offset + 8));
        VN_SKETCH_ROUND(result, _wtlRead64(key, offset + 16));
        VN_SKETCH_ROUND(result, _wtlRead64(key, offset + 24));
    }

    //Fold the total length
    result += spanGetSizeC(key);

    return _wtlAvalanche(result);
}

/*
* Public entry point. Dispatches to short, medium, or long path based
* on input size.
*/
vnlib_fn_internal uint64_t wtlHash(cspan_t key, uint64_t seed)
{
    uint32_t len = spanGetSizeC(key);

    if (len <= 16)
    {
        return _wtlHashShort(key, seed);
    }
    else if (len <= 128)
    {
        return _wtlHashMedium(key, seed);
    }
    else
    {
        return _wtlHashLong(key, seed);
    }
}

/*
* 32-bit hash. Computes the full 64-bit hash then folds it to 32 bits
* by XOR of the high and low halves. XOR-fold preserves the entropy
* of both halves reducing collision.
*/
vnlib_fn_internal uint32_t wtlHash32(cspan_t key, uint64_t seed)
{
    uint64_t hash = wtlHash(key, seed);
    return (uint32_t)(hash ^ (hash >> 32));
}
