/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: validation.c
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
* Local mirrors of the raw error codes returned by wtlHashTableIsValid.
* These are not yet named constants in hashtable.h, so the literals are
* copied here until the public API gains its own constants.
*
* Note: the null-table path (-1) is not tested. wtlHashTableIsValid
* begins with DEBUG_ASSERT(table), so passing a NULL table aborts debug
* builds. The NULL check is defensive and only reachable in release
* builds where assertions are compiled out.
*/
#define VALIDATION_ERR_CAPACITY   (-2)

/*
* A freshly allocated table must validate successfully.
*/
static int IsValidReturnsSuccessOnValidTable(void)
{
    WtlHashTable* table = allocHashTableRaw(16);

    EXPECT_EQ(wtlHashTableIsValid(table), WTL_SUCCESS);

    free(table);

    return 0;
}

/*
* Every power-of-two capacity from 1 up must validate successfully.
*/
static int IsValidReturnsSuccessAcrossPowersOfTwo(void)
{
    const uint32_t capacities[] = { 1, 2, 4, 16, 64, 256, 1024 };

    for (size_t i = 0; i < sizeof(capacities) / sizeof(capacities[0]); i++)
    {
        WtlHashTable* table = allocHashTableRaw(capacities[i]);

        EXPECT_EQ(wtlHashTableIsValid(table), WTL_SUCCESS);

        free(table);
    }

    return 0;
}

/*
* A table with a NULL slots pointer must return the null error (-1).
* Unlike the NULL-table path, a non-NULL table pointer does not trip
* the DEBUG_ASSERT, so this defensive branch is testable.
*/
static int IsValidNullSlotsReturnsNullError(void)
{
    WtlHashTable* table = allocHashTableRaw(16);

    table->slots = NULL;

    EXPECT_EQ(wtlHashTableIsValid(table), -1);

    free(table);

    return 0;
}

/*
* A table with a zero capacity must return the capacity error.
*/
static int IsValidZeroCapacityReturnsCapacityError(void)
{
    WtlHashTable* table = allocHashTableRaw(0);

    EXPECT_EQ(wtlHashTableIsValid(table), VALIDATION_ERR_CAPACITY);

    free(table);

    return 0;
}

/*
* A table whose capacity is not a power of two must return the
* capacity error.
*/
static int IsValidNonPowerOfTwoReturnsCapacityError(void)
{
    WtlHashTable* table12 = allocHashTableRaw(12);
    WtlHashTable* table100 = allocHashTableRaw(100);

    EXPECT_EQ(wtlHashTableIsValid(table12), VALIDATION_ERR_CAPACITY);
    EXPECT_EQ(wtlHashTableIsValid(table100), VALIDATION_ERR_CAPACITY);

    free(table12);
    free(table100);

    return 0;
}

static int RunValidationTests(void)
{
    RUN_TEST(IsValidReturnsSuccessOnValidTable());
    RUN_TEST(IsValidReturnsSuccessAcrossPowersOfTwo());
    RUN_TEST(IsValidNullSlotsReturnsNullError());
    RUN_TEST(IsValidZeroCapacityReturnsCapacityError());
    RUN_TEST(IsValidNonPowerOfTwoReturnsCapacityError());

    return 0;
}
