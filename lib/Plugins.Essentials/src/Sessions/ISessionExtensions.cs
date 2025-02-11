/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: ISessionExtensions.cs 
*
* ISessionExtensions.cs is part of VNLib.Plugins.Essentials which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Security.Authentication;
using System.Runtime.CompilerServices;

using VNLib.Net.Http;
using VNLib.Utils;
using VNLib.Utils.Extensions;
using VNLib.Plugins.Essentials.Extensions;

namespace VNLib.Plugins.Essentials.Sessions
{
    public static class ISessionExtensions
    {
        public const string USER_AGENT_ENTRY = "__.ua";
        public const string ORIGIN_ENTRY = "__.org";
        public const string REFER_ENTRY = "__.rfr";
        public const string SECURE_ENTRY = "__.sec";
        public const string CROSS_ORIGIN = "__.cor";
        public const string LOCAL_TIME_ENTRY = "__.lot";     

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetUserAgent(this ISession session) => session[USER_AGENT_ENTRY];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetUserAgent(this ISession session, string? userAgent) => session[USER_AGENT_ENTRY] = userAgent;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetOrigin(this ISession session) => session[ORIGIN_ENTRY];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Uri? GetOriginUri(this ISession session) => Uri.TryCreate(session[ORIGIN_ENTRY], UriKind.Absolute, out Uri? origin) ? origin : null;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetOrigin(this ISession session, string origin) => session[ORIGIN_ENTRY] = origin;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetRefer(this ISession session) => session[REFER_ENTRY];
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetRefer(this ISession session, string? refer) => session[REFER_ENTRY] = refer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SslProtocols GetSecurityProtocol(this ISession session) => (SslProtocols)session.GetValueType<string, int>(SECURE_ENTRY);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSecurityProtocol(this ISession session, SslProtocols protocol) => session[SECURE_ENTRY] = VnEncoding.ToBase32String((int)protocol);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCrossOrigin(this ISession session) => session[CROSS_ORIGIN] == bool.TrueString;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IsCrossOrigin(this ISession session, bool crossOrign) => session[CROSS_ORIGIN] = crossOrign.ToString();

        /// <summary>
        /// Initializes a "new" session with initial varaibles from the current connection
        /// for lookup/comparison later
        /// </summary>
        /// <param name="session"></param>
        /// <param name="ci">The <see cref="IConnectionInfo"/> object containing connection details</param>
        public static void InitNewSession(this ISession session, IConnectionInfo ci)
        {
            session.IsCrossOrigin(ci.CrossOrigin);
            session.SetRefer(ci.Referer?.ToString());
            session.SetSecurityProtocol(ci.GetSslProtocol());
            session.SetUserAgent(ci.UserAgent);

            /*
             * If no origin is specified, then we can use the authority of 
             * our current virtual host because it cannot be a cross-origin 
             * request.
             */
            if(ci.Origin is null)
            {
                string scheme = ci.RequestUri.Scheme;
                string authority = ci.RequestUri.Authority;

                session.SetOrigin($"{scheme}{authority}");
            }
            else
            {
                session.SetOrigin(ci.Origin.ToString());
            }
        }
       
    }
}