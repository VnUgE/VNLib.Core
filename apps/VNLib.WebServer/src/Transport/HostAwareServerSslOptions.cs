/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: HostAwareServerSslOptions.cs 
*
* HostAwareServerSslOptions.cs is part of VNLib.WebServer which is part 
* of the larger VNLib collection of libraries and utilities.
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
using System.Net.Security;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using VNLib.WebServer.Config;
using VNLib.WebServer.Config.Model;

namespace VNLib.WebServer.Transport
{

    internal sealed class HostAwareServerSslOptions : SslServerAuthenticationOptions
    {
        //TODO programatically setup ssl protocols, but for now we only use HTTP/1.1 so this can be hard-coded
        internal static readonly List<SslApplicationProtocol> SslAppProtocols = new()
        {
            SslApplicationProtocol.Http11,
            //SslApplicationProtocol.Http2,
        };
     
        private readonly bool _clientCertRequired;
        private readonly X509Certificate _cert;

        private readonly SslPolicyErrors _errorLevel;

        public HostAwareServerSslOptions(TransportInterface iFace)
        {
            ArgumentNullException.ThrowIfNull(iFace);
            Validate.Assert(iFace.Ssl, "An interface was selected that does not have SSL enabled. This is likely a bug");

            _clientCertRequired = iFace.ClientCertRequired;
            _cert = iFace.LoadCertificate()!;

            /*
            * If client certificates are required, then no policy errors are allowed
            * and the certificate must be requested from the user. Otherwise, no errors
            * except missing certificates are allowed
            */
            _errorLevel = _clientCertRequired
                ? SslPolicyErrors.None
                : SslPolicyErrors.RemoteCertificateNotAvailable;

            //Set validation callback
            RemoteCertificateValidationCallback = OnRemoteCertVerification;
            ServerCertificateSelectionCallback = OnGetCertificatForHost;

            ConfigureBaseDefaults(iFace.UseOsCiphers);
        }

        private void ConfigureBaseDefaults(bool doNotForceProtocols)
        {
            //Eventually when HTTP2 is supported, we can select the ssl version to match
            ApplicationProtocols = SslAppProtocols;

            AllowRenegotiation = false;
            EncryptionPolicy = EncryptionPolicy.RequireEncryption;

            //Allow user to disable forced protocols and let the os decide
            EnabledSslProtocols = doNotForceProtocols
                ? SslProtocols.None
                : SslProtocols.Tls12 | SslProtocols.Tls13;
        }

        private bool OnRemoteCertVerification(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            /*
             * Since certificates are loaded at the interface level, an interface is defined at the virtual host level
             * and can only accept one certificate per virtual host. So SNI is not useful here since certificates are
             * verified at the interface level.
             */

            return _errorLevel == sslPolicyErrors;
        }

        /*
         * Callback for getting the certificate from a hostname
         * 
         * Always used the certificate defined at the interface level
         */
        private X509Certificate OnGetCertificatForHost(object sender, string? hostName) => _cert;

    }
}
