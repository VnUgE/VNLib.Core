/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: cache.c
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

#include "wtlfu.h"
#include "platform.h"
#include "internal.h"
#include "span.h"

#include "cache.h"

#include "hash.h"
#include "cmsketch.h"
#include "lru.h"
#include "hashtable.h"

#define WTL_CHECK_NULL(ptr) if (!(ptr)) { return WTL_ERR_INVALID_ARG; }

/* ---------------- CONFIG HELPERS ---------------- */

/*
* Resolves user percentages into concrete per-segment entry counts.
* Window is a percentage of total capacity (rounded up); protected is
* a percentage of the main cache (window excluded), probation gets the
* remainder, so the three segments sum to capacity exactly.
*/
vnlib_fn_internal void wtlConfigResolveInternal(
	const WtlConfig* src,
	struct wtl_internal_cache_config* dst
)
{
	DEBUG_ASSERT(src);
	DEBUG_ASSERT(dst);

	uint32_t window, main, protectedSize;

	// Clamp window to 2 minimum entries for very small tables, 
	// easier for lru lists. See above guarantees section
	window = (src->capacity * src->windowPct + WTL_NUM_MAX_PERCENT) / 100;
	window = window < 2 ? 2 : window;
	
	main = src->capacity - window;
	protectedSize = (main * src->protectedPct) / 100;

	struct wtl_internal_cache_config conf = {
		.capacity		= src->capacity,
		.keySeed		= src->seed,
		.windowSize		= window,
		.protectedSize  = protectedSize,
		.probationSize  = (main - protectedSize)
	};

	*dst = conf;
}

/*
* Computes the payload layout for the given configuration. This is the
* single source of truth shared by WtlGetMemorySize and WtlInit
* so the size formula and the pointer walk cannot drift apart.
*/
vnlib_fn_internal void wtlConfigGetMemoryLayout(
	const WtlConfig* config, 
	struct wtl_cache_layout* out
)
{
	DEBUG_ASSERT(config);
	DEBUG_ASSERT(out);

	uint64_t offset = _alignUp(sizeof(WtlCtx), WTL_CACHE_LINE);

	out->slotsOffset = offset;
	out->slotsBytes = (uint64_t)config->capacity * sizeof(WtlHashSlot);

	offset = _alignUp(offset + out->slotsBytes, WTL_CACHE_LINE);

	out->sketchOffset = offset;
	out->sketchBytes = (uint64_t)(((uint64_t)config->sketchDepth) * (uint64_t)(config->sketchWidth));

	out->total = offset + out->sketchBytes;
}

/* ---------------- INLINE HELPERS ---------------- */

static _vn_inline uint32_t _getKeyHashCode(const WtlCtx* cache, cspan_t key)
{
	DEBUG_ASSERT(cache);
	return wtlHash32(key, cache->config.keySeed);
}

static _vn_inline void _assignValueFromEntry(const WtlEntry* entry, WtlValue* value)
{
	DEBUG_ASSERT(entry);
	DEBUG_ASSERT(value);

	// Copy value data from our entry
	value->key = spanGetOffsetC(entry->key, 0);
	value->keyLen = spanGetSizeC(entry->key);
	value->value = entry->value;
}

static _vn_inline void _assignEntryFromValue(const WtlValue* value, uint32_t hashCode, WtlEntry* entry)
{
	DEBUG_ASSERT(value);
	DEBUG_ASSERT(entry);

	// Assign new entry values
	entry->hash = hashCode;
	entry->value = value->value;

	spanInitC(&entry->key, value->key, value->keyLen);
}

static _vn_inline WtlEntry* _chooseVictim(const WtlCtx* cache, WtlEntry* first, WtlEntry* other)
{
	uint32_t firstEst, otherEst;

	DEBUG_ASSERT(cache);
	DEBUG_ASSERT(first);
	DEBUG_ASSERT(other);	

	firstEst = wtlSketchEstimate(&cache->sketch, first->hash);
	otherEst = wtlSketchEstimate(&cache->sketch, other->hash);

	// If first estimate is greater than other, then other is the victim
	// otherwise choose first as the victim
	return firstEst > otherEst
		? other
		: first;
}

