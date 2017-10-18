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
using CBAM.Abstractions;
using UtilPack.AsyncEnumeration;

using TCreationParameter = UtilPack.EitherOr<CBAM.HTTP.HTTPRequest, System.Func<CBAM.HTTP.HTTPRequest>>;
namespace CBAM.HTTP
{
   /// <summary>
   /// This interface extends <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TEnumerable}"/> to provide a way to create statements which enable sending <see cref="HTTPRequest"/>s to client.
   /// </summary>
   public interface HTTPConnection : Connection<HTTPStatement, HTTPStatementInformation, TCreationParameter, HTTPResponse, HTTPConnectionVendorFunctionality, IAsyncConcurrentEnumerable<HTTPResponse>>
   {

   }

   /// <summary>
   /// This interface extends <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/> to provide any HTTP-specific functionality which does not need a connection.
   /// </summary>
   public interface HTTPConnectionVendorFunctionality : ConnectionVendorFunctionality<HTTPStatement, TCreationParameter>
   {

   }

}
