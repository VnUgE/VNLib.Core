/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: lru.c
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
* lru.c - Intrusive doubly-linked LRU list operations for vnlib_wtlfu.
*
* Implements sentinel-free circular doubly-linked list operations used
* by the three cache segments (window, probationary, protected). Entries
* are moved between lists as they are admitted, promoted, or evicted.
*
* The list tracks entry count to support capacity-based eviction
* decisions at the cache layer. Byte tracking is handled by the cache
* stats layer, not the list itself.
*
* All functions are defensive: NULL list pointers are no-op (or return
* NULL for accessors). Precondition asserts verify that entries are not
* already linked before push, and are linked before unlink.
*/

#include "span.h"
#include "lru.h"
#include "debug.h"

#define LRU_FALSE 0
#define LRU_TRUE 1

static _vn_inline void _lruIncrementCount(WtlLruList* lru)
{
    DEBUG_ASSERT(lru);
    lru->count++;
}

static _vn_inline void _lruDecrementCount(WtlLruList* lru)
{
    DEBUG_ASSERT(lru);
    lru->count--;
}

static _vn_inline void _lruNodeClearLinks(WtlEntry* entry)
{
    DEBUG_ASSERT(entry);

    entry->next = NULL;
    entry->prev = NULL;
}

/*
* Push an entry to the MRU position (front/head) of the LRU list.
* The entry must not already be in any list (caller must unlink first).
*
* The list is a sentinel-free circular doubly-linked list. On insert,
* the entry becomes the new head; the old tail links back to it.
*/
vnlib_fn_internal int lruPush(WtlLruList* lru, WtlEntry* entry)
{
    DEBUG_ASSERT(lru);
    DEBUG_ASSERT(entry);

    // Entry must have null pointers, otherwise error
    DEBUG_ASSERT(!entry->prev);
    DEBUG_ASSERT(!entry->next);	

    if (!lru || !entry || entry->prev || entry->next)
    {
        return LRU_FALSE;
    }

    // No elements in the list
    if (!lru->head)
    {
        // Self-link to satisfy the circular invariant
        entry->prev = entry;
        entry->next = entry;

        lru->head = entry;
        lru->tail = entry;
    }
    else
    {
        // Splice into the front of the circular ring
        entry->prev = lru->tail;
        entry->next = lru->head;

        lru->head->prev = entry;
        lru->tail->next = entry;
        lru->head = entry;
    }

    _lruIncrementCount(lru);

    return LRU_TRUE;
}

/*
* Push an entry to the LRU position (back/tail) of the list.
* The entry must not already be in any list (caller must unlink first).
* Used by the admission path to insert new entries as the coldest
* item in the probationary segment.
*/
vnlib_fn_internal int lruPushTail(WtlLruList* lru, WtlEntry* entry)
{
    DEBUG_ASSERT(lru);
    DEBUG_ASSERT(entry);

    //Expects entries not in the list
    DEBUG_ASSERT(!entry->prev);
    DEBUG_ASSERT(!entry->next);

    if (!lru || !entry || entry->prev || entry->next)
    {
        return LRU_FALSE;
    }

    // No elements in the list
    if (!lru->head)
    {
        // Self-link to satisfy the circular invariant
        entry->prev = entry;
        entry->next = entry;

        lru->head = entry;
        lru->tail = entry;
    }
    else
    {
        // Splice into the back of the circular ring
        entry->next = lru->head;
        entry->prev = lru->tail;

        lru->tail->next = entry;
        lru->head->prev = entry;
        lru->tail = entry;
    }

    _lruIncrementCount(lru);

    return LRU_TRUE;
}