static _vn_inline void _lruEntryPush(WtlCtx* cache, WtlEntry* entry, WtlEntryLruMemberType newMembership)
{
	int lruRet;
	DEBUG_ASSERT(cache);
	DEBUG_ASSERT(entry);	

	switch (newMembership)
	{
	case WTL_LRU_MEMBER_WINDOW:
		lruRet = lruPush(&cache->windowCache, entry);
		DEBUG_ASSERT2(lruRet, "LRU Failed to push entry into window cache");
		break;
	
	case WTL_LRU_MEMBER_PROBATION:
		lruRet = lruPush(&cache->mainCache.probation, entry);
		DEBUG_ASSERT2(lruRet, "LRU Failed to push entry into probation cache");
		break;

	case WTL_LRU_MEMBER_PROTECTED:
		lruRet = lruPush(&cache->mainCache.protected, entry);
		DEBUG_ASSERT2(lruRet, "LRU Failed to push entry into main protected cache");
		break;
	
	case WTL_LRU_MEMBER_NONE:
	default:
		DEBUG_ASSERT2(0, "Invalid LRU list member");
		lruRet = 0;
		break;
	}

	// Update list membership if pushed correctly
	if (lruRet)
	{
		entry->lruMember = newMembership;
	}
}

static _vn_inline void _lruEntryPushTail(WtlCtx* cache, WtlEntry* entry, WtlEntryLruMemberType newMembership)
{
	int lruRet;
	DEBUG_ASSERT(cache);
	DEBUG_ASSERT(entry);

	switch (newMembership)
	{
	case WTL_LRU_MEMBER_WINDOW:
		lruRet = lruPushTail(&cache->windowCache, entry);
		DEBUG_ASSERT2(lruRet, "LRU Failed to push entry into window cache tail");
		break;

	case WTL_LRU_MEMBER_PROBATION:
		lruRet = lruPushTail(&cache->mainCache.probation, entry);
		DEBUG_ASSERT2(lruRet, "LRU Failed to push entry into probation cache tail");
		break;

	case WTL_LRU_MEMBER_PROTECTED:
		lruRet = lruPushTail(&cache->mainCache.protected, entry);
		DEBUG_ASSERT2(lruRet, "LRU Failed to push entry into main protected cache tail");
		break;

	case WTL_LRU_MEMBER_NONE:
	default:
		DEBUG_ASSERT2(0, "Invalid LRU list member");
		lruRet = 0;
		break;
	}

	// Update list membership if pushed correctly
	if (lruRet)
	{
		entry->lruMember = newMembership;
	}
}

static _vn_inline void _lruEntryUnlink(WtlCtx* cache, WtlEntry* entry)
{
	int lruRet;
	DEBUG_ASSERT(cache);
	DEBUG_ASSERT(entry);

	switch (entry->lruMember)
	{
	case WTL_LRU_MEMBER_WINDOW:
		lruRet = lruUnlink(&cache->windowCache, entry);
		DEBUG_ASSERT2(lruRet, "LRU Failed to unlink entry from window cache");
		break;

	case WTL_LRU_MEMBER_PROBATION:
		lruRet = lruUnlink(&cache->mainCache.probation, entry);
		DEBUG_ASSERT2(lruRet, "LRU Failed to unlink entry from probation cache");
		break;

	case WTL_LRU_MEMBER_PROTECTED:
		lruRet = lruUnlink(&cache->mainCache.protected, entry);
		DEBUG_ASSERT2(lruRet, "LRU Failed to unlink entry from main protected cache");
		break;

	case WTL_LRU_MEMBER_NONE:
	default:
		DEBUG_ASSERT2(0, "Entry has no list membership");
		lruRet = 0;
		break;
	}

	// Clear membership flags if unlinked correctly
	if (lruRet)
	{		
		entry->lruMember = WTL_LRU_MEMBER_NONE;
	}	
}

