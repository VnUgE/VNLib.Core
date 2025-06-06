/*
* Copyright (c) 2025 Vaughn Nugent
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
    /// <summary>
    /// Contains constants for JSON Web Key (JWK) algorithm identifiers
    /// </summary>
    public static class JWKAlgorithms
    {
        /// <summary>
        /// RSA signature algorithm using SHA-256 hash
        /// </summary>
        public const string RS256 = "RS256";
        
        /// <summary>
        /// RSA signature algorithm using SHA-384 hash
        /// </summary>
        public const string RS384 = "RS384";
        
        /// <summary>
        /// RSA signature algorithm using SHA-512 hash
        /// </summary>
        public const string RS512 = "RS512";
        
        /// <summary>
        /// RSA PSS signature algorithm using SHA-256 hash
        /// </summary>
        public const string PS256 = "PS256";
        
        /// <summary>
        /// RSA PSS signature algorithm using SHA-384 hash
        /// </summary>
        public const string PS384 = "PS384";
        
        /// <summary>
        /// RSA PSS signature algorithm using SHA-512 hash
        /// </summary>
        public const string PS512 = "PS512";
        
        /// <summary>
        /// ECDSA signature algorithm using P-256 curve and SHA-256 hash
        /// </summary>
        public const string ES256 = "ES256";
        
        /// <summary>
        /// ECDSA signature algorithm using P-384 curve and SHA-384 hash
        /// </summary>
        public const string ES384 = "ES384";
        
        /// <summary>
        /// ECDSA signature algorithm using P-521 curve and SHA-512 hash
        /// </summary>
        public const string ES512 = "ES512";
        
        /// <summary>
        /// ECDSA signature algorithm using secp256k1 curve and SHA-256 hash
        /// </summary>
        public const string ES256K = "ES256K";
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

    /// <summary>
    /// The JWK key usage flags
    /// </summary>
    public enum JwkKeyUsage
    {
        /// <summary>
        /// Default/not supported operation
        /// </summary>
        None,
        /// <summary>
        /// The key supports cryptographic signatures
        /// </summary>
        Signature,
        /// <summary>
        /// The key supports encryption operations
        /// </summary>
        Encryption
    }

    /// <summary>
    /// Contains extension methods for verifying and signing <see cref="JsonWebToken"/> 
    /// using <see cref="IJsonWebKey"/>s.
    /// </summary>
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
        public static bool VerifyFromJwk<TKey>(this JsonWebToken token, in TKey jwk) where TKey: notnull, IJsonWebKey
        {            
            ArgumentNullException.ThrowIfNull(token);

            //Use and alg are required here
            if(jwk.KeyUse != JwkKeyUsage.Signature || jwk.Algorithm == null)
            {
                return false;
            }

            //Get the jwt header to confirm its the same algorithm as the jwk
            using (JsonDocument jwtHeader = token.GetHeader())
            {
                string? jwtAlg = jwtHeader.RootElement.GetPropString("alg");
                
                //Make sure the jwt was signed with the same algorithm type
                if (!jwk.Algorithm.Equals(jwtAlg, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            switch (jwk.Algorithm.ToUpper(null))
            {
                //Rsa witj pkcs and pss
                case JWKAlgorithms.RS256:
                    {
                        using RSA? rsa = GetRSAPublicKey(jwk);
                        return rsa != null && token.Verify(rsa, HashAlg.SHA256, RSASignaturePadding.Pkcs1);
                    }
                case JWKAlgorithms.RS384:
                    {
                        using RSA? rsa = GetRSAPublicKey(jwk);
                        return rsa != null && token.Verify(rsa, HashAlg.SHA384, RSASignaturePadding.Pkcs1);
                    }
                case JWKAlgorithms.RS512:
                    {
                        using RSA? rsa = GetRSAPublicKey(jwk);
                        return rsa != null && token.Verify(rsa, HashAlg.SHA512, RSASignaturePadding.Pkcs1);
                    }
                case JWKAlgorithms.PS256:
                    {
                        using RSA? rsa = GetRSAPublicKey(jwk);
                        return rsa != null && token.Verify(rsa, HashAlg.SHA256, RSASignaturePadding.Pss);
                    }
                case JWKAlgorithms.PS384:
                    {
                        using RSA? rsa = GetRSAPublicKey(jwk);
                        return rsa != null && token.Verify(rsa, HashAlg.SHA384, RSASignaturePadding.Pss);
                    }
                case JWKAlgorithms.PS512:
                    {
                        using RSA? rsa = GetRSAPublicKey(jwk);
                        return rsa != null && token.Verify(rsa, HashAlg.SHA512, RSASignaturePadding.Pss);
                    }
                //Eccurves
                case JWKAlgorithms.ES256:
                    {
                        using ECDsa? eCDsa = GetECDsaPublicKey(jwk);
                        return eCDsa != null && token.Verify(eCDsa, HashAlg.SHA256);
                    }
                case JWKAlgorithms.ES384:
                    {
                        using ECDsa? eCDsa = GetECDsaPublicKey(jwk);
                        return eCDsa != null && token.Verify(eCDsa, HashAlg.SHA384);
                    }
                case JWKAlgorithms.ES512:
                    {
                        using ECDsa? eCDsa = GetECDsaPublicKey(jwk);
                        return eCDsa != null && token.Verify(eCDsa, HashAlg.SHA512);
                    }
                case JWKAlgorithms.ES256K:
                    {
                        using ECDsa? eCDsa = GetECDsaPublicKey(jwk);
                        return eCDsa != null && token.Verify(eCDsa, HashAlg.SHA256);
                    }
                default:
                    throw new EncryptionTypeNotSupportedException();
            }

        }
        
        /// <summary>
        /// Signs the <see cref="JsonWebToken"/> with the supplied JWK json element
        /// </summary>
        /// <param name="token"></param>
        /// <param name="jwk">The JWK in the <see cref="JsonElement"/> </param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="EncryptionTypeNotSupportedException"></exception>
        public static void SignFromJwk<T>(this JsonWebToken token, in T jwk) where T: notnull, IJsonWebKey
        {
            _ = token ?? throw new ArgumentNullException(nameof(token));

            //Make sure the key is used for signing/verification
            if (jwk.KeyUse != JwkKeyUsage.Signature)
            {
                throw new InvalidOperationException("The JWK cannot be used for signing");
            }

            //Alg is a required property
            if (jwk.Algorithm == null)
            {
                throw new InvalidOperationException("Algorithm or JWK use is null");
            }

            switch (jwk.Algorithm.ToUpper(null))
            {
                //Rsa witj pkcs and pss
                case JWKAlgorithms.RS256:
                    {
                        using RSA? rsa = GetRSAPrivateKey(jwk);
                        _ = rsa ?? throw new InvalidOperationException("JWK Does not contain an RSA private key");
                        token.Sign(rsa, HashAlg.SHA256, RSASignaturePadding.Pkcs1);
                        return;
                    }
                case JWKAlgorithms.RS384:
                    {
                        using RSA? rsa = GetRSAPrivateKey(jwk);
                        _ = rsa ?? throw new InvalidOperationException("JWK Does not contain an RSA private key");
                        token.Sign(rsa, HashAlg.SHA384, RSASignaturePadding.Pkcs1);
                        return;
                    }
                case JWKAlgorithms.RS512:
                    {
                        using RSA? rsa = GetRSAPrivateKey(jwk);
                        _ = rsa ?? throw new InvalidOperationException("JWK Does not contain an RSA private key");
                        token.Sign(rsa, HashAlg.SHA512, RSASignaturePadding.Pkcs1);
                        return;
                    }
                case JWKAlgorithms.PS256:
                    {
                        using RSA? rsa = GetRSAPrivateKey(jwk);
                        _ = rsa ?? throw new InvalidOperationException("JWK Does not contain an RSA private key");
                        token.Sign(rsa, HashAlg.SHA256, RSASignaturePadding.Pss);
                        return;
                    }
                case JWKAlgorithms.PS384:
                    {
                        using RSA? rsa = GetRSAPrivateKey(jwk);
                        _ = rsa ?? throw new InvalidOperationException("JWK Does not contain an RSA private key");
                        token.Sign(rsa, HashAlg.SHA384, RSASignaturePadding.Pss);
                        return;
                    }
                case JWKAlgorithms.PS512:
                    {
                        using RSA? rsa = GetRSAPrivateKey(jwk);
                        _ = rsa ?? throw new InvalidOperationException("JWK Does not contain an RSA private key");
                        token.Sign(rsa, HashAlg.SHA512, RSASignaturePadding.Pss);
                        return;
                    }
                //Eccurves
                case JWKAlgorithms.ES256:
                    {
                        using ECDsa? eCDsa = GetECDsaPrivateKey(jwk);
                        _ = eCDsa ?? throw new InvalidOperationException("JWK Does not contain an ECDsa private key");
                        token.Sign(eCDsa, HashAlg.SHA256);
                        return;
                    }
                case JWKAlgorithms.ES384:
                    {
                        using ECDsa? eCDsa = GetECDsaPrivateKey(jwk);
                        _ = eCDsa ?? throw new InvalidOperationException("JWK Does not contain an ECDsa private key");
                        token.Sign(eCDsa, HashAlg.SHA384);
                        return;
                    }
                case JWKAlgorithms.ES512:
                    {
                        using ECDsa? eCDsa = GetECDsaPrivateKey(jwk);
                        _ = eCDsa ?? throw new InvalidOperationException("JWK Does not contain an ECDsa private key");
                        token.Sign(eCDsa, HashAlg.SHA512);
                        return;
                    }
                case JWKAlgorithms.ES256K:
                    {
                        using ECDsa? eCDsa = GetECDsaPrivateKey(jwk);
                        _ = eCDsa ?? throw new InvalidOperationException("JWK Does not contain an ECDsa private key");
                        token.Sign(eCDsa, HashAlg.SHA256);
                        return;
                    }
                default:
                    throw new EncryptionTypeNotSupportedException();
            }
        }

        /// <summary>
        /// Gets the RSA public key algorithm from the supplied Json Web Key <see cref="JsonElement"/>
        /// </summary>
        /// <param name="jwk">The element that contains the JWK data</param>
        /// <returns>The <see cref="RSA"/> algorithm if found, or null if the element does not contain public key</returns>
        public static RSA? GetRSAPublicKey<TKey>(this TKey jwk) where TKey: IJsonWebKey
        {
            RSAParameters? rSAParameters = GetRsaParameters(in jwk, false);
            //Create rsa from params
            return rSAParameters.HasValue ? RSA.Create(rSAParameters.Value) : null;
        }

        /// <summary>
        /// Gets the RSA private key algorithm from the supplied Json Web Key
        /// </summary>
        /// <param name="jwk"></param>
        /// <returns>The <see cref="RSA"/> algorithm if found, or null if the element does not contain private key</returns>
        public static RSA? GetRSAPrivateKey<TKey>(this TKey jwk) where TKey: IJsonWebKey
        {
            RSAParameters? rSAParameters = GetRsaParameters(in jwk, true);
            //Create rsa from params
            return rSAParameters.HasValue ? RSA.Create(rSAParameters.Value) : null;
        }

        /// <summary>
        /// Gets the RSA key parameters from the current Json Web Key 
        /// </summary>>
        /// <param name="jwk"></param>
        /// <param name="includePrivateKey">A value that indicates that a private key should be parsed and included in the parameters</param>
        /// <returns>A nullable structure that contains the parsed keys, or null if required properties were empty</returns>
        public static RSAParameters? GetRsaParameters(this ReadOnlyJsonWebKey jwk, bool includePrivateKey)
            => GetRsaParameters(in jwk, includePrivateKey);

        /// <summary>
        /// Gets the RSA key parameters from the current Json Web Key 
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="jwk"></param>
        /// <param name="includePrivateKey">A value that indicates that a private key should be parsed and included in the parameters</param>
        /// <returns>A nullable structure that contains the parsed keys, or null if required properties were empty</returns>
        public static RSAParameters? GetRsaParameters<TKey>(in TKey jwk, bool includePrivateKey) where TKey : IJsonWebKey
        {
            //Get the RSA public key credentials
            ReadOnlySpan<char> e = jwk.GetKeyProperty("e");
            ReadOnlySpan<char> n = jwk.GetKeyProperty("n");

            if (e.IsEmpty || n.IsEmpty)
            {
                return null;
            }

            if (includePrivateKey)
            {
                //Get optional private key params
                ReadOnlySpan<char> d = jwk.GetKeyProperty("d");
                ReadOnlySpan<char> dp = jwk.GetKeyProperty("dq");
                ReadOnlySpan<char> dq = jwk.GetKeyProperty("dp");
                ReadOnlySpan<char> p = jwk.GetKeyProperty("p");
                ReadOnlySpan<char> q = jwk.GetKeyProperty("q");

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
        /// Gets the ECDsa public key algorithm from the supplied Json Web Key <see cref="JsonElement"/>
        /// </summary>
        /// <param name="jwk">The public key element</param>
        /// <returns>The <see cref="ECDsa"/> algorithm from the key if loaded, null if no key data was found</returns>
        public static ECDsa? GetECDsaPublicKey<TKey>(this TKey jwk) where TKey : IJsonWebKey
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
        public static ECDsa? GetECDsaPrivateKey<TKey>(this TKey jwk) where TKey : IJsonWebKey
        {
            //Get the EC params
            ECParameters? ecParams = GetECParameters(in jwk, true);
            //Return new alg
            return ecParams.HasValue ? ECDsa.Create(ecParams.Value) : null;
        }

        /// <summary>
        /// Gets the EC key parameters from the current Json Web Key 
        /// </summary>
        /// <param name="jwk"></param>
        /// <param name="includePrivate">A value that inidcates if private key parameters should be parsed and included </param>
        /// <returns>The parsed key parameter structure, or null if the key parameters were empty or could not be parsed</returns>
        public static ECParameters? GetECParameters(this ReadOnlyJsonWebKey jwk, bool includePrivate) => GetECParameters(in jwk, includePrivate);

        /// <summary>
        /// Gets the EC key parameters from the current Json Web Key 
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="jwk"></param>
        /// <param name="includePrivate">A value that inidcates if private key parameters should be parsed and included </param>
        /// <returns>The parsed key parameter structure, or null if the key parameters were empty or could not be parsed</returns>
        public static ECParameters? GetECParameters<TKey>(in TKey jwk, bool includePrivate) where TKey : IJsonWebKey
        {
            //Get the RSA public key credentials
            ReadOnlySpan<char> x = jwk.GetKeyProperty("x");
            ReadOnlySpan<char> y = jwk.GetKeyProperty("y");

            //Optional private key
            ReadOnlySpan<char> d = includePrivate ? jwk.GetKeyProperty("d") : null;

            if (x.IsEmpty || y.IsEmpty)
            {
                return null;
            }
            
            ECCurve curve;
            //Get the EC curve name from the curve ID
            switch (jwk.GetKeyProperty("crv")?.ToUpper(null))
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
                case "SECP256K1":
                    curve = ManagedHash.CurveSecp256k1;
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

            //Use stack buffer
            if(base64.Length <= 64)
            {
                //Use stack buffer
                Span<byte> buffer = stackalloc byte[64];

                //base64url decode
                ERRNO count = VnEncoding.Base64UrlDecode(base64, buffer);

                //Return buffer or null if failed
                return count ? buffer[0.. (int)count].ToArray() : null;
            }
            else
            {
                //bin buffer for temp decoding with some extra space just incase
                using UnsafeMemoryHandle<byte> binBuffer = MemoryUtil.UnsafeAlloc(base64.Length + 16, false);

                //base64url decode
                ERRNO count = VnEncoding.Base64UrlDecode(base64, binBuffer.Span);

                //Return buffer or null if failed
                return count ? binBuffer.AsSpan(0, count).ToArray() : null;
            }
        }
    }
}
