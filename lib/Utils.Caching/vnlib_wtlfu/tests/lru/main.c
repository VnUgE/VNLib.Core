/*
 * Copyright (c) 2026 Vaughn Nugent
 *
 * Library: VNLib
 * Package: vnlib_wtlfu
 * File: tests/lru/main.c
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

#include <stdlib.h>
#include <string.h>

#include "internal.h"
#include "test.h"

static void lruUnlinkAll(WtlLruList* lru)
{
    while (lruPop(lru));
}

/*
 * lruIsEmpty must correctly report the list state across its lifecycle:
 * empty on init, non-empty after push, empty again after pop.
 */
static int IsEmptyLifecycle(void)
{
    WtlLruList lru;
    WtlEntry entry, *popped;

    memset(&lru, 0, sizeof(lru));
    memset(&entry, 0, sizeof(entry));   

    // Freshly init'd list is empty
    EXPECT_TRUE(lruIsEmpty(&lru));

    // Pushing makes it non-empty
    {
        lruPush(&lru, &entry);
        EXPECT_FALSE(lruIsEmpty(&lru));
    }
    
    // Popping restores empty state
    {
        popped = lruPop(&lru);
        EXPECT_TRUE(popped == &entry);
        EXPECT_TRUE(lruIsEmpty(&lru));
    }

    return 0;
}

/*
 * lruPush must maintain the circular doubly-linked list invariant across
 * single, two, and three entry insertions, with correct head/tail/count.
 */
static int PushMaintainsRing(void)
{
    WtlLruList lru;
    WtlEntry a, b, c;

    memset(&lru, 0, sizeof(lru));
    memset(&a, 0, sizeof(a));
    memset(&b, 0, sizeof(b));
    memset(&c, 0, sizeof(c));

    // Single entry: both head and tail, self-linked
    {
        EXPECT_TRUE(lruPush(&lru, &a));

        EXPECT_TRUE(lru.head == &a);
        EXPECT_TRUE(lru.tail == &a);
        EXPECT_TRUE(a.prev == &a);
        EXPECT_TRUE(a.next == &a);
        EXPECT_EQ(lru.count, 1);
    }

    // Two entries: second is new head, first is tail, ring closes
    {
        EXPECT_TRUE(lruPush(&lru, &b));

        EXPECT_TRUE(lru.head == &b);
        EXPECT_TRUE(lru.tail == &a);
        EXPECT_TRUE(b.prev == &a);
        EXPECT_TRUE(b.next == &a);
        EXPECT_TRUE(a.prev == &b);
        EXPECT_TRUE(a.next == &b);
        EXPECT_EQ(lru.count, 2);
    }

    // Three entries: full ring traversal wraps in both directions
    {
        EXPECT_TRUE(lruPush(&lru, &c));

        EXPECT_TRUE(lru.head == &c);
        EXPECT_TRUE(lru.tail == &a);
        EXPECT_EQ(lru.count, 3);

        // Forward: c -> b -> a -> c
        EXPECT_TRUE(lru.head->next == &b);
        EXPECT_TRUE(lru.head->next->next == &a);
        EXPECT_TRUE(lru.head->next->next->next == &c);

        // Backward: a -> b -> c -> a
        EXPECT_TRUE(lru.tail->prev == &b);
        EXPECT_TRUE(lru.tail->prev->prev == &c);
        EXPECT_TRUE(lru.tail->prev->prev->prev == &a);
    }

    return 0;
}

/*
 * lruPushTail must maintain the circular doubly-linked list invariant
 * across single, two, and three entry insertions, with correct head/tail/count.
 * Entries enter at the tail (coldest position), opposite of lruPush.
 */
