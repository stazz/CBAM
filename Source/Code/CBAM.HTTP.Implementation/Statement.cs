/*
 * Copyright 2017 Stanislav Muhametsin. All rights Reserved.
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
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.HTTP.Implementation
{
   internal sealed class Reference<T>
   {
      public T Value { get; set; }
   }

   internal sealed class HTTPStatementInformationImpl<TRequestMetaData> : HTTPStatementInformation<TRequestMetaData>
   {
      private readonly Reference<Func<HTTPResponseInfo<TRequestMetaData>, ValueTask<HTTPRequestInfo<TRequestMetaData>>>> _nextRequestGenerator;
      private readonly HTTPRequestInfo<TRequestMetaData> _initialRequest;

      public HTTPStatementInformationImpl(
         HTTPRequestInfo<TRequestMetaData> initialRequest,
         Reference<Func<HTTPResponseInfo<TRequestMetaData>, ValueTask<HTTPRequestInfo<TRequestMetaData>>>> generator
         )
      {
         this._initialRequest = initialRequest;
         ArgumentValidator.ValidateNotNull( nameof( initialRequest.Request ), initialRequest.Request );
         this._nextRequestGenerator = ArgumentValidator.ValidateNotNull( nameof( generator ), generator );
      }

      public TRequestMetaData InitialRequestMetaData => this._initialRequest.RequestMetaData;

      public HTTPRequest InitialRequest => this._initialRequest.Request;

      //public HTTPRequestInfo<TRequestMetaData> InitialRequest { get; }

      public Func<HTTPResponseInfo<TRequestMetaData>, ValueTask<HTTPRequestInfo<TRequestMetaData>>> NextRequestGenerator => this._nextRequestGenerator.Value;
   }

   internal sealed class HTTPStatementImpl<TRequestMetaData> : HTTPStatement<TRequestMetaData>
   {
      //private const Int32 INITIAL = 0;
      //private const Int32 RETURNING = 1;
      //private const Int32 DONE = 2;

      private readonly Reference<Func<HTTPResponseInfo<TRequestMetaData>, ValueTask<HTTPRequestInfo<TRequestMetaData>>>> _nextRequestGenerator;

      public HTTPStatementImpl(
         HTTPRequestInfo<TRequestMetaData> initialRequest
         )
      {
         //var state = INITIAL;
         this.InitialRequest = initialRequest;
         this.Information = new HTTPStatementInformationImpl<TRequestMetaData>(
            initialRequest,
            this._nextRequestGenerator = new Reference<Func<HTTPResponseInfo<TRequestMetaData>, ValueTask<HTTPRequestInfo<TRequestMetaData>>>>()
            );

         //   () =>
         //{
         //   var generator = this.MessageGenerator;

         //   HTTPRequest retVal;
         //   if ( generator != null )
         //   {
         //      retVal = generator();
         //   }
         //   else if ( Interlocked.CompareExchange( ref state, RETURNING, INITIAL ) == INITIAL )
         //   {
         //      try
         //      {
         //         retVal = this.StaticMessage;
         //      }
         //      finally
         //      {
         //         Interlocked.Exchange( ref state, DONE );
         //      }
         //   }
         //   else
         //   {
         //      Interlocked.CompareExchange( ref state, DONE, INITIAL );
         //      retVal = null;
         //   }

         //   return retVal;
         //} );
      }

      public HTTPRequestInfo<TRequestMetaData> InitialRequest { get; }

      public TRequestMetaData InitialRequestMetaData => this.InitialRequest.RequestMetaData;

      public Func<HTTPResponseInfo<TRequestMetaData>, ValueTask<HTTPRequestInfo<TRequestMetaData>>> NextRequestGenerator
      {
         get => this._nextRequestGenerator.Value;
         set => this._nextRequestGenerator.Value = value;
      }

      //public HTTPRequest StaticMessage { get; set; }
      //public Func<HTTPRequest> MessageGenerator { get; set; }

      public HTTPStatementInformation<TRequestMetaData> Information { get; }

      //Func<HTTPRequest> HTTPStatementInformation.MessageGenerator => this.MessageGenerator;
   }
}
