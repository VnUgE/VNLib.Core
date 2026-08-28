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

#ifndef VN_WTLFU_INTERNAL_H
#define VN_WTLFU_INTERNAL_H

#include <stdint.h>
#include <stddef.h>
#include "wtlfu.h"
#include "platform.h"
#include "span.h"

/*
* Initial hash table capacity (must be power of 2 for fast modulo).
*/
#define WTL_HASH_INIT_CAPACITY 64

typedef enum 
{
    WTL_LRU_MEMBER_NONE,
    
    WTL_LRU_MEMBER_WINDOW,
    
    WTL_LRU_MEMBER_PROBATION,

    WTL_LRU_MEMBER_PROTECTED

} WtlEntryLruMemberType;

typedef struct WtlEntry
{
    /* Intrusive doubly-linked list pointers for LRU segment membership */
    struct WtlEntry* prev;
    struct WtlEntry* next;

    /*
    * The cache LRU list membership.
    */
    WtlEntryLruMemberType lruMember;

    /*
    * Cached hashcode value for the entry in the table. Computed once
    * on insert (reservation) and used for sketch and hashtable lookups
    */
    uint32_t hash;

    /*
    * span pointing to the key data 
    */
    cspan_t key;
    
    /*
    * User's data value to be stored in the entry
    */
    const void* value;

} WtlEntry;

#endif /* !VN_WTLFU_INTERNAL_H */