static int PushTailMaintainsRing(void)
{
    WtlLruList lru;
    WtlEntry a, b, c;

    memset(&lru, 0, sizeof(lru));
    memset(&a, 0, sizeof(a));
    memset(&b, 0, sizeof(b));
    memset(&c, 0, sizeof(c));

    // Single entry: both head and tail, self-linked
    {
        EXPECT_TRUE(lruPushTail(&lru, &a));

        EXPECT_TRUE(lru.head == &a);
        EXPECT_TRUE(lru.tail == &a);
        EXPECT_TRUE(a.prev == &a);
        EXPECT_TRUE(a.next == &a);
        EXPECT_EQ(lru.count, 1);
    }

    // Two entries: second is new tail, first is head, ring closes
    {
        EXPECT_TRUE(lruPushTail(&lru, &b));

        EXPECT_TRUE(lru.head == &a);
        EXPECT_TRUE(lru.tail == &b);
        EXPECT_TRUE(b.prev == &a);
        EXPECT_TRUE(b.next == &a);
        EXPECT_TRUE(a.prev == &b);
        EXPECT_TRUE(a.next == &b);
        EXPECT_EQ(lru.count, 2);
    }

    // Three entries: full ring traversal wraps in both directions
    {
        EXPECT_TRUE(lruPushTail(&lru, &c));

        EXPECT_TRUE(lru.head == &a);
        EXPECT_TRUE(lru.tail == &c);
        EXPECT_EQ(lru.count, 3);

        // Forward: a -> b -> c -> a
        EXPECT_TRUE(lru.head->next == &b);
        EXPECT_TRUE(lru.head->next->next == &c);
        EXPECT_TRUE(lru.head->next->next->next == &a);

        // Backward: c -> b -> a -> c
        EXPECT_TRUE(lru.tail->prev == &b);
        EXPECT_TRUE(lru.tail->prev->prev == &a);
        EXPECT_TRUE(lru.tail->prev->prev->prev == &c);
    }

    return 0;
}

static int PopReturnsLruOrder(void)
{
    WtlLruList lru;
    WtlEntry a, b, c, *popped;

    memset(&lru, 0, sizeof(lru));
    memset(&a, 0, sizeof(a));
    memset(&b, 0, sizeof(b));
    memset(&c, 0, sizeof(c));

    // Pop on empty list returns NULL
    {
        popped = lruPop(&lru);
        EXPECT_TRUE(popped == NULL);
    }

    // Push A, B, C (head==C, tail==A) then pop in LRU order: A, B, C

    ENSURE(lruPush(&lru, &a));
    ENSURE(lruPush(&lru, &b));
    ENSURE(lruPush(&lru, &c));

    // First pop returns A (oldest/tail)
    {     
        popped = lruPop(&lru);

        EXPECT_TRUE(popped == &a);
        EXPECT_TRUE(popped->prev == NULL);
        EXPECT_TRUE(popped->next == NULL);

        EXPECT_EQ(lru.count, 2);
        EXPECT_TRUE(lru.head == &c);
        EXPECT_TRUE(lru.tail == &b);
    }

    // Second pop returns B
    {       
        popped = lruPop(&lru);

        EXPECT_TRUE(popped == &b);
        EXPECT_TRUE(popped->prev == NULL);
        EXPECT_TRUE(popped->next == NULL);
        EXPECT_EQ(lru.count, 1);
        EXPECT_TRUE(lru.head == &c);
        EXPECT_TRUE(lru.tail == &c);
    }
    // Third pop returns C (now single entry)
    {       
        popped = lruPop(&lru);

        EXPECT_TRUE(popped == &c);
        EXPECT_TRUE(popped->prev == NULL);
        EXPECT_TRUE(popped->next == NULL);
        EXPECT_EQ(lru.count, 0);
        EXPECT_TRUE(lru.head == NULL);
        EXPECT_TRUE(lru.tail == NULL);
    }

    return 0;
}

/*
 * lruPeek must return the LRU (tail) without removing it, and return
 * NULL on an empty list.
 */
static int PeekReturnsTailWithoutRemoval(void)
{
    WtlLruList lru;
    WtlEntry a, b, c, *peeked;

    memset(&lru, 0, sizeof(lru));
    memset(&a, 0, sizeof(a));
    memset(&b, 0, sizeof(b));
    memset(&c, 0, sizeof(c));

    // Peek on empty list returns NULL
    {
        peeked = lruPeek(&lru);
        EXPECT_TRUE(peeked == NULL);
    }

    // Push A, B, C (head==C, tail==A); peek returns A without removing
    {
        ENSURE(lruPush(&lru, &a));
        ENSURE(lruPush(&lru, &b));
        ENSURE(lruPush(&lru, &c));

        peeked = lruPeek(&lru);
        EXPECT_TRUE(peeked == &a);

        // List state unchanged
        EXPECT_EQ(lru.count, 3);
        EXPECT_TRUE(lru.head == &c);
        EXPECT_TRUE(lru.tail == &a);
    }

    return 0;
}

/*
 * lruHeadGet and lruTailGet must return NULL on an empty list, both
 * return the same entry when only one exists, and return MRU/LRU
 * respectively for multiple entries.
 */
