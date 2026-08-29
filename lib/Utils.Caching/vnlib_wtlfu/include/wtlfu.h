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
* A minimal, high-performance cache/store using the W-TinyLFU admission 
*  policy:
*   - A small window LRU accepts all new entries.
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
*   Internal data structures are allocated once and stored inline with 
*   the main cache data-structure. It is a fixed size block for the duration
*   of the cache lifecycle. It's size is dependent on the configuration.
* 
*   Users allocate the cache structure after calling WtlGetMemorySize
*   and pass it to WtlInit for startup initialization. 
* 
*   During insert, a WtlValue is passed and stored internally, callers must keep 
*   key and value memory alive for the lifecycle of the value. 
* 
*   Otherwise WtlKey operations are general read-while-in-use and otherwise not 
*   stored.
*
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

/*
* Experimental internal compatibility level. Incremented when the library
* has been modified in a non-compatible way. Hiding compatible features
* for a given library compat version.
*/
#define WTL_COMPAT_VERSION 1

/* ---------- Error codes ---------- */

#define WTL_SUCCESS             0
#define WTL_ERROR               (-1)
#define WTL_ERR_NO_MEMORY       (-2)
#define WTL_ERR_INVALID_ARG     (-3)
#define WTL_ERR_NOT_FOUND       (-4)
#define WTL_ERR_DUPLICATE       (-5)
#define WTL_ERR_WILL_EVICT      (-6)

#define WTL_ITEM_EVICTED        (1)

/*
* The minimum size capacity allowed by library. Capacity below 
* this value will be rejected.
*/
#define WTL_MIN_CAPACITY        16

/*
* Opaque cache data structure. The primary handle/context for a cache
* store. Use WtlGetMemorySize() to get the size of this structure
* at runtime for the desired configuration, then initialize the store 
* with WtlInit().
* 
* This handle holds memory for the entire store for the entire lifetime 
* of the store. 
*/
typedef struct WtlCtx WtlCtx;

typedef struct WtlValue 
{
    /* A pointer to the key memory to store */
    const uint8_t* key;

    /* An opaque pointer to the arbitrary data to store for this key*/
    const void* value;

    /* The length of the key memory */
    uint32_t keyLen;

} WtlValue;

/*
* Represents a key to a cache item, can be used to reference
* existing stored values. 
*/
typedef struct WtlKey 
{
    /* A pointer to the key memory to use */
    const uint8_t* key;

    /* The length of the key memory to read */
    uint32_t len;

} WtlKey;

/* ---------- Configuration ---------- */

typedef struct wtl_config_struct
{
    /* 
     * Maximum number of elements allowed in the table
     */
    uint32_t capacity;
 
    /*
    * Percentage of capacity that will be used for the window
    * lru
    */
    uint32_t windowPct;

    /*
    * Percentage of main cache used for (capacity - window) used for 
    * protected or long-running cache items. Max 99%. 80% is generally
    * a good value.
    */
    uint32_t protectedPct;

    /* 
     * Count-Min Sketch dimensions.
     * Width:  columns (buckets per row). More = higher resolution.
     * Depth:  rows (independent hash functions). More = fewer collisions.
     * ResetThreshold: total accesses before aging (halve all counters).
     */
    uint32_t sketchWidth;
    uint32_t sketchDepth;
    uint32_t sketchResetThreshold;

    /*
    * Unique seed for the sketch row-hash mixing function to reduce
    * hash based collisions 
    */
    uint64_t sketchSeed;

    /*
    * Unique seed used for the hash function used to derive hash codes from 
    * input keys
    */
    uint64_t seed;

} WtlConfig;

/* ---------- Public API ---------- */

/*
* Gets the version string of the library as compiled. Returns a 
* numeric, semantic version string. In the format 
*   Major.Minor.Patch.Build 
* 
* Where every value is a base 10 integer. 
*/
VNLIB_EXPORT const char* VNLIB_CC WtlGetVersionString(void);

