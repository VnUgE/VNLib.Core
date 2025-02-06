/*
* Copyright (c) 2025 Vaughn Nugent
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

#include "argon2.h"
#include <monocypher.h>

#define ARGON2_WORK_AREA_MULTIPLIER 1024

VNLIB_EXPORT uint32_t VNLIB_CC Argon2CalcWorkAreaSize(const argon2Ctx* context)
{
	return context->m_cost * ARGON2_WORK_AREA_MULTIPLIER;
}

/*
* The purpose of this function is to remap the Argon2 context/function call
* interface to the Monocypher library version. Also performing some basic
* input validation that also matches the Argon2 library.
*/

VNLIB_EXPORT argon2_error_codes VNLIB_CC Argon2ComputeHash(const argon2Ctx* context, void* workArea)
{
	if (!context || !workArea)
	{
		return ERR_INVALID_PTR;
	}

	crypto_argon2_config config = 
	{
		.algorithm = context->version,
		.nb_blocks = context->m_cost,
		.nb_passes = context->t_cost,
		.nb_lanes = context->threads
	};

	crypto_argon2_inputs inputs =
	{
		.pass		= context->pwd,
		.pass_size	= context->pwdlen,
		.salt		= context->salt,
		.salt_size	= context->saltlen
	};

	crypto_argon2_extras extras =
	{
		.ad			= context->ad,
		.ad_size	= context->adlen,
		.key		= context->secret,
		.key_size	= context->secretlen
	};

	/* must specify a password input */
	if (inputs.pass_size < 1)
	{
		return ARGON2_PWD_TOO_SHORT;
	}

	if (!inputs.pass)
	{
		return ARGON2_PWD_PTR_MISMATCH;
	}

	if (inputs.salt_size < 1)
	{
		return ARGON2_SALT_TOO_SHORT;
	}

	/* Verify salt pointer 1is not invalid  */
	if (!inputs.salt)
	{
		return ARGON2_SALT_PTR_MISMATCH;
	}

	//If key is set, verify a valid pointer
	if (extras.key_size > 0 && !extras.key)
	{
		return ARGON2_SECRET_PTR_MISMATCH;
	}

	if (context->outlen < 1)
	{
		return ARGON2_OUTPUT_TOO_SHORT;
	}

	if (!context->out)
	{
		return ARGON2_OUTPUT_PTR_NULL;
	}

	/* invoke lib function */
	crypto_argon2(
		context->out, 
		context->outlen, 
		workArea, 
		config, 
		inputs, 
		extras
	);

	return ARGON2_OK;
}