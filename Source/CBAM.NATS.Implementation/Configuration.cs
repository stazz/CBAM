/*
 * Copyright 2018 Stanislav Muhametsin. All rights Reserved.
 *
 * Licensed  under the  Apache License,  Version 2.0  (the "License");
 * you may not use  this file  except in  compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed  under the  License is distributed on an "AS IS" BASIS,
 * WITHOUT  WARRANTIES OR CONDITIONS  OF ANY KIND, either  express  or
 * implied.
 *
 * See the License for the specific language governing permissions and
 * limitations under the License. 
 */
using CBAM.Abstractions.Implementation;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.ResourcePooling;
using UtilPack.ResourcePooling.NetworkStream;
using UtilPack.Configuration.NetworkStream;

namespace CBAM.NATS
{

   public sealed class NATSConnectionCreationInfo : NetworkConnectionCreationInfo<NATSConnectionCreationInfoData, NATSConnectionConfiguration, NATSInitializationConfiguration, NATSProtocolConfiguration, NATSPoolingConfiguration, NATSAuthenticationConfiguration>
   {
      public NATSConnectionCreationInfo( NATSConnectionCreationInfoData data )
         : base( data )
      {
      }
   }

   public sealed class NATSConnectionCreationInfoData : NetworkConnectionCreationInfoData<NATSConnectionConfiguration, NATSInitializationConfiguration, NATSProtocolConfiguration, NATSPoolingConfiguration, NATSAuthenticationConfiguration>
   {
   }

   public sealed class NATSConnectionConfiguration : NetworkConnectionConfiguration
   {
   }

   public sealed class NATSInitializationConfiguration : NetworkInitializationConfiguration<NATSProtocolConfiguration, NATSPoolingConfiguration, NATSAuthenticationConfiguration>
   {
   }

   public sealed class NATSProtocolConfiguration
   {
      public const Int32 DEFAULT_BUFFER_SIZE = 0x10000;

      public Boolean Verbose { get; set; }

      public Boolean Pedantic { get; set; }

      public String ClientName { get; set; } = "CBAM.NATS";

      public String ClientLanguage { get; set; } = "CLR";

      public String ClientVersion { get; set; } = "0.1";

      public Int32 StreamBufferSize { get; set; } = DEFAULT_BUFFER_SIZE;
   }

   public sealed class NATSPoolingConfiguration : NetworkPoolingConfiguration
   {
   }

   public sealed class NATSAuthenticationConfiguration
   {
      internal static readonly Encoding PasswordByteEncoding = new UTF8Encoding( false, true );

      private Byte[] _pwBytes;
      private Byte[] _tokenBytes;

      public String Username { get; set; }
      public String Password
      {
         get
         {
            var arr = this._pwBytes;
            return arr == null ? null : PasswordByteEncoding.GetString( arr, 0, arr.Length );
         }
         set
         {
            this._pwBytes = value == null ? null : PasswordByteEncoding.GetBytes( value );
         }
      }

      public String AuthenticationToken
      {
         get
         {
            var arr = this._tokenBytes;
            return arr == null ? null : PasswordByteEncoding.GetString( arr, 0, arr.Length );
         }
         set
         {
            this._tokenBytes = value == null ? null : PasswordByteEncoding.GetBytes( value );
         }
      }
   }
}
