/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Net.Transport.SimpleTCP
* File: TcpServerExtensions.cs 
*
* TcpServerExtensions.cs is part of VNLib.Net.Transport.SimpleTCP which is
* part of the larger VNLib collection of libraries and utilities.
*
* VNLib.Net.Transport.SimpleTCP is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 2 of the
* License, or (at your option) any later version.
*
* VNLib.Net.Transport.SimpleTCP is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Threading;
using System.Net.Security;
using System.Threading.Tasks;
using System.Security.Authentication;
using System.Runtime.CompilerServices;

namespace VNLib.Net.Transport.Tcp
{
    /// <summary>
    /// Exposes extension methods for <see cref="TcpServer"/>
    /// </summary>
    public static class TcpServerExtensions
    {

        /// <summary>
        /// Accepts a new ssl connection and attemts to authenticate it as a server
        /// </summary>
        /// <param name="server"></param>
        /// <param name="options">The ssl server authentication options used to initalize the connection</param>
        /// <param name="cancellation">A token to cancel the async accept operation</param>
        /// <returns>A <see cref="ValueTask"/> that resolve the <see cref="TransportEventContext"/> around the connection</returns>
        /// <exception cref="AuthenticationException"></exception>
        public static async Task<TransportEventContext> AcceptSslAsync(this TcpServer server, SslServerAuthenticationOptions options, CancellationToken cancellation = default)
        {
            //accept internal args
            ITcpConnectionDescriptor args = await server.AcceptConnectionAsync(cancellation);

            //Begin authenication and make sure the socket stream is closed as its required to cleanup
            SslStream stream = new(args.GetStream(), false);
            try
            {
                //auth the new connection
                await stream.AuthenticateAsServerAsync(options, cancellation);
                return new(args, stream);
            }
            catch (Exception ex)
            {
                //Cleanup the socket when auth fails
                await stream.DisposeAsync();

                //Disconnect socket 
                args.CloseConnection();

                throw new AuthenticationException("Failed client/server TLS authentication", ex);
            }
        }

        /// <summary>
        /// Safely closes an ssl connection
        /// </summary>
        /// <param name="ctx">The context to close the connection on</param>
        /// <returns>A value task that completes when the connection is closed</returns>
        public static async ValueTask CloseSslConnectionAsync(this TransportEventContext ctx)
        {
            try
            {
                //Close the ssl stream
                await (ctx.ConnectionStream as SslStream)!.ShutdownAsync();
            }
            finally
            {
                //Always close the connection
                await ctx.CloseConnectionAsync();
            }
        }

        /// <summary>
        /// Gets the SslProtocol for an ssl connection
        /// </summary>
        /// <param name="ctx">The <see cref="TransportEventContext"/> that contains the ssl connection stream</param>
        /// <returns>The current <see cref="SslProtocols"/> of the connection</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SslProtocols GetSslProtocol(this in TransportEventContext ctx)
        {
            return (ctx.ConnectionStream as SslStream)!.SslProtocol;
        }

    }
}