static _vn_inline int _newEntryCanEvict(const WtlCtx* cache)
{
	return lruCount(&cache->windowCache) >= cache->config.windowSize
		&& lruCount(&cache->mainCache.probation) >= cache->config.probationSize;
}

/* ---------------- INTERNAL HELPERS ---------------- */

vnlib_fn_internal WtlEntry* wtlFindEntryFromKey(WtlCtx* cache, cspan_t key)
{
	WtlEntry* entry;
	uint32_t hash;

	DEBUG_ASSERT(cache);
	DEBUG_ASSERT(!spanIsNullC(key) && !spanIsEmptyC(key));

	// Compute hash of key
	hash = _getKeyHashCode(cache, key);

	// Check table for entry
	entry = wtlHashTableLookup(&cache->table, hash);
	if (!entry)
	{
		return NULL;
	}

	// Sanity check on hashes
	DEBUG_ASSERT2(entry->hash == hash, "An entry was returned by lookup table with the wrong hash code");
	DEBUG_ASSERT(!spanIsNullC(entry->key));

	// Ensure key matches
	if (
		spanGetSizeC(key) != spanGetSizeC(entry->key) ||
		memcmp(spanGetOffsetC(entry->key, 0), spanGetOffsetC(key, 0), spanGetSizeC(entry->key)) != 0
		)
	{
		// Key does not match exactly, it's not the same entry, but hash collision

		return NULL;
	}

	return entry;
}

vnlib_fn_internal void wtlPromoteEntryToProtected(WtlCtx* cache, WtlEntry* entry)
{
	DEBUG_ASSERT(cache);
	DEBUG_ASSERT(entry);
	DEBUG_ASSERT(entry->lruMember == WTL_LRU_MEMBER_PROBATION);

	_lruEntryUnlink(cache, entry);

	// Protected segment is full, so we need to demote lru protected
	if (lruCount(&cache->mainCache.protected) >= cache->config.protectedSize)
	{
		WtlEntry* demoted = lruPop(&cache->mainCache.protected);
		DEBUG_ASSERT(demoted);

		// Push as demoted to tail of probation segment
		_lruEntryPushTail(cache, demoted, WTL_LRU_MEMBER_PROBATION);
	}

	// Now part of the protected segment
	_lruEntryPush(cache, entry, WTL_LRU_MEMBER_PROTECTED);
}	

vnlib_fn_internal WtlEntry* wtlPushNewEntry(WtlCtx* cache, WtlEntry* entry)
{
	WtlEntry* candidate = NULL, * victim = NULL;

	DEBUG_ASSERT(cache); 
	DEBUG_ASSERT(entry);

	// entry needs to enter the window cache
	_lruEntryPush(cache, entry, WTL_LRU_MEMBER_WINDOW);

	// If window has room were done, otherwise we need to shuffle lists
	if (lruCount(&cache->windowCache) <= cache->config.windowSize)
	{
		return NULL;
	}

	candidate = lruPeek(&cache->windowCache);

	// Window is overflowing and needs to move to probation, see if room is available

	if (lruCount(&cache->mainCache.probation) < cache->config.probationSize)
	{
		// Pushing into probation is safe, there is room

		// Unlink it from window and push into probation
		_lruEntryUnlink(cache, candidate);
		_lruEntryPush(cache, candidate, WTL_LRU_MEMBER_PROBATION);

		return NULL;
	}	

	// Probation is full, time to choose eviction candidate
	victim = _chooseVictim(cache, candidate, lruPeek(&cache->mainCache.probation));

	// Victim is probee, candidate is window
	if (victim->lruMember == WTL_LRU_MEMBER_PROBATION)
	{		
		// Candidate is window time, unlink it and stuff it into the probation
		_lruEntryUnlink(cache, candidate);
		_lruEntryPush(cache, candidate, WTL_LRU_MEMBER_PROBATION);		
	}
	else
	{
		// Candidate was selected as eviction victim
		DEBUG_ASSERT(victim->lruMember == WTL_LRU_MEMBER_WINDOW);
		DEBUG_ASSERT(candidate == victim);
	}	

	// Always unlink the victim
	_lruEntryUnlink(cache, victim);

	return victim;
}

