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


#if 0
#include <monocypher.h>
#include "vnlib_monocypher.h"

#define AEAD_MAX_NONCE_SIZE 24
#define AEAD_MAX_KEY_SIZE 32

#define AEAD_RESULT_SUCCESS 0

typedef struct ChaChaStreamStruct {
	uint8_t nonceCounter[8];
	uint8_t secretKey[32];
	uint8_t mac[16];
} ChaChaStream;

static int32_t _incrementNonce(ChaChaStream* stream)
{
	/*
	* The once will be incremented by 1 for each call to lock/unlock
	* if the nonce will overflow, then return 0 to indicate an error
	*/

	VALIDATE_PTR(stream);

	/* increment the nonce */

	uint64_t* nonce;
	uint64_t value;

	nonce = (uint64_t*)stream->nonceCounter;
	value = *nonce;

	/* increment the nonce */
	if(++value == 0)
	{
		/* nonce overflow */
		return FALSE;
	}

	/* assign the value back */
	*nonce = value;
	return TRUE;
}

uint32_t AeadStreamStructSize(void)
{
	return sizeof(ChaChaStream);
}

int32_t AeadUpdateKey(ChaChaStream* stream, const uint8_t key[32])
{
	VALIDATE_PTR(stream);
	VALIDATE_PTR(key);

	/* copy the key to the structure key */
	_memmove(stream->secretKey, key, 32);
	return TRUE;
}

int32_t AeadUpdateMac(ChaChaStream* stream, const uint8_t mac[16])
{
	VALIDATE_PTR(stream);
	VALIDATE_PTR(mac);

	/* copy the mac to the structure mac */
	_memmove(stream->mac, mac, 16);
	return TRUE;
}

int32_t AeadInitStream(ChaChaStream* stream, const uint8_t key[32], const uint8_t startingNonce[8], const uint8_t mac[16])
{
	VALIDATE_PTR(stream);
	VALIDATE_PTR(key);
	VALIDATE_PTR(startingNonce);
	VALIDATE_PTR(mac);

	/* clear stream before using */
	crypto_wipe(stream, sizeof(ChaChaStream));

	/* copy the key to the structure key */
	_memmove(stream->secretKey, key, 32);

	/* copy the nonce to the structure nonce */
	_memmove(stream->nonceCounter, startingNonce, 8);

	/* copy the mac to the structure mac */
	_memmove(stream->mac, mac, 16);
	return TRUE;
}

int32_t AeadEncrypt(ChaChaStream* stream, const uint8_t* plainText, uint32_t plainTextSize, uint8_t* cipherText, uint8_t* tag)
{
	VALIDATE_PTR(stream);
	VALIDATE_PTR(plainText);
	VALIDATE_PTR(cipherText);
	VALIDATE_PTR(tag);
}

#else 
typedef int something_to_stop_compiler_err_while_in_dev;
#endif