/*
* Computes and returns the number of bytes required to store the entire cache
* internal data structures as a single block of contiguous memory. This allocation
* is static for the lifetime of the cache service. Overhead is added for alignment 
* and hashtable load factor.
* 
* @param config  A valid cache configuration used to compute internal buffer sizes
* @returns  32bit signed integer with the number of bytes to hold the cache structure if
* positive. 0 is undefined, and a negative integer if the configuration is invalid. 
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlGetMemorySize(const WtlConfig* config);

/*
* Configures internal data structures based on the configuration and inside the block 
* of memory supplied by the cache parameter. You must use WtlGetMemorySize to 
* get the required structure size to allocate the block when starting your application.
* 
* @param config  A valid configuration used to create the cache system
* @param cache   A pointer to a valid memory block used for the cache library
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlInit(const WtlConfig* config, WtlCtx* cache);

/*
* Inserts a new value into the cache store by the key identified in the value parameter. The
* table maintains a reference to the value for the lifetime of the item. Insert can also 
* cause a forced lfu eviction which writes the evicted value to the evicted parameter if 
* an eviction occurs. evicted parameter may be null if a best-effort insertion is desired.
* 
* If evicted parameter is null and an eviction can occur, WTL_ERR_WILL_EVICT will be 
* returned no changes occur. 
* 
* @param cache    A pointer to an initialized cache store
* @param value    A pointer to the new value to store 
* @param evicted  A valid pointer to a WtlValue that may be written to with the evicted item
* caused by the insertion. May be null if caller wishes to try an insertion.
* @returns  WTL_SUCCESS if the item was inserted WTL_ITEM_EVICTED if an item was evicted,
* a negative error code otherwise. 
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlInsert(WtlCtx* cache, const WtlValue* value, WtlValue* evicted);

/*
* Searches the cache store for a value at the given key. If found, writes the fields
* of the outValue pointer supplied. Updates internal frequency counters for the value.
* The key memory is not referenced outside of this call. Both hits and misses update
* internal frequency counters. 
* 
* @param cache     A pointer to an initialized cache store
* @param key       A WtlKey structure that points to the key memory used for lookup
* @param outValue  A pointer to the WtlValue memory used to assign the found value
* @returns  WTL_SUCCESS if the value was found and written to the outValue. Error code 
* otherwise.
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlGet(WtlCtx* cache, WtlKey key, WtlValue* outValue);

/*
* Searches the store for a value at the given key, but does not update any frequency 
* counters for the key. The key memory is not referenced outside of this call. outValue
* parameter may be a null pointer if checking for key existence.
* 
* @param cache     A pointer to an initialized cache store
* @param key       A WtlKey structure that points to the key memory used for lookup
* @param outValue  A pointer to the WtlValue memory used to assign the found value,
* or a null pointer if checking for key existence
* @returns  WTL_SUCCESS if the value was found and written to the outValue. Error code
* otherwise.
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlPeek(WtlCtx* cache, WtlKey key, WtlValue* outValue);

/*
* Removes an item from the cache store by the given key, if it's found. This is 
* unconditional and the internal value is cleared, which can leak memory if you
* do not maintain a reference to the original value's memory. This operation 
* completely removes all references to the previously stored value.
* 
* @param cache     A pointer to an initialized cache store
* @param key       A WtlKey structure that points to the key memory used for lookup
* @returns  WTL_SUCCESS if the value was found and removed from the store. Error code
* otherwise.
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlRemove(WtlCtx* cache, WtlKey key);

/*
* Removes an item from the cache store identified by the supplied value. The value should
* be a previously Get or Peek value. The key is used to identify the item in the store.
* This operation completely removes all references to the previously stored value. 
*
* @param cache     A pointer to an initialized cache store
* @param key       A pointer to a WtlValue structure obtained from Get() or Peek()
* @returns  WTL_SUCCESS if the value was found and removed from the store. Error code
* otherwise.
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlRemoveValue(WtlCtx* cache, const WtlValue* value);

/*
* Returns the total number of items currently occupying the cache store.
* 
* @param cache  A pointer to a previously configured cache structure
* @returns  The number of items in cache, or 0 if cache is null
*/
VNLIB_EXPORT uint32_t VNLIB_CC WtlCount(const WtlCtx* cache);

/*
* Records a key "hit" if the key exists in the store. Increasing the item's 
* frequency, keeping the item warmer or promoting it if necessary. Effectively 
* identical to WtlGet() but discarding the value.
* 
* @param cache     A pointer to an initialized cache store
* @param key       A WtlKey structure that points to the key memory used for lookup
* @returns  WTL_SUCCESS if the value was found and update. Error code otherwise.
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlTouch(WtlCtx* cache, WtlKey key);

/*
* Forces a manual sketch frequency table age, helpful on an interval to evict stale 
* items over longer periods of time.
*
* @param cache     A pointer to an initialized cache store
* @returns  WTL_SUCCESS if update succeeded, error code otherwise.
*/
VNLIB_EXPORT int32_t VNLIB_CC WtlAgeSketch(WtlCtx* cache);


#ifdef __cplusplus
}
#endif

#endif /* !VN_WTLFU_H */