/* ---------------- PUBLIC API ---------------- */

VNLIB_EXPORT const char* VNLIB_CC WtlGetVersionString(void)
{
#ifndef WTL_VERSION_STRING
	#error No library version string defined
#else
	return WTL_VERSION_STRING;
#endif // !WTL_VERSION_STRING	
}

VNLIB_EXPORT int32_t VNLIB_CC WtlGetMemorySize(const WtlConfig* config)
{
	struct wtl_cache_layout memLayout;

	// Ensure percentages
	{
		// Limit minimum capacity
		if (config->capacity < WTL_MIN_CAPACITY)
		{
			return WTL_ERR_INVALID_ARG;
		}

		if (
			config->protectedPct == 0 ||
			config->windowPct == 0
		)
		{
			return WTL_ERR_INVALID_ARG;
		}

		if (
			config->protectedPct > WTL_NUM_MAX_PERCENT ||
			config->windowPct > WTL_NUM_MAX_PERCENT
			)
		{
			return WTL_ERR_INVALID_ARG;
		}

		// List segment sizes are valid, continue
	}

	// Validate sketch
	{
		WtlSketchConfig sketch = {
			.depth				= config->sketchDepth,
			.width				= config->sketchWidth,
			.resetThreshold		= config->sketchResetThreshold,
			.seed				= config->sketchSeed
		};

		if (wtlSketchConfigIsValid(&sketch) != 0)
		{
			return WTL_ERR_INVALID_ARG;
		}

		// Sketch config is valid, continue
	}

	// Hashtable is valid so long as capacity > 0 and the system has enough memory

	wtlConfigGetMemoryLayout(config, &memLayout);

	/*
	* size_t is the variable width generally used for malloc() family calls and 
	* a good indicator of how much memory the system can afford to allocate. Users
	* generally should be nowhere near this value, but to protect overflows we can 
	* use it as a theoretical max memory boundary. 
	* 
	* Otherwise int32 is our max upper bound for this function so cap to int32 max
	*/

#if SIZE_MAX < INT32_MAX
	if (memLayout.total >= SIZE_MAX) return WTL_ERR_INVALID_ARG;
#else
	if (memLayout.total > (uint64_t)INT32_MAX) return WTL_ERR_INVALID_ARG;
#endif

	// Safe to cast to int32 without overflow
	return (int32_t)memLayout.total;
}

VNLIB_EXPORT int32_t VNLIB_CC WtlInit(const WtlConfig* config, WtlCtx* cache)
{	
	// Absolute base address for the cache table, required for cache/alignment
	uint8_t* const absBaseOffset = (uint8_t*)(cache);
	struct wtl_cache_layout memLayout;

	WTL_CHECK_NULL(config);
	WTL_CHECK_NULL(cache);

	// Load cache memory layout
	wtlConfigGetMemoryLayout(config, &memLayout);

	// Minimal guard for overruns/overflows
#if SIZE_MAX < INT32_MAX
	if (memLayout.total >= SIZE_MAX) return WTL_ERR_INVALID_ARG;
#else
	if (memLayout.total > (uint64_t)INT32_MAX) return WTL_ERR_INVALID_ARG;
#endif

	memset(cache, 0, memLayout.total);

	// Resolve and set internal configuration
	// down-cast the const away for initialization, otherwise config is const through 
	// normal lifetime
	wtlConfigResolveInternal(config, (struct wtl_internal_cache_config*)(&cache->config));

	// Setup hashtable
	{
		cache->table.capacity = config->capacity;
		cache->table.slots = (WtlHashSlot*)(absBaseOffset + memLayout.slotsOffset);

		// Ensure hashtable is valid
		if (wtlHashTableIsValid(&cache->table) != WTL_SUCCESS)
		{
			return WTL_ERR_INVALID_ARG;
		}
	}

	// Setup sketch
	{
		WtlSketchConfig sketchConf = {
			.depth			= config->sketchDepth,
			.width			= config->sketchWidth,
			.resetThreshold	= config->sketchResetThreshold,
			.seed			= config->sketchSeed
		};

		// Assign sketch config
		*((WtlSketchConfig*)(&cache->sketch.config)) = sketchConf;	
		
		// Assign sketch table memory from base offset
		spanInit(
			&cache->sketch.table,
			(absBaseOffset + memLayout.sketchOffset),
			(uint32_t)memLayout.sketchBytes
		);

		// Ensure sketch config is valid before continuing
		if (wtlSketchIsValid(&cache->sketch) != WTL_SUCCESS)
		{
			return WTL_ERR_INVALID_ARG;
		}
	}

	return WTL_SUCCESS;
}

