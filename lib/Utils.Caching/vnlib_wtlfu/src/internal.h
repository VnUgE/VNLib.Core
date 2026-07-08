/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: internal.h
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

#pragma once

#ifndef _VN_WTLFU_INTERNAL_H
#define _VN_WTLFU_INTERNAL_H

#include <stdint.h>
#include <stddef.h>
#include "platform.h"
#include "debug.h"

/*
* Load factor threshold for hash table resize (as numerator over 100).
* 75 means resize when count > capacity * 75 / 100.
*/
#define WTL_HASH_LOAD_FACTOR 75

/*
* Initial hash table capacity (must be power of 2 for fast modulo).
*/
#define WTL_HASH_INIT_CAPACITY 64

/*
* Default configuration values (used when config fields are 0).
*/
#define WTL_DEFAULT_WINDOW_PCT      1
#define WTL_DEFAULT_PROTECTED_PCT   80

/*
* Cache entry structure.
*
* Memory layout (single allocation):
*   [ WtlEntry header | key bytes (keyLen) | value bytes (valueSize) ]
*
* The key and value bytes immediately follow the struct. Accessor
* macros below compute the correct offsets. The WtlValue* exposed to
* callers points to the value-bytes region.
*
* The entry is linked into one LRU segment list (prev/next) and
* tracked in the hash table via its hash slot.
*/
typedef struct WtlEntry
{
    /* Intrusive doubly-linked list pointers for LRU segment membership */
    struct WtlEntry* prev;
    struct WtlEntry* next;

} WtlEntry;

/*
* Intrusive doubly-linked LRU list head.
*
* Uses sentinel-free circular doubly-linked list. head == NULL means
* empty. On insert, entry becomes head; prev/next point to each other
* for a single-element list.
*/
typedef struct WtlLruList
{
    WtlEntry* head;  /* Most recently used (front of list) */
    WtlEntry* tail;  /* Least recently used (back of list) */
    uint32_t  count; /* Number of entries in this list */

} WtlLruList;


_VN_WTLFU_INTERNAL int lruInit(WtlLruList* lru);
_VN_WTLFU_INTERNAL int lruPush(WtlLruList* lru, WtlEntry* entry);
_VN_WTLFU_INTERNAL int lruPushTail(WtlLruList* lru, WtlEntry* entry);
_VN_WTLFU_INTERNAL int lruUnlink(WtlLruList* lru, WtlEntry* entry);
_VN_WTLFU_INTERNAL int lruMoveToHead(WtlLruList* lru, WtlEntry* entry);
_VN_WTLFU_INTERNAL WtlEntry* lruPop(WtlLruList* lru);
_VN_WTLFU_INTERNAL WtlEntry* lruPeek(const WtlLruList* lru);
_VN_WTLFU_INTERNAL WtlEntry* lruHeadGet(const WtlLruList* lru);
_VN_WTLFU_INTERNAL WtlEntry* lruTailGet(const WtlLruList* lru);
_VN_WTLFU_INTERNAL int       lruIsEmpty(const WtlLruList* lru);
_VN_WTLFU_INTERNAL uint32_t  lruCount(const WtlLruList* lru);


#endif /* !_VN_WTLFU_INTERNAL_H */
