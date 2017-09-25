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
using UtilPack;

namespace CBAM.HTTP.Implementation
{
   internal sealed class HTTPStatementInformationImpl : HTTPStatementInformation
   {
      public HTTPStatementInformationImpl( Func<HTTPRequest> generator )
      {
         this.MessageGenerator = ArgumentValidator.ValidateNotNull( nameof( generator ), generator );
      }

      public Func<HTTPRequest> MessageGenerator { get; }
   }

   internal sealed class HTTPStatementImpl : HTTPStatement
   {
      private const Int32 INITIAL = 0;
      private const Int32 RETURNING = 1;
      private const Int32 DONE = 2;
      public HTTPStatementImpl()
      {
         var state = INITIAL;
         this.Information = new HTTPStatementInformationImpl( () =>
         {
            var generator = this.MessageGenerator;

            HTTPRequest retVal;
            if ( generator != null )
            {
               retVal = generator();
            }
            else if ( Interlocked.CompareExchange( ref state, RETURNING, INITIAL ) == INITIAL )
            {
               try
               {
                  retVal = this.StaticMessage;
               }
               finally
               {
                  Interlocked.Exchange( ref state, DONE );
               }
            }
            else
            {
               Interlocked.CompareExchange( ref state, DONE, INITIAL );
               retVal = null;
            }

            return retVal;
         } );
      }

      public HTTPRequest StaticMessage { get; set; }
      public Func<HTTPRequest> MessageGenerator { get; set; }

      public HTTPStatementInformation Information { get; }

      //Func<HTTPRequest> HTTPStatementInformation.MessageGenerator => this.MessageGenerator;
   }
}
