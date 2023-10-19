/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: SessionBase.cs 
*
* SessionBase.cs is part of VNLib.Plugins.Essentials which is part of the larger 
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
using System.Net;
using System.Runtime.CompilerServices;

using VNLib.Utils;

namespace VNLib.Plugins.Essentials.Sessions
{
    /// <summary>
    /// Provides a base class for the <see cref="ISession"/> interface that handles basic housekeeping
    /// context
    /// </summary>
    public abstract class SessionBase : ISession
    {
        protected const ulong MODIFIED_MSK =    0b0000000000000001UL;
        protected const ulong IS_NEW_MSK =      0b0000000000000010UL;
        protected const ulong REGEN_ID_MSK =    0b0000000000000100UL;
        protected const ulong INVALID_MSK =     0b0000000000001000UL;
        protected const ulong DETACHED_MSK =    0b0000000000010000UL;
        protected const ulong ALL_INVALID_MSK = 0b0000000000100000UL;

        protected const string USER_ID_ENTRY = "__.i.uid";
        protected const string TOKEN_ENTRY = "__.i.tk";
        protected const string PRIV_ENTRY = "__.i.pl";
        protected const string IP_ADDRESS_ENTRY = "__.i.uip";
        protected const string SESSION_TYPE_ENTRY = "__.i.tp";

        /// <summary>
        /// A <see cref="BitField"/> of status flags for the state of the current session.
        /// May be used internally
        /// </summary>
        protected BitField Flags { get; } = new(0);

        /// <summary>
        /// Gets or sets the Modified flag
        /// </summary>
        protected bool IsModified
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Flags.IsSet(MODIFIED_MSK);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Flags.Set(MODIFIED_MSK, value);
        }

        ///<inheritdoc/>
        public abstract string SessionID { get; }
        ///<inheritdoc/>
        public abstract DateTimeOffset Created { get; set; }

#nullable disable

        ///<inheritdoc/>
        ///<exception cref="ObjectDisposedException"></exception>
        public string this[string index]
        {
            get => IndexerGet(index);
            set => IndexerSet(index, value);
        }

        ///<inheritdoc/>
        public virtual IPAddress UserIP
        {
            get
            {
                //try to parse the IP address, otherwise return null
                _ = IPAddress.TryParse(this[IP_ADDRESS_ENTRY], out IPAddress? ip);
                return ip;
            }
            protected set
            {
                //Store the IP address as its string representation
                this[IP_ADDRESS_ENTRY] = value?.ToString();
            }
        }
        ///<inheritdoc/>
        public virtual SessionType SessionType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Enum.Parse<SessionType>(this[SESSION_TYPE_ENTRY]);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            protected set => this[SESSION_TYPE_ENTRY] = value.ToString();
        }
     
        ///<inheritdoc/>
        public virtual ulong Privilages
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => string.IsNullOrWhiteSpace(this[PRIV_ENTRY]) ? 0 : Convert.ToUInt64(this[PRIV_ENTRY], 16);
            //Store in hexadecimal to conserve space
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this[PRIV_ENTRY] = value.ToString("X");
        }
        ///<inheritdoc/>
        public bool IsNew
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Flags.IsSet(IS_NEW_MSK);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Flags.Set(IS_NEW_MSK, value);
        }
        ///<inheritdoc/>
        public virtual string UserID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this[USER_ID_ENTRY];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this[USER_ID_ENTRY] = value;
        }
        ///<inheritdoc/>
        public virtual string Token
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this[TOKEN_ENTRY];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this[TOKEN_ENTRY] = value;
        }

        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void Invalidate(bool all = false)
        {
            Flags.Set(INVALID_MSK);
            Flags.Set(ALL_INVALID_MSK, all);
        }
        ///<inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual void RegenID() => Flags.Set(REGEN_ID_MSK);

        /// <inheritdoc/>
        public virtual void Detach() => Flags.Set(DETACHED_MSK);

        /// <summary>
        /// Invoked when the indexer is is called to 
        /// </summary>
        /// <param name="key">The key/index to get the value for</param>
        /// <returns>The value stored at the specified key</returns>
        protected abstract string IndexerGet(string key);
        /// <summary>
        /// Sets a value requested by the indexer
        /// </summary>
        /// <param name="key">The key to associate the value with</param>
        /// <param name="value">The value to store</param>
        protected abstract void IndexerSet(string key, string value);
    }
}