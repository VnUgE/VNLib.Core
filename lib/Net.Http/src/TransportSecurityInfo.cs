/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Http
* File: TransportSecurityInfo.cs 
*
* TransportSecurityInfo.cs is part of VNLib.Net.Http which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Net.Http is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Http is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;


namespace VNLib.Net.Http
{

    /// <summary>
    /// Gets the transport TLS security information for the current connection
    /// </summary>
    public readonly struct TransportSecurityInfo
    {
        /// <summary>
        /// Gets a Boolean value that indicates whether the certificate revocation list is checked during the certificate validation process.
        /// </summary>
        /// <returns>true if the certificate revocation list is checked during validation; otherwise, false.</returns>
        public readonly bool CheckCertRevocationStatus { get; init; }

        /// <summary>
        /// Gets a value that identifies the bulk encryption algorithm used by the connection.
        /// </summary>
        public readonly CipherAlgorithmType CipherAlgorithm { get; init; }

        /// <summary>
        /// Gets a value that identifies the strength of the cipher algorithm used by the connection.
        /// </summary>
        public readonly int CipherStrength { get; init; }

        /// <summary>
        /// Gets the algorithm used for generating message authentication codes (MACs).
        /// </summary>
        public readonly HashAlgorithmType HashAlgorithm { get; init; }

        /// <summary>
        /// Gets a value that identifies the strength of the hash algorithm used by this instance.
        /// </summary>
        public readonly int HashStrength { get; init; }

        /// <summary>
        /// Gets a Boolean value that indicates whether authentication was successful.
        /// </summary>
        public readonly bool IsAuthenticated { get; init; }

        /// <summary>
        /// Gets a Boolean value that indicates whether this connection uses data encryption.
        /// </summary>
        public readonly bool IsEncrypted { get; init; }

        /// <summary>
        /// Gets a Boolean value that indicates whether both server and client have been authenticated.
        /// </summary>
        public readonly bool IsMutuallyAuthenticated { get; init; }

        /// <summary>
        /// Gets a Boolean value that indicates whether the data sent using this connection is signed.
        /// </summary>
        public readonly bool IsSigned { get; init; }

        /// <summary>
        /// Gets the key exchange algorithm used by this connection
        /// </summary>
        public readonly ExchangeAlgorithmType KeyExchangeAlgorithm { get; init; }

        /// <summary>
        /// Gets a value that identifies the strength of the key exchange algorithm used by the transport connection
        /// </summary>
        public readonly int KeyExchangeStrength { get; init; }

        /// <summary>
        /// Gets the certificate used to authenticate the local endpoint.
        /// </summary>
        public readonly X509Certificate? LocalCertificate { get; init; }

        /// <summary>
        /// The negotiated application protocol in TLS handshake.
        /// </summary>
        public readonly SslApplicationProtocol NegotiatedApplicationProtocol { get; init; }

        /// <summary>
        /// Gets the cipher suite which was negotiated for this connection.
        /// </summary>
        public readonly TlsCipherSuite NegotiatedCipherSuite { get; init; }

        /// <summary>
        /// Gets the certificate used to authenticate the remote endpoint.
        /// </summary>
        public readonly X509Certificate? RemoteCertificate { get; init; }

        /// <summary>
        /// Gets the TransportContext used for authentication using extended protection.
        /// </summary>
        public readonly TransportContext TransportContext { get; init; }
    }
}