VNLIB_EXPORT uint32_t VNLIB_CC WtlCount(const WtlCtx* cache)
{
	return cache ? wtlHashTableCount(&cache->table) : 0;
}

VNLIB_EXPORT int32_t VNLIB_CC WtlPeek(WtlCtx* cache, WtlKey key, WtlValue* outValue)
{
	cspan_t keySpan;
	WtlEntry* entry;

	// Validate user args
	WTL_CHECK_NULL(cache);
	WTL_CHECK_NULL(key.key);  // Early check for null key pointer

	// Set key span and validate. Null may be allowed of size is 0 or 
	// assigned manually so must check both 
	spanInitC(&keySpan, key.key, key.len);
	if (spanIsEmptyC(keySpan) || spanIsNullC(keySpan))
	{
		return WTL_ERR_INVALID_ARG;
	}

	// Find the entry from the user's supplied key
	entry = wtlFindEntryFromKey(cache, keySpan);
	if (!entry)
	{
		return WTL_ERR_NOT_FOUND;
	}

	// Caller can set out to null if they just want to see if the key 
	// exists. If they set outValue param, then assign it.
	if (outValue) 
	{
		// Overwrites the outValue's fields with data from the found entry
		_assignValueFromEntry(entry, outValue);
	}

	return WTL_SUCCESS;
}

VNLIB_EXPORT int32_t VNLIB_CC WtlAgeSketch(WtlCtx* cache)
{
	WTL_CHECK_NULL(cache);

	wtlSketchAge(&cache->sketch);

	return WTL_SUCCESS;
}

VNLIB_EXPORT int32_t VNLIB_CC WtlRemove(WtlCtx* cache, WtlKey key)
{
	cspan_t keySpan;
	WtlEntry* entry;

	// Validate user args
	WTL_CHECK_NULL(cache);
	WTL_CHECK_NULL(key.key);  // Early check for null key pointer

	// Set key span and validate. Null may be allowed of size is 0 or 
	// assigned manually so must check both 
	spanInitC(&keySpan, key.key, key.len);
	if (spanIsEmptyC(keySpan) || spanIsNullC(keySpan))
	{
		return WTL_ERR_INVALID_ARG;
	}

	entry = wtlFindEntryFromKey(cache, keySpan);
	if (!entry)
	{
		return WTL_ERR_NOT_FOUND;
	}

	// Remove entry from internal list segments
	_lruEntryUnlink(cache, entry);

	// Remove from table, also invalidates entry memory
	return (int32_t)wtlHashTableRemove(&cache->table, entry);
}

VNLIB_EXPORT int32_t VNLIB_CC WtlRemoveValue(WtlCtx* cache, const WtlValue* value)
{
	WtlKey key;

	WTL_CHECK_NULL(value);

	// Assign key data only, we don't care about the value pointer
	// WtlRemove will validate the key memory and length
	key.key = value->key;
	key.len = value->keyLen;

	return WtlRemove(cache, key);
}

VNLIB_EXPORT int32_t VNLIB_CC WtlGet(WtlCtx* cache, WtlKey key, WtlValue* outValue)
{
	cspan_t keySpan;
	WtlEntry* entry;

	// Validate user args
	WTL_CHECK_NULL(cache);
	WTL_CHECK_NULL(outValue);
	WTL_CHECK_NULL(key.key);  // Early check for null key pointer

	// Set key span and validate. Null may be allowed of size is 0 or 
	// assigned manually so must check both 
	spanInitC(&keySpan, key.key, key.len);
	if (spanIsEmptyC(keySpan) || spanIsNullC(keySpan))
	{
		return WTL_ERR_INVALID_ARG;
	}

	entry = wtlFindEntryFromKey(cache, keySpan);
	if (!entry)
	{
		return WTL_ERR_NOT_FOUND;
	}

	// Record the key hit 
	wtlSketchRecord(&cache->sketch, entry->hash);

	switch (entry->lruMember)
	{
		// Nothing to do, just promote to tip for window or protected 
		// segments
	case WTL_LRU_MEMBER_WINDOW:
		lruMoveToHead(&cache->windowCache, entry);
		break;

	case WTL_LRU_MEMBER_PROTECTED:
		lruMoveToHead(&cache->mainCache.protected, entry);
		break;

		// When in probation, entry must be promoted to protected 
		// segment on hit
	case WTL_LRU_MEMBER_PROBATION:
		wtlPromoteEntryToProtected(cache, entry);
		break;

	case WTL_LRU_MEMBER_NONE:
	default:
		DEBUG_ASSERT2(0, "Entry update failed, does not belong to an lru list");
		return WTL_ERROR;
	}

	// All good, give the value back to the user :) 
	_assignValueFromEntry(entry, outValue);

	return WTL_SUCCESS;
}

VNLIB_EXPORT int32_t VNLIB_CC WtlInsert(WtlCtx* cache, const WtlValue* value, WtlValue* evictedVal)
{
	uint32_t keyHash;
	cspan_t keySpan;
	WtlEntry* entry = NULL, * evicted = NULL;

	// Validate user args
	WTL_CHECK_NULL(cache);
	WTL_CHECK_NULL(value);
	WTL_CHECK_NULL(value->key);	// early check for null key pointer

	spanInitC(&keySpan, value->key, value->keyLen);
	if (spanIsEmptyC(keySpan) || spanIsNullC(keySpan))
	{
		return WTL_ERR_INVALID_ARG;
	}

	/*
	* Evicted pointer may be null if the caller does not want to service an
	* eviction. They can attempt an insertion, and if an eviction will occur
	* return an error if the evicted ptr is null
	*/
	if (!evictedVal && _newEntryCanEvict(cache))
	{
		return WTL_ERR_WILL_EVICT;
	}

	keyHash = _getKeyHashCode(cache, keySpan);
	
	switch (wtlHashTableInsert(&cache->table, keyHash, &entry))
	{
		// Inserted, ready for use, ensure pointing to valid memory
	case WTL_SUCCESS:
		DEBUG_ASSERT(entry);
		break;

	case WTL_TABLE_ERR_FULL:
		return WTL_ERR_NO_MEMORY;

	case WTL_ERR_DUPLICATE:
		return WTL_ERR_DUPLICATE;

	default:
		return WTL_ERROR;
	}

	// Always clear new entry
	memset(entry, 0, sizeof(WtlEntry));

	// Update sketch record
	wtlSketchRecord(&cache->sketch, keyHash);

	_assignEntryFromValue(value, keyHash, entry);
	DEBUG_ASSERT(keyHash == entry->hash);

	// Push new entries and process an eviction
	evicted = wtlPushNewEntry(cache, entry);
	if (evicted)
	{
		DEBUG_ASSERT(evictedVal);

		DEBUG_ASSERT(evicted->hash != 0);
		DEBUG_ASSERT(evicted->lruMember == WTL_LRU_MEMBER_NONE);

		// Copy evicted data before freeing entry
		_assignValueFromEntry(evicted, evictedVal);

		// Free/zero entry memory back to pool
		wtlHashTableRemove(&cache->table, evicted);
		evicted = NULL;

		// Notify caller that an item was evicted
		return WTL_ITEM_EVICTED;
	}
	else
	{
		return WTL_SUCCESS;
	}	
}

VNLIB_EXPORT int32_t VNLIB_CC WtlTouch(WtlCtx* cache, WtlKey key)
{
	WtlValue outVal;

	// Pass a pointer to the out val, but discard it
	return WtlGet(cache, key, &outVal);
}
