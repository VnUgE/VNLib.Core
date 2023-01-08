/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Hashing.Portable
* File: JsonWebKey.cs 
*
* JsonWebKey.cs is part of VNLib.Hashing.Portable which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Hashing.Portable is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Hashing.Portable is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Hashing.Portable. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Text.Json;
using System.Security.Cryptography;

using VNLib.Utils;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;

namespace VNLib.Hashing.IdentityUtility
{
    public static class JWKAlgorithms
    {
        public const string RS256 = "RS256";
        public const string RS384 = "RS384";
        public const string RS512 = "RS512";
        public const string PS256 = "PS256";
        public const string PS384 = "PS384";
        public const string PS512 = "PS512";
        public const string ES256 = "ES256";
        public const string ES384 = "ES384";
        public const string ES512 = "ES512";
    }
    
    public class EncryptionTypeNotSupportedException : NotSupportedException
    {
        public EncryptionTypeNotSupportedException(string message) : base(message)
        {}

        public EncryptionTypeNotSupportedException(string message, Exception innerException) : base(message, innerException)
        {}

        public EncryptionTypeNotSupportedException()
        {}
    }

    public static class JsonWebKey
    {       
        
        /// <summary>
        /// Verifies the <see cref="JsonWebToken"/> against the supplied
        /// Json Web Key in <see cref="JsonDocument"/> format
        /// </summary>
        /// <param name="token"></param>
        /// <param name="jwk">The supplied single Json Web Key</param>
        /// <returns>True if required JWK data exists, ciphers were created, and data is verified, false otherwise</returns>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="EncryptionTypeNotSupportedException"></exception>
        public static bool VerifyFromJwk(this JsonWebToken token, in JsonElement jwk)
        {           
            //Get key use and algorithm
            string? use = jwk.GetPropString("use");
            string? alg = jwk.GetPropString("alg");

            //Use and alg are required here
            if(use == null || alg == null)
            {
                return false;
            }
            
            //Make sure the key is used for signing/verification
            if (!"sig".Equals(use, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using (JsonDocument jwtHeader = token.GetHeader())
            {
                string? jwtAlg = jwtHeader.RootElement.GetPropString("alg");
                //Make sure the jwt was signed with the same algorithm type
                if (!alg.Equals(jwtAlg, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            switch (alg.ToUpper(null))
            {
                //Rsa witj pkcs and pss
                case JWKAlgorithms.RS256:
                    {
                        using RSA? rsa = GetRSAPublicKey(in jwk);
                        return rsa != null && token.Verify(rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    }
                case JWKAlgorithms.RS384:
                    {
                        using RSA? rsa = GetRSAPublicKey(in jwk);
                        return rsa != null && token.Verify(rsa, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1);
                    }
                case JWKAlgorithms.RS512:
                    {
                        using RSA? rsa = GetRSAPublicKey(in jwk);
                        return rsa != null && token.Verify(rsa, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
                    }
                case JWKAlgorithms.PS256:
                    {
                        using RSA? rsa = GetRSAPublicKey(in jwk);
                        return rsa != null && token.Verify(rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
                    }
                case JWKAlgorithms.PS384:
                    {
                        using RSA? rsa = GetRSAPublicKey(in jwk);
                        return rsa != null && token.Verify(rsa, HashAlgorithmName.SHA384, RSASignaturePadding.Pss);
                    }
                case JWKAlgorithms.PS512:
                    {
                        using RSA? rsa = GetRSAPublicKey(in jwk);
                        return rsa != null && token.Verify(rsa, HashAlgorithmName.SHA512, RSASignaturePadding.Pss);
                    }
                //Eccurves
                case JWKAlgorithms.ES256:
                    {
                        using ECDsa? eCDsa = GetECDsaPublicKey(in jwk);
                        return eCDsa != null && token.Verify(eCDsa, HashAlgorithmName.SHA256);
                    }
                case JWKAlgorithms.ES384:
                    {
                        using ECDsa? eCDsa = GetECDsaPublicKey(in jwk);
                        return eCDsa != null && token.Verify(eCDsa, HashAlgorithmName.SHA384);
                    }
                case JWKAlgorithms.ES512:
                    {
                        using ECDsa? eCDsa = GetECDsaPublicKey(in jwk);
                        return eCDsa != null && token.Verify(eCDsa, HashAlgorithmName.SHA512);
                    }
                default:
                    throw new EncryptionTypeNotSupportedException();
            }

        }

        /// <summary>
        /// Verifies the <see cref="JsonWebToken"/> against the supplied
        /// <see cref="ReadOnlyJsonWebKey"/>
        /// </summary>
        /// <param name="token"></param>
        /// <param name="jwk">The supplied single Json Web Key</param>
        /// <returns>True if required JWK data exists, ciphers were created, and data is verified, false otherwise</returns>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="OutOfMemoryException"></exception>
        /// <exception cref="EncryptionTypeNotSupportedException"></exception>
        public static bool VerifyFromJwk(this JsonWebToken token, ReadOnlyJsonWebKey jwk) => token.VerifyFromJwk(jwk.KeyElement);
        
        /// <summary>
        /// Signs the <see cref="JsonWebToken"/> with the supplied JWK json element
        /// </summary>
        /// <param name="token"></param>
        /// <param name="jwk">The JWK in the <see cref="JsonElement"/> </param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="EncryptionTypeNotSupportedException"></exception>
        public static void SignFromJwk(this JsonWebToken token, in JsonElement jwk)
        {
            _ = token ?? throw new ArgumentNullException(nameof(token));
            //Get key use and algorithm
            string? use = jwk.GetPropString("use");
            string? alg = jwk.GetPropString("alg");

            //Use and alg are required here
            if (use == null || alg == null)
            {
                throw new InvalidOperationException("Algorithm or JWK use is null");
            }

            //Make sure the key is used for signing/verification
            if (!"sig".Equals(use, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The JWK cannot be used for signing");
            }

            switch (alg.ToUpper(null))
            {
                //Rsa witj pkcs and pss
                case JWKAlgorithms.RS256:
                    {
                        using RSA? rsa = GetRSAPrivateKey(in jwk);
                        _ = rsa ?? throw new InvalidOperationException("JWK Does not contain an RSA private key");
                        token.Sign(rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, 128);
                        return;
                    }
                case JWKAlgorithms.RS384:
                    {
                        using RSA? rsa = GetRSAPrivateKey(in jwk);
                        _ = rsa ?? throw new InvalidOperationException("JWK Does not contain an RSA private key");
                        token.Sign(rsa, HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1, 128);
                        return;
                    }
                case JWKAlgorithms.RS512:
                    {
                        using RSA? rsa = GetRSAPrivateKey(in jwk);
                        _ = rsa ?? throw new InvalidOperationException("JWK Does not contain an RSA private key");
                        token.Sign(rsa, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1, 256);
                        return;
                    }
                case JWKAlgorithms.PS256:
                    {
                        using RSA? rsa = GetRSAPrivateKey(in jwk);
                        _ = rsa ?? throw new InvalidOperationException("JWK Does not contain an RSA private key");
                        token.Sign(rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pss, 128);
                        return;
                    }
                case JWKAlgorithms.PS384:
                    {
                        using RSA? rsa = GetRSAPrivateKey(in jwk);
                        _ = rsa ?? throw new InvalidOperationException("JWK Does not contain an RSA private key");
                        token.Sign(rsa, HashAlgorithmName.SHA384, RSASignaturePadding.Pss, 128);
                        return;
                    }
                case JWKAlgorithms.PS512:
                    {
                        using RSA? rsa = GetRSAPrivateKey(in jwk);
                        _ = rsa ?? throw new InvalidOperationException("JWK Does not contain an RSA private key");
                        token.Sign(rsa, HashAlgorithmName.SHA512, RSASignaturePadding.Pss, 256);
                        return;
                    }
                //Eccurves
                case JWKAlgorithms.ES256:
                    {
                        using ECDsa? eCDsa = GetECDsaPrivateKey(in jwk);
                        _ = eCDsa ?? throw new InvalidOperationException("JWK Does not contain an ECDsa private key");
                        token.Sign(eCDsa, HashAlgorithmName.SHA256, 128);
                        return;
                    }
                case JWKAlgorithms.ES384:
                    {
                        using ECDsa? eCDsa = GetECDsaPrivateKey(in jwk);
                        _ = eCDsa ?? throw new InvalidOperationException("JWK Does not contain an ECDsa private key");
                        token.Sign(eCDsa, HashAlgorithmName.SHA384, 128);
                        return;
                    }
                case JWKAlgorithms.ES512:
                    {
                        using ECDsa? eCDsa = GetECDsaPrivateKey(in jwk);
                        _ = eCDsa ?? throw new InvalidOperationException("JWK Does not contain an ECDsa private key");
                        token.Sign(eCDsa, HashAlgorithmName.SHA512, 256);
                        return;
                    }
                default:
                    throw new EncryptionTypeNotSupportedException();
            }
        }

        /// <summary>
        /// Signs the <see cref="JsonWebToken"/> with the supplied JWK json element
        /// </summary>
        /// <param name="token"></param>
        /// <param name="jwk">The JWK in the <see cref="ReadOnlyJsonWebKey"/> </param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="EncryptionTypeNotSupportedException"></exception>
        public static void SignFromJwk(this JsonWebToken token, ReadOnlyJsonWebKey jwk) => token.SignFromJwk(jwk.KeyElement);

        /// <summary>
        /// Gets the <see cref="RSA"/> public key algorithm for the current <see cref="ReadOnlyJsonWebKey"/>
        /// </summary>
        /// <param name="key"></param>
        /// <returns>The <see cref="RSA"/> algorithm of the public key if loaded</returns>
        public static RSA? GetRSAPublicKey(this ReadOnlyJsonWebKey key) => key == null ? null : GetRSAPublicKey(key.KeyElement);

        /// <summary>
        /// Gets the <see cref="RSA"/> private key algorithm for the current <see cref="ReadOnlyJsonWebKey"/>
        /// </summary>
        /// <param name="key"></param>
        ///<returns>The <see cref="RSA"/> algorithm of the private key key if loaded</returns>
        public static RSA? GetRSAPrivateKey(this ReadOnlyJsonWebKey key) => key == null ? null : GetRSAPrivateKey(key.KeyElement);

        /// <summary>
        /// Gets the RSA public key algorithm from the supplied Json Web Key <see cref="JsonElement"/>
        /// </summary>
        /// <param name="jwk">The element that contains the JWK data</param>
        /// <returns>The <see cref="RSA"/> algorithm if found, or null if the element does not contain public key</returns>
        public static RSA? GetRSAPublicKey(in JsonElement jwk)
        {
            RSAParameters? rSAParameters = GetRsaParameters(in jwk, false);
            //Create rsa from params
            return rSAParameters.HasValue ? RSA.Create(rSAParameters.Value) : null;
        }

        /// <summary>
        /// Gets the RSA private key algorithm from the supplied Json Web Key <see cref="JsonElement"/>
        /// </summary>
        /// <param name="jwk"></param>
        /// <returns>The <see cref="RSA"/> algorithm if found, or null if the element does not contain private key</returns>
        public static RSA? GetRSAPrivateKey(in JsonElement jwk)
        {
            RSAParameters? rSAParameters = GetRsaParameters(in jwk, true);
            //Create rsa from params
            return rSAParameters.HasValue ? RSA.Create(rSAParameters.Value) : null;
        }

        private static RSAParameters? GetRsaParameters(in JsonElement jwk, bool includePrivateKey)
        {
            //Get the RSA public key credentials
            ReadOnlySpan<char> e = jwk.GetPropString("e");
            ReadOnlySpan<char> n = jwk.GetPropString("n");

            if (e.IsEmpty || n.IsEmpty)
            {
                return null;
            }

            if (includePrivateKey)
            {
                //Get optional private key params
                ReadOnlySpan<char> d = jwk.GetPropString("d");
                ReadOnlySpan<char> dp = jwk.GetPropString("dq");
                ReadOnlySpan<char> dq = jwk.GetPropString("dp");
                ReadOnlySpan<char> p = jwk.GetPropString("p");
                ReadOnlySpan<char> q = jwk.GetPropString("q");

                //Create params from exponent, moduls and private key components
                return new()
                {
                    Exponent = FromBase64UrlChars(e),
                    Modulus = FromBase64UrlChars(n),
                    D = FromBase64UrlChars(d),
                    DP = FromBase64UrlChars(dp),
                    DQ = FromBase64UrlChars(dq),
                    P = FromBase64UrlChars(p),
                    Q = FromBase64UrlChars(q),
                };
            }
            else
            {
                //Create params from exponent and moduls 
                return new()
                {
                    Exponent = FromBase64UrlChars(e),
                    Modulus = FromBase64UrlChars(n),
                };
            }
          
        }


        /// <summary>
        /// Gets the ECDsa public key algorithm for the current <see cref="ReadOnlyJsonWebKey"/>
        /// </summary>
        /// <param name="key"></param>
        /// <returns>The <see cref="ECDsa"/> algorithm of the public key if loaded</returns>
        public static ECDsa? GetECDsaPublicKey(this ReadOnlyJsonWebKey key) => key == null ? null : GetECDsaPublicKey(key.KeyElement);

        /// <summary>
        /// Gets the <see cref="ECDsa"/> private key algorithm for the current <see cref="ReadOnlyJsonWebKey"/>
        /// </summary>
        /// <param name="key"></param>
        ///<returns>The <see cref="ECDsa"/> algorithm of the private key key if loaded</returns>
        public static ECDsa? GetECDsaPrivateKey(this ReadOnlyJsonWebKey key) => key == null ? null : GetECDsaPrivateKey(key.KeyElement);

        /// <summary>
        /// Gets the ECDsa public key algorithm from the supplied Json Web Key <see cref="JsonElement"/>
        /// </summary>
        /// <param name="jwk">The public key element</param>
        /// <returns>The <see cref="ECDsa"/> algorithm from the key if loaded, null if no key data was found</returns>
        public static ECDsa? GetECDsaPublicKey(in JsonElement jwk)
        {
            //Get the EC params
            ECParameters? ecParams = GetECParameters(in jwk, false);
            //Return new alg
            return ecParams.HasValue ? ECDsa.Create(ecParams.Value) : null;
        }

        /// <summary>
        /// Gets the ECDsa private key algorithm from the supplied Json Web Key <see cref="JsonElement"/>
        /// </summary>
        /// <param name="jwk">The element that contains the private key data</param>
        /// <returns>The <see cref="ECDsa"/> algorithm from the key if loaded, null if no key data was found</returns>
        public static ECDsa? GetECDsaPrivateKey(in JsonElement jwk)
        {
            //Get the EC params
            ECParameters? ecParams = GetECParameters(in jwk, true);
            //Return new alg
            return ecParams.HasValue ? ECDsa.Create(ecParams.Value) : null;
        }
        

        private static ECParameters? GetECParameters(in JsonElement jwk, bool includePrivate)
        {
            //Get the RSA public key credentials
            ReadOnlySpan<char> x = jwk.GetPropString("x");
            ReadOnlySpan<char> y = jwk.GetPropString("y");

            //Optional private key
            ReadOnlySpan<char> d = includePrivate ? jwk.GetPropString("d") : null;

            if (x.IsEmpty || y.IsEmpty)
            {
                return null;
            }
            
            ECCurve curve;
            //Get the EC curve name from the curve ID
            switch (jwk.GetPropString("crv")?.ToUpper(null))
            {
                case "P-256":
                    curve = ECCurve.NamedCurves.nistP256;
                    break;
                case "P-384":
                    curve = ECCurve.NamedCurves.nistP384;
                    break;
                case "P-521":
                    curve = ECCurve.NamedCurves.nistP521;
                    break;
                default:
                    return null;
            }

            //get params
            return new()
            {
                Curve = curve,
                Q = new ECPoint()
                {
                    X = FromBase64UrlChars(x),
                    Y = FromBase64UrlChars(y),
                },
                //Optional private key
                D = FromBase64UrlChars(d)
            };
        }
        
        private static byte[]? FromBase64UrlChars(ReadOnlySpan<char> base64)
        {
            if (base64.IsEmpty)
            {
                return null;
            }
            //bin buffer for temp decoding
            using UnsafeMemoryHandle<byte> binBuffer = Memory.UnsafeAlloc<byte>(base64.Length + 16, false);
            //base64url decode
            ERRNO count = VnEncoding.Base64UrlDecode(base64, binBuffer.Span);
            //Return buffer or null if failed
            return count ? binBuffer.AsSpan(0, count).ToArray() : null;
        }
    }
}