static int HeadAndTailAccessors(void)
{
    WtlLruList lru;
    WtlEntry a, b, c;

    memset(&lru, 0, sizeof(lru));
    memset(&a, 0, sizeof(a));
    memset(&b, 0, sizeof(b));
    memset(&c, 0, sizeof(c));

    // Empty list: both return NULL
    {
        EXPECT_TRUE(lruHeadGet(&lru) == NULL);
        EXPECT_TRUE(lruTailGet(&lru) == NULL);
    }

    // Single entry: both return the same entry
    {
        ENSURE(lruPush(&lru, &a));
        EXPECT_TRUE(lruHeadGet(&lru) == &a);
        EXPECT_TRUE(lruTailGet(&lru) == &a);
    }

    // Three entries: head==C (MRU), tail==A (LRU)
    {
        ENSURE(lruPush(&lru, &b));
        ENSURE(lruPush(&lru, &c));
        EXPECT_TRUE(lruHeadGet(&lru) == &c);
        EXPECT_TRUE(lruTailGet(&lru) == &a);
    }

    return 0;
}

/*
 * lruUnlink must remove an arbitrary entry from any position, repair
 * the circular ring, clear the victim's links, and update count.
 */
static int UnlinkFromAllPositions(void)
{
    WtlLruList lru;
    WtlEntry a, b, c;

    memset(&a, 0, sizeof(a));
    memset(&b, 0, sizeof(b));
    memset(&c, 0, sizeof(c));

    // Unlink the only entry: list becomes empty
    {
        memset(&lru, 0, sizeof(lru));
       
        ENSURE(lruPush(&lru, &a));

        ENSURE(lruUnlink(&lru, &a));

        EXPECT_EQ(lru.count, 0);
        EXPECT_TRUE(lru.head == NULL);
        EXPECT_TRUE(lru.tail == NULL);
        EXPECT_TRUE(a.prev == NULL);
        EXPECT_TRUE(a.next == NULL);
        
        lruUnlinkAll(&lru);
    }

    // Unlink head (C): B becomes new head, A stays tail, ring intact
    {
        memset(&lru, 0, sizeof(lru));

        ENSURE(lruPush(&lru, &a));
        ENSURE(lruPush(&lru, &b));
        ENSURE(lruPush(&lru, &c));

        ENSURE(lruUnlink(&lru, &c));

        EXPECT_EQ(lru.count, 2);
        EXPECT_TRUE(lru.head == &b);
        EXPECT_TRUE(lru.tail == &a);
        // Ring: B -> A -> B
        EXPECT_TRUE(b.next == &a);
        EXPECT_TRUE(a.prev == &b);
        EXPECT_TRUE(c.prev == NULL);
        EXPECT_TRUE(c.next == NULL);

        lruUnlinkAll(&lru);
    }

    // Unlink tail (A): B becomes new tail, C stays head, ring intact
    {
        memset(&lru, 0, sizeof(lru));
        ENSURE(lruPush(&lru, &a));
        ENSURE(lruPush(&lru, &b));
        ENSURE(lruPush(&lru, &c));

        ENSURE(lruUnlink(&lru, &a));

        EXPECT_EQ(lru.count, 2);
        EXPECT_TRUE(lru.head == &c);
        EXPECT_TRUE(lru.tail == &b);
        // Ring: C -> B -> C
        EXPECT_TRUE(c.next == &b);
        EXPECT_TRUE(b.prev == &c);
        EXPECT_TRUE(a.prev == NULL);
        EXPECT_TRUE(a.next == NULL);

        lruUnlinkAll(&lru);
    }

    // Unlink interior (B): head and tail unchanged, ring closes C -> A -> C
    {
        memset(&lru, 0, sizeof(lru));
        ENSURE(lruPush(&lru, &a));
        ENSURE(lruPush(&lru, &b));
        ENSURE(lruPush(&lru, &c));

        ENSURE(lruUnlink(&lru, &b));

        EXPECT_EQ(lru.count, 2);
        EXPECT_TRUE(lru.head == &c);
        EXPECT_TRUE(lru.tail == &a);
        // Ring: C -> A -> C
        EXPECT_TRUE(c.next == &a);
        EXPECT_TRUE(a.prev == &c);
        EXPECT_TRUE(b.prev == NULL);
        EXPECT_TRUE(b.next == NULL);
    }

    return 0;
}

/*
 * lruMoveToHead must relocate an existing entry to MRU without changing
 * count, and leave the ring intact for head, tail, and interior positions.
 */
