/*
 * Copyright (c) 2026 Vaughn Nugent
 *
 * Library: VNLib
 * Package: vnlib_wtlfu
 * File: remove.c
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
* Confirms null/invalid inputs fail gracefully
*/
static int RemoveInputValidation(void)
{
    WtlCtx* cache = allocCache(NULL);

    WtlKey dummyKey = dummy_key("hello world");

    // Null cache structure
    EXPECT_EQ(WtlRemove(NULL, dummyKey), WTL_ERR_INVALID_ARG);

    // Null key but len should fail
    dummyKey.key = NULL;
    dummyKey.len = 5;
    EXPECT_EQ(WtlRemove(cache, dummyKey), WTL_ERR_INVALID_ARG);

    // Key but zero len should also fail
    dummyKey.key = "hello";
    dummyKey.len = 0;
    EXPECT_EQ(WtlRemove(cache, dummyKey), WTL_ERR_INVALID_ARG);

    free(cache);
    return 0;
}

/*
* Ensures that isolated values can be inserted and removed
* by their key. Which exercises key comparison.
*/
static int RemoveBasic(void)
{
    WtlCtx* cache = allocCache(NULL);

    WtlValue dummy = dummy_value("hello world");
    WtlKey dummyKey = dummy_key("hello world");

    EXPECT_EQ(WtlInsert(cache, &dummy, NULL), WTL_SUCCESS);
    EXPECT_EQ(WtlCount(cache), 1);
    EXPECT_EQ(WtlPeek(cache, dummyKey, NULL), WTL_SUCCESS);

    // Remove and check 
    EXPECT_EQ(WtlRemove(cache, dummyKey), WTL_SUCCESS);
    EXPECT_EQ(WtlCount(cache), 0);
    EXPECT_EQ(WtlPeek(cache, dummyKey, NULL), WTL_ERR_NOT_FOUND);

    free(cache);
    return 0;
}

/*
* Ensures that a double remove of the same key returns not found
*/
static int RemoveDoubleRemoveFails(void)
{
    WtlCtx* cache = allocCache(NULL);

    WtlValue dummy = dummy_value("hello world");
    WtlKey dummyKey = dummy_key("hello world");

    EXPECT_EQ(WtlInsert(cache, &dummy, NULL), WTL_SUCCESS);
    EXPECT_EQ(WtlCount(cache), 1);
    EXPECT_EQ(WtlPeek(cache, dummyKey, NULL), WTL_SUCCESS);

    // Remove and check 
    EXPECT_EQ(WtlRemove(cache, dummyKey), WTL_SUCCESS);
    EXPECT_EQ(WtlRemove(cache, dummyKey), WTL_ERR_NOT_FOUND);

    free(cache);
    return 0;
}


static int RunRemoveTests(void)
{
    RUN_TEST(RemoveInputValidation());
    RUN_TEST(RemoveBasic());
    RUN_TEST(RemoveDoubleRemoveFails());

    return 0;
}
