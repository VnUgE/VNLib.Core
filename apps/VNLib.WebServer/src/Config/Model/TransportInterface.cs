/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: TransportInterface.cs 
*
* TransportInterface.cs is part of VNLib.WebServer which is part of
* the larger VNLib collection of libraries and utilities.
*
* VNLib.WebServer is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.WebServer is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.WebServer. If not, see http://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

using VNLib.Utils.Memory;
using VNLib.Utils.Resources;

namespace VNLib.WebServer.Config.Model
{
    /// <summary>
    /// Represents a transport interface configuration element for a virtual host
    /// </summary>
    internal class TransportInterface
    {
        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("certificate")]
        public string? Cert { get; set; }

        [JsonPropertyName("private_key")]
        public string? PrivKey { get; set; } 

        [JsonPropertyName("ssl")]
        public bool Ssl { get; set; }

        [JsonPropertyName("client_cert_required")]
        public bool ClientCertRequired { get; set; }

        [JsonPropertyName("password")]
        public string? PrivKeyPassword { get; set; }

        [JsonPropertyName("use_os_ciphers")]
        public bool UseOsCiphers { get; set; }

        public IPEndPoint GetEndpoint()
        {
            IPAddress addr = string.IsNullOrEmpty(Address) ? IPAddress.Any : IPAddress.Parse(Address);
            return new IPEndPoint(addr, Port);
        }

        public X509Certificate? LoadCertificate()
        {
            if (!Ssl)
            {
                return null;
            }

            Validate.EnsureNotNull(Cert, "TLS Certificate is required when ssl is enabled");
            Validate.FileExists(Cert);

            X509Certificate? cert = null;

            /*
             * Default to use a PEM encoded certificate and private key file. Unless the file
             * is a pfx file, then we will use the private key from the pfx file.
             */

            if (Path.GetExtension(Cert).EndsWith("pfx", StringComparison.OrdinalIgnoreCase))
            {
                //Create from pfx file including private key
                cert = X509Certificate.CreateFromCertFile(Cert);
            }
            else
            {
                Validate.EnsureNotNull(PrivKey, "TLS Private Key is required ssl is enabled");
                Validate.FileExists(PrivKey);

                /*
                 * Attempt to capture the private key password. This will wrap the 
                 * string in a private string instance, and setting the value to true
                 * will ensure the password memory is wiped when this function returns
                 */
                using PrivateString? password = PrivateString.ToPrivateString(PrivKeyPassword, true);

                //Load the cert and decrypt with password if set
                using X509Certificate2 cert2 = password == null ? X509Certificate2.CreateFromPemFile(Cert, PrivKey)
                        : X509Certificate2.CreateFromEncryptedPemFile(Cert, password.ToReadOnlySpan(), PrivKey);

                /*
                 * Workaround for a silly Windows SecureChannel module bug for parsing 
                 * X509Certificate2 from pem cert and private key files. 
                 * 
                 * Must export into pkcs12 format then create a new X509Certificate2 from the 
                 * exported bytes. 
                 */

                //Copy the cert in pkcs12 format
                byte[] pkcs = cert2.Export(X509ContentType.Pkcs12);
                cert = new X509Certificate2(pkcs);
                MemoryUtil.InitializeBlock(pkcs);
            }

            return cert;
        }

        /// <summary>
        /// Builds a deterministic hash-code base on the configuration state. 
        /// </summary>
        /// <returns>The hash-code that represents the current instance</returns>
        public override int GetHashCode() => HashCode.Combine(Address, Port);

        public override bool Equals(object? obj) => obj is TransportInterface iface && GetHashCode() == iface.GetHashCode();

        public override string ToString() => $"[{Address}:{Port}]";

    }
}
