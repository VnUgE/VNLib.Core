/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: wtlfu.h
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
* wtlfu.h - W-TinyLFU cache library.
*
* A high-performance cache using the W-TinyLFU admission policy:
*   - A small window LRU (default 1% of capacity) accepts all new entries.
*   - When the window overflows, the evicted item is compared against the
*     main cache's probationary victim via a Count-Min Sketch frequency
*     estimate. The higher-frequency item wins admission.
*   - The main cache is a segmented LRU (probationary + protected).
*   - The sketch is aged (all counters halved) periodically to keep
*     frequency estimates fresh.
*
* THREAD SAFETY:
*   This library is NOT thread-safe. The caller must synchronize all
*   access if used from multiple threads.
*
* MEMORY MANAGEMENT:
*   All memory is allocated through a caller-supplied allocator. The
*   cache owns all key and value bytes (copied inline with each entry
*   in a single allocation). The caller's key/value buffers may be
*   freed or reused immediately after WtlCacheInsert returns.
*
* BORROWED REFERENCES:
*   WtlCacheGet returns a borrowed reference (const WtlValue*) into the
*   cache's internal memory. The caller MUST call WtlCacheReleaseValue when
*   done with the value. If an entry is evicted or removed while borrowed,
*   its memory is kept alive until the last reference is released.
*   A new WtlCacheGet or WtlCacheInsert for the same key will return the
*   new entry; holders of the old reference still see the old value bytes
*   until they release.
*
*   WtlCachePeek does NOT borrow (no Release needed) but the returned
*   pointer is only valid until the next cache mutation (Insert/Remove/
*   Get that triggers eviction).
*/

#ifndef VN_WTLFU_H
#define VN_WTLFU_H

#include <stddef.h>
#include <stdint.h>
#include "platform.h"