/*
* Remove and return the LRU entry (tail/back of list).
* Returns NULL if the list is empty or NULL.
*/
vnlib_fn_internal WtlEntry* lruPop(WtlLruList* lru)
{
    WtlEntry* victim = NULL;
    WtlEntry* newTail = NULL;

    DEBUG_ASSERT(lru);
    if (!lru)
    {
        return NULL;
    }	

    // There is no tail, nothing to return
    if (!lru->tail)
    {
        return NULL;
    }

    victim = lru->tail;

    // Single element tail == head, clear pointers
    if (lru->head == lru->tail)
    {
        lru->head = NULL;
        lru->tail = NULL;
    }
    else
    {
        // Repair the ring after tail removal
        newTail = victim->prev;
        newTail->next = lru->head;

        lru->head->prev = newTail;
        lru->tail = newTail;
    }

    _lruNodeClearLinks(victim);
    _lruDecrementCount(lru);

    return victim;
}

/*
* Remove an arbitrary entry from anywhere in the list.
* The entry's prev/next are cleared to NULL. The caller is responsible
* for freeing or re-linking the entry after unlink.
*/
vnlib_fn_internal int lruUnlink(WtlLruList* lru, WtlEntry* entry)
{
    WtlEntry* newHead = NULL;
    WtlEntry* newTail = NULL;

    DEBUG_ASSERT(lru);
    DEBUG_ASSERT(entry);

    // Entry must be linked in a list, list is circular, must never 
    // have a null pointer if in the list
    DEBUG_ASSERT(entry->prev);
    DEBUG_ASSERT(entry->next);

    if (!lru || !entry || !entry->prev || !entry->next)
    {
        return LRU_FALSE;
    }

    // Entry is the only element in the list
    if (lru->head == entry && lru->tail == entry)
    {
        // Only element: no ring to repair, just clear the list
        lru->head = NULL;
        lru->tail = NULL;
    }
    // Entry is the head of the list
    else if (lru->head == entry)
    {
        // Advance head to the next entry and re-close the ring from tail to new head
        newHead = entry->next;
        newHead->prev = lru->tail;

        lru->tail->next = newHead;
        lru->head = newHead;
    }
    // Entry is the tail of the list
    else if (lru->tail == entry)
    {
        // Retreat tail to the previous entry and re-close the ring from head to new tail
        newTail = entry->prev;
        newTail->next = lru->head;

        lru->head->prev = newTail;
        lru->tail = newTail;
    }
    // Interior node
    else
    {
        // Head and tail are unaffected, update node pointers
        entry->prev->next = entry->next;
        entry->next->prev = entry->prev;
    }

    _lruNodeClearLinks(entry);
    _lruDecrementCount(lru);

    return LRU_TRUE;
}

/*
* Move an entry that is already in the list to the MRU (head) position.
* This is the recency update on a cache hit.
*/
vnlib_fn_internal int lruMoveToHead(WtlLruList* lru, WtlEntry* entry)
{
    // Push cannot fail if unlink succeeds as it properly clears its fields
    return lruUnlink(lru, entry) && lruPush(lru, entry);
}

/*
* Return the LRU entry (tail) without removing it.
* Returns NULL if the list is empty or NULL.
*/
vnlib_fn_internal WtlEntry* lruPeek(const WtlLruList* lru)
{
    // tail contains least recently used, same as peek
    return lruTailGet(lru);
}

/*
* Return the MRU entry (head).
* Returns NULL if the list is empty or NULL.
*/
vnlib_fn_internal WtlEntry* lruHeadGet(const WtlLruList* lru)
{
    DEBUG_ASSERT(lru);	

    return lru ? lru->head : NULL;
}

/*
* Return the LRU entry (tail).
* Returns NULL if the list is empty or NULL.
*/
vnlib_fn_internal WtlEntry* lruTailGet(const WtlLruList* lru)
{
    DEBUG_ASSERT(lru);	

    return lru ? lru->tail : NULL;
}

/*
* Return non-zero if the list has no entries, zero otherwise.
* Returns non-zero (empty) if the list is NULL.
*/
vnlib_fn_internal int lruIsEmpty(const WtlLruList* lru)
{
    DEBUG_ASSERT(lru);	

    return lru ? lru->count == 0 : 1;
}

/*
* Return the number of entries in the list.
* Returns 0 if the list is NULL.
*/
vnlib_fn_internal uint32_t lruCount(const WtlLruList* lru)
{
    DEBUG_ASSERT(lru);

    return lru ? lru->count : 0;
}
