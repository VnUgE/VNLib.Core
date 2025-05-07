
/*
* Copyright (c) 2025 Vaughn Nugent
*
* Package: envsub
* File: span.h
*
* This library is free software; you can redistribute it and/or
* modify it under the terms of the GNU Lesser General Public License
* as published by the Free Software Foundation; either version 2.1
* of the License, or  (at your option) any later version.
*
* This library is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* Lesser General Public License for more details.
*
* You should have received a copy of the GNU Lesser General Public License
* along with envsub. If not, see http://www.gnu.org/licenses/.
*/

#pragma once

#ifndef _SPAN_UTIL_H
#define _SPAN_UTIL_H

#include <stdint.h>
#include <string.h>
#include <assert.h>

/*
* By default empty spans are allowed, which means that
* spans can be created with a size of 0, and/or a null
* data pointer.
*/
#ifndef EMPTY_SPANS
    #define EMPTY_SPANS 1
#endif

typedef struct memory_span_struct
{
    char* data;
    uint32_t size;
} span_t;

typedef struct read_only_memory_span_struct
{
    const char* data;
    uint32_t size;
} cspan_t;

static inline int spanIsValid(span_t span)

{
#if EMPTY_SPANS
    return span.size == 0 || span.data != NULL;
#else
    return span.data != NULL;
#endif /* !EMPTY_SPANS */
}

static inline int spanIsValidC(cspan_t span)
{
#if EMPTY_SPANS
    return span.size == 0 || span.data != NULL;
#else
    return span.data != NULL;
#endif /* !EMPTY_SPANS */
}

static inline int spanIsValidRange(span_t span, uint32_t offset, uint32_t size)
{
    return offset + size <= span.size;
}

static inline int spanIsValidRangeC(cspan_t span, uint32_t offset, uint32_t size)
{
    return offset + size <= span.size;
}

static inline void spanInitC(cspan_t* span, const char* data, uint32_t size)
{
    span->data = data;
    span->size = size;
}

static inline void spanInit(span_t* span, char* data, uint32_t size)
{
    span->data = data;
    span->size = size;
}

static inline const char* spanGetOffsetC(cspan_t span, uint32_t offset)
{
#if EMPTY_SPANS
    /*
    * Allow passing null pointers for empty spans, if enabled,
    * otherwise debug guards will catch empty spans
    */
    if (span.size == 0 && offset == 0)
    {
        return NULL;
    }
#endif /* !EMPTY_SPANS */

    assert(spanIsValidC(span) && "Expected span to be non-null");
    assert(offset < span.size && "Expected offset to be less than span size");

    return span.data + offset;
}

static inline char* spanGetOffset(span_t span, uint32_t offset)
{
    cspan_t cspan;
    spanInitC(&cspan, span.data, span.size);
    return (char*)spanGetOffsetC(cspan, offset);
}

static inline uint32_t spanGetSizeC(cspan_t span)
{
    return spanIsValidC(span)
        ? span.size
        : 0;
}

static inline uint32_t spanGetSize(span_t span)
{
    return spanIsValid(span)
        ? span.size
        : 0;
}

static inline void spanWrite(span_t span, uint32_t offset, const char* data, uint32_t size)
{
    assert(data != NULL && "Expected data to be non-null");
    assert(spanIsValidRange(span, offset, size) && "Expected offset + size to be less than span size");

    /* Copy data to span */
    memcpy(span.data + offset, data, size);
}

static inline void spanAppend(span_t span, uint32_t* offset, const char* data, uint32_t size)
{
    assert(offset != NULL && "Expected offset to be non-null");
    assert(data != NULL && "Expected data to be non-null");
    assert(spanIsValidRange(span, *offset, size) && "Expected offset + size to be less than span size");

    /* Copy data to span (also performs argument assertions) */
    spanWrite(span, *offset, data, size);

    /* Increment offset */
    *offset += size;
}

static inline span_t spanSlice(span_t span, uint32_t offset, uint32_t size)
{
    span_t slice;
    assert(spanIsValidRange(span, offset, size) && "Expected offset + size to be less than span size");

    /* If the size of the sliced span is 0 return an empty span */
    if (size == 0)
    {
        spanInit(&slice, NULL, 0);
    }
    else
    {
        /* Initialize slice, offset input data by the specified offset */
        spanInit(
            &slice,
            spanGetOffset(span, offset),
            size
        );
    }

    return slice;
}

static inline cspan_t spanSliceC(cspan_t span, uint32_t offset, uint32_t size)
{
    cspan_t slice;
    assert(spanIsValidRangeC(span, offset, size) && "Expected offset + size to be less than span size");

    /* If the size of the sliced span is 0 return an empty span */
    if (size == 0)
    {
        spanInitC(&slice, NULL, 0);
    }
    else
    {
        /* Initialize slice, offset input data by the specified offset */
        spanInitC(
            &slice,
            spanGetOffsetC(span, offset),
            size
        );
    }

    return slice;
}

static inline void spanCopyC(cspan_t src, span_t dest)
{
    assert(spanIsValidC(src) && "Expected span to be non-null");
    assert(spanIsValid(dest) && "Expected destination span to be non-null");
    assert(dest.size >= src.size && "Output buffer too small. Overrun detected");

    /* Copy data to span */
    memcpy(dest.data, src.data, src.size);
}

static inline void spanCopy(span_t src, span_t dest)
{
    cspan_t csrc;

    spanInitC(&csrc, src.data, src.size);
    spanCopyC(csrc, dest);
}

static inline void spanReadC(cspan_t src, char* dest, uint32_t size)
{
    span_t dsts;

    spanInit(&dsts, dest, size);
    spanCopyC(src, dsts);
}

static inline void spanRead(span_t src, char* dest, uint32_t size)
{
    cspan_t srcs;

    spanInitC(&srcs, src.data, src.size);
    spanReadC(srcs, dest, size);
}

static inline char spanGetCharC(cspan_t span, uint32_t offset)
{
    const char* ptr = spanGetOffsetC(span, offset);
    return ptr != NULL
        ? *(ptr)
        : '\0'; // Return null character if offset is out of bounds
}

static inline char spanGetChar(span_t span, uint32_t offset)
{
    cspan_t cspan;

    spanInitC(&cspan, span.data, span.size);
    return spanGetCharC(cspan, offset);
}

#endif /* !_SPAN_UTIL_H */
