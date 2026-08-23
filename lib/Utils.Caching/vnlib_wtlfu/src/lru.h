/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: lru.h
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

#ifndef VN_WTLFU_LRU_LIST_H
#define VN_WTLFU_LRU_LIST_H

#include <stdint.h>
#include <stddef.h>
#include "internal.h"

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

#endif /* !VN_WTLFU_LRU_LIST_H */