static int MoveToHeadFromAllPositions(void)
{
    WtlLruList lru;
    WtlEntry a, b, c;

    memset(&a, 0, sizeof(a));
    memset(&b, 0, sizeof(b));
    memset(&c, 0, sizeof(c));

    // Move the only entry to head: no-op, still head
    {
        memset(&lru, 0, sizeof(lru));
        ENSURE(lruPush(&lru, &a));

        ENSURE(lruMoveToHead(&lru, &a));

        EXPECT_EQ(lru.count, 1);
        EXPECT_TRUE(lru.head == &a);
        EXPECT_TRUE(lru.tail == &a);

        lruUnlinkAll(&lru);
    }
   
    // Move tail (A) to head: A becomes MRU, B becomes new tail
    {
        memset(&lru, 0, sizeof(lru));
        ENSURE(lruPush(&lru, &a));
        ENSURE(lruPush(&lru, &b));
        ENSURE(lruPush(&lru, &c));
        // head==C, tail==A

        ENSURE(lruMoveToHead(&lru, &a));

        EXPECT_EQ(lru.count, 3);
        EXPECT_TRUE(lru.head == &a);
        EXPECT_TRUE(lru.tail == &b);
        // Ring: A -> C -> B -> A
        EXPECT_TRUE(a.next == &c);
        EXPECT_TRUE(c.next == &b);
        EXPECT_TRUE(b.next == &a);
        EXPECT_TRUE(a.prev == &b);
        EXPECT_TRUE(b.prev == &c);
        EXPECT_TRUE(c.prev == &a);

        lruUnlinkAll(&lru);
    }

    // Move interior (B) to head: B becomes MRU, ring intact
    {
        memset(&lru, 0, sizeof(lru));
        ENSURE(lruPush(&lru, &a));
        ENSURE(lruPush(&lru, &b));
        ENSURE(lruPush(&lru, &c));
        // head==C, tail==A, B is interior

        ENSURE(lruMoveToHead(&lru, &b));

        EXPECT_EQ(lru.count, 3);
        EXPECT_TRUE(lru.head == &b);
        EXPECT_TRUE(lru.tail == &a);
        // Ring: B -> C -> A -> B
        EXPECT_TRUE(b.next == &c);
        EXPECT_TRUE(c.next == &a);
        EXPECT_TRUE(a.next == &b);
        EXPECT_TRUE(b.prev == &a);
        EXPECT_TRUE(a.prev == &c);
        EXPECT_TRUE(c.prev == &b);

        lruUnlinkAll(&lru);
    }

    return 0;
}

/*
 * lruCount must report 0 on an empty list, track push/pop mutations,
 * and stay consistent with the internal count field across operations.
 */
static int CountTracksMutations(void)
{
    WtlLruList lru;
    WtlEntry a, b, c;

    memset(&a, 0, sizeof(a));
    memset(&b, 0, sizeof(b));
    memset(&c, 0, sizeof(c));

    memset(&lru, 0, sizeof(lru));

    // Empty list reports zero
    EXPECT_EQ(lruCount(&lru), 0);

    // Push tracks count
    {
        ENSURE(lruPush(&lru, &a));
        EXPECT_EQ(lruCount(&lru), 1);

        ENSURE(lruPush(&lru, &b));
        EXPECT_EQ(lruCount(&lru), 2);
    }

    // Pop decrements    
    {
        lruPop(&lru);
        EXPECT_EQ(lruCount(&lru), 1);

        // Push more, then unlink to verify decrement
        ENSURE(lruPush(&lru, &c));
        EXPECT_EQ(lruCount(&lru), 2);

        ENSURE(lruUnlink(&lru, &c));
        EXPECT_EQ(lruCount(&lru), 1);
    }

    // MoveToHead does not change count
    {       
        ENSURE(lruMoveToHead(&lru, &b));
        EXPECT_EQ(lruCount(&lru), 1);
    }

    // Drain to empty
    {
        lruPop(&lru);
        EXPECT_EQ(lruCount(&lru), 0);
    }

    return 0;
}

int RunTests(void)
{
    RUN_TEST(IsEmptyLifecycle());
    RUN_TEST(PushMaintainsRing());
    RUN_TEST(PushTailMaintainsRing());
    RUN_TEST(PopReturnsLruOrder());
    RUN_TEST(PeekReturnsTailWithoutRemoval());
    RUN_TEST(HeadAndTailAccessors());
    RUN_TEST(UnlinkFromAllPositions());
    RUN_TEST(MoveToHeadFromAllPositions());
    RUN_TEST(CountTracksMutations());

    return 0;
}
