/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.WebServer
* File: SslTcpTransportContext.cs 
*
* SslTcpTransportContext.cs is part of VNLib.WebServer which is part of the larger 
* VNLib collection of libraries and utilities.
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


using System.Net.Security;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using VNLib.Net.Http;
using VNLib.Net.Transport.Tcp;

namespace VNLib.WebServer.Transport
{
    internal sealed class SslTcpTransportContext(ITcpListner server, ITcpConnectionDescriptor descriptor, SslStream stream) 
        : TcpTransportContext(server, descriptor, stream)
    {
        private TransportSecurityInfo? _securityInfo;
        private readonly SslStream _baseStream = stream;

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async override ValueTask CloseConnectionAsync()
        {
            try
            {
                //Shutdown the ssl stream before cleaning up the connection
                await _baseStream.ShutdownAsync();
                await _connectionStream.DisposeAsync();
            }
            finally
            {
                //Always close the underlying connection
                await base.CloseConnectionAsync();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override ref readonly TransportSecurityInfo? GetSecurityInfo()
        {
            //Value has not been loaded yet, so lazy load it
            if (!_securityInfo.HasValue)
            {
                //Create sec info from the ssl stream
                GetSecInfo(ref _securityInfo, _baseStream);
            }

            return ref _securityInfo;
        }


        //Lazy load sec info
        private static void GetSecInfo(ref TransportSecurityInfo? tsi, SslStream ssl)
        {
            //Build sec info
            tsi = new()
            {
                SslProtocol = ssl.SslProtocol,
                HashAlgorithm = ssl.HashAlgorithm,
                CipherAlgorithm = ssl.CipherAlgorithm,

                HashStrength = ssl.HashStrength,
                CipherStrength = ssl.CipherStrength,

                IsSigned = ssl.IsSigned,
                IsEncrypted = ssl.IsEncrypted,
                IsAuthenticated = ssl.IsAuthenticated,
                IsMutuallyAuthenticated = ssl.IsMutuallyAuthenticated,
                CheckCertRevocationStatus = ssl.CheckCertRevocationStatus,

                KeyExchangeStrength = ssl.KeyExchangeStrength,
                KeyExchangeAlgorithm = ssl.KeyExchangeAlgorithm,

                LocalCertificate = ssl.LocalCertificate,
                RemoteCertificate = ssl.RemoteCertificate,

                TransportContext = ssl.TransportContext,
                NegotiatedCipherSuite = ssl.NegotiatedCipherSuite,
                NegotiatedApplicationProtocol = ssl.NegotiatedApplicationProtocol,               
            };
        }
    }
}
