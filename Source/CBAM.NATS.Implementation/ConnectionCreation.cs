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

namespace CBAM.NATS.Implementation
{



   using TNetworkStreamInitState = ValueTuple<ClientProtocol.ReadState, Reference<ServerInformation>, CancellationToken, Stream>;

   internal sealed class ClientProtocolPoolInfo : PooledConnectionFunctionality
   {

      private Object _cancellationToken;

      public ClientProtocolPoolInfo( ClientProtocol protocol )
      {
         //this.Socket = ArgumentValidator.ValidateNotNull( nameof( socket ), socket );
         this.Protocol = ArgumentValidator.ValidateNotNull( nameof( protocol ), protocol );
      }

      public ClientProtocol Protocol { get; }

      //public Socket Socket { get; }

      public CancellationToken CurrentCancellationToken
      {
         get => (CancellationToken) this._cancellationToken;
         set => Interlocked.Exchange( ref this._cancellationToken, value );
      }

      public Boolean CanBeReturnedToPool => this.Protocol.CanBeReturnedToPool;

      public void ResetCancellationToken()
      {
         this._cancellationToken = null;
      }
   }

}