#ifdef __cplusplus
extern "C"
{
#endif

/* ---------- Error codes ---------- */

#define WTL_SUCCESS           0
#define WTL_ERROR             (-1)
#define WTL_ERR_NO_MEMORY     (-2)
#define WTL_ERR_INVALID_ARG   (-3)
#define WTL_ERR_NOT_FOUND     (-4)

/* ---------- Opaque handles ---------- */

typedef struct WtlCache WtlCache;

typedef struct WtlValue {
    const uint8_t* key;
    const uint8_t* value;
    uint32_t keyLen;
    uint32_t valueLen;
} WtlValue;

/* ---------- Configuration ---------- */

typedef struct wtl_config_struct
{
    /* Total byte budget for cached values (not counting per-entry overhead).
     * The cache will evict to stay under this budget. */
    uint32_t maxCapacityBytes;

    /* Window LRU as percentage of total capacity (1 = 1%).
     * Set to 0 for default (1%). Max 99. */
    uint32_t windowPct;

    /* Protected segment as percentage of main cache (80 = 80%).
     * Probationary gets the remainder (20%).
     * Set to 0 for default (80%). Max 99. */
    uint32_t protectedPct;

    /* Count-Min Sketch dimensions.
     * Width:  columns (buckets per row). More = higher resolution. 0 = 1024.
     * Depth:  rows (independent hash functions). More = fewer collisions. 0 = 4.
     * ResetThreshold: total accesses before aging (halve all counters). 0 = 10 * width. */
    uint32_t sketchWidth;
    uint32_t sketchDepth;
    uint32_t sketchResetThreshold;

} WtlConfig;

/* ---------- Public API ---------- */

/*
* Creates a new cache instance. The config struct is copied internally.
* The allocator pointer is retained (caller must keep it alive for the
* lifetime of the cache).
*
* @param config  Cache configuration (must not be NULL)
* @return New cache handle, or NULL on failure
*/
VNLIB_EXPORT WtlCache* VNLIB_CC WtlCacheCreate(const WtlConfig* config);

/*
* Destroys the cache and frees all internal memory through the allocator.
* Any outstanding borrowed references become dangling — the caller must
* call WtlCacheReleaseValue on all borrowed values before destroying.
*
* @param cache  Cache to destroy (may be NULL, no-op)
*/
VNLIB_EXPORT void VNLIB_CC WtlCacheDestroy(WtlCache* cache);

/*
* Inserts a key-value pair. The key and value bytes are copied into the
* cache's own memory via the allocator. If the key already exists, the
* old value is replaced (old entry is evicted, potentially deferred if
* currently borrowed).
*
* The insertion goes through the W-TinyLFU admission policy:
*   - New keys always enter the window LRU.
*   - When the window overflows, the evicted item is compared against
*     the main cache's probationary victim via the frequency sketch.
*   - If the new item has higher estimated frequency, it is admitted
*     (victim is evicted). Otherwise it is rejected entirely.
*
* @param cache     Cache handle
* @param key       Pointer to key bytes
* @param keyLen    Number of key bytes
* @param value     Pointer to value bytes
* @param valueSize Number of value bytes
* @return WTL_SUCCESS on success, WTL_ERROR on failure
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlCacheInsert(
    WtlCache* cache,
    const uint8_t* key,
    uint32_t keyLen,
    const void* value,
    uint32_t valueSize
);

/*
* Attempts to get a value by key. On hit:
*   - The entry's frequency is incremented in the sketch.
*   - If in probationary and accessed again, promoted to protected.
*   - If in window, frequency is recorded; entry stays in window until
*     it overflows (then faces admission).
*   - The entry's refcount is incremented (borrowed reference).
*   - *outValue points directly into the cache's memory (zero-copy).
*   - Caller MUST call WtlCacheReleaseValue when done with the pointer.
*
* @param cache     Cache handle
* @param key       Pointer to key bytes
* @param keyLen    Number of key bytes
* @param outValue  Receives a borrowed value reference on hit
* @return WTL_SUCCESS if the cache value is found, WTL_ERROR on failure
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlCacheGet(
    WtlCache* cache, 
    const uint8_t* key, 
    uint32_t keyLen, 
    WtlValue** outValue
);

/*
* Releases a borrowed reference obtained from WtlCacheGet.
* Decrements the entry's refcount. If refcount reaches 0 and the entry
* was marked for eviction while borrowed, it is freed now.
*
* @param cache  Cache handle
* @param value  The borrowed value reference obtained from WtlCacheGet
* @return WTL_SUCCESS on successful release, WTL_ERROR otherwise
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlCacheReleaseValue(WtlCache* cache, const WtlValue* value);

/*
* Checks for a key's existence without affecting frequency or recency.
* Returns a value pointer valid only until the next cache mutation
* (Insert, Remove, or Get that triggers eviction). Does NOT borrow —
* no WtlCacheReleaseValue call needed.
*
* @param cache     Cache handle
* @param key       Pointer to key bytes
* @param keyLen    Number of key bytes
* @param outValue  Receives a value pointer valid until next mutation
* @return WTL_SUCCESS on hit, WTL_ERR_NOT_FOUND on miss
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlCachePeek(
    WtlCache* cache, 
    const uint8_t* key, 
    uint32_t keyLen, 
    WtlValue** outValue
);

/*
* Explicitly removes an entry by key. If the entry is currently borrowed
* (refcount > 0), it is marked for eviction and freed when the last
* reference is released.
*
* @param cache   Cache handle
* @param key     Pointer to key bytes
* @param keyLen  Number of key bytes
* @return WTL_SUCCESS on success, WTL_ERR_NOT_FOUND if key not present
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlCacheRemove(
    WtlCache* cache,
    const uint8_t* key,
    uint32_t keyLen
);

/*
* Evicts all entries. Entries currently borrowed (refcount > 0) are
* marked for eviction and freed when their last reference is released.
*
* @param cache  Cache handle
* @return WTL_SUCCESS on successful call, error code otherwise
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlCacheClear(WtlCache* cache);

/*
* Returns the current total bytes of cached values (not including
* per-entry overhead).
*
* @param cache  Cache handle
* @return Current value byte usage
*/
VNLIB_EXPORT uint32_t VNLIB_CC WtlCacheGetSize(WtlCache* cache);

/*
* Manually triggers sketch aging (halve all counters). Normally
* automatic, but exposed for testing and observability.
*
* @param cache  Cache handle
* @return WTL_SUCCESS on successful call, error code otherwise
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlCacheAgeSketch(WtlCache* cache);

/* ---------- Value accessors ----------
*
* These provide read-only access to a borrowed value's bytes.
* The pointers are valid for the lifetime of the borrow (until
* WtlCacheReleaseValue is called) or until the cache is destroyed.
*/

VNLIB_EXPORT uint32_t VNLIB_CC WtlValueGetData(const WtlValue* val,  const void** dataOut);

#ifdef __cplusplus
}
#endif

#endif /* !VN_WTLFU_H */
