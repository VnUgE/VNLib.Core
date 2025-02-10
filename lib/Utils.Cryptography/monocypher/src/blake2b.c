/*
* Copyright (c) 2023 Vaughn Nugent
*
* vnlib_monocypher is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* vnlib_monocypher is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with vnlib_monocypher. If not, see http://www.gnu.org/licenses/.
*/

#include <stdlib.h>
#include <monocypher.h>
#include "blake2b.h"

VNLIB_EXPORT uint32_t VNLIB_CC Blake2GetContextSize(void)
{
	return sizeof(crypto_blake2b_ctx);
}

VNLIB_EXPORT int32_t VNLIB_CC Blake2Init(void* context, uint32_t hashlen, const void* key, uint32_t keylen)
{
	crypto_blake2b_ctx* ctx;
	ctx = (crypto_blake2b_ctx*)context;

	VALIDATE_PTR(ctx);

	/* validate key pointer if set */
	if (keylen > 0 && !key)
	{
		return ERR_KEY_PTR_INVALID;
	}

	if (keylen > MC_MAX_KEY_SIZE)
	{
		return ERR_KEY_LEN_INVALID;
	}

	/* validate hash length */
	if (hashlen > MC_MAX_HASH_SIZE)
	{
		return ERR_HASH_LEN_INVALID;
	}

	/* initialize context, non-keyed just calls the keyed funciton */
	crypto_blake2b_keyed_init(ctx, hashlen, key, keylen);

	return BLAKE2B_RESULT_SUCCESS;
}

VNLIB_EXPORT int32_t VNLIB_CC Blake2Update(void* context, const void* data, uint32_t datalen)
{
	crypto_blake2b_ctx* ctx;
	ctx = (crypto_blake2b_ctx*)context;
	VALIDATE_PTR(ctx);
	VALIDATE_PTR(data);

	crypto_blake2b_update(ctx, data, datalen);
	return BLAKE2B_RESULT_SUCCESS;
}

VNLIB_EXPORT int32_t VNLIB_CC Blake2Final(void* context, void* hash, uint32_t hashlen)
{
	crypto_blake2b_ctx* ctx;
	ctx = (crypto_blake2b_ctx*)context;
	VALIDATE_PTR(ctx);
	VALIDATE_PTR(hash);

	/* validate hash length */
	if (hashlen != ctx->hash_size)
	{
		return ERR_HASH_LEN_INVALID;
	}
	crypto_blake2b_final(ctx, hash);
	return BLAKE2B_RESULT_SUCCESS;
}

VNLIB_EXPORT int32_t VNLIB_CC Blake2GetHashSize(void* context)
{
	crypto_blake2b_ctx* ctx;
	ctx = (crypto_blake2b_ctx*)context;
	VALIDATE_PTR(ctx);
	return (int32_t)ctx->hash_size;
}
