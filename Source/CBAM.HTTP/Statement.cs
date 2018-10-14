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
using System.Threading.Tasks;

namespace CBAM.HTTP
{
   /// <summary>
   /// This is read-only interface for <see cref="HTTPStatement{TRequestMetaData}"/>. Right now, it has no public API.
   /// </summary>
   /// <typeparam name="TRequestMetaData">The type of metadata associated with each request, used in identifying the request that response is associated with. Typically this is <see cref="Guid"/> or <see cref="Int64"/>.</typeparam>
   public interface HTTPStatementInformation<out TRequestMetaData>
   {
      // This interface is exposed via async enumerator observability, so we probably don't want to expose generator here.
      //Func<HTTPRequest> MessageGenerator { get; }

      /// <summary>
      /// Gets the metadata of the initial request.
      /// </summary>
      TRequestMetaData InitialRequestMetaData { get; }
   }

   /// <summary>
   /// This is read-write API for controlling how <see cref="HTTPConnection{TRequestMetaData}"/> will send HTTP requests.
   /// </summary>
   /// <typeparam name="TRequestMetaData">The type of metadata associated with each request, used in identifying the request that response is associated with. Typically this is <see cref="Guid"/> or <see cref="Int64"/>.</typeparam>
   public interface HTTPStatement<TRequestMetaData> : HTTPStatementInformation<TRequestMetaData>
   {
      /// <summary>
      /// Gets or sets the callback, used when enumerating, to generate next request after seeing response from previous request.
      /// </summary>
      /// <value>The callback, used when enumerating, to generate next request after seeing response from previous request.</value>
      Func<HTTPResponseInfo<TRequestMetaData>, ValueTask<HTTPRequestInfo<TRequestMetaData>>> NextRequestGenerator { get; set; }

      /// <summary>
      /// Gets the <see cref="HTTPRequestInfo{TRequestMetaData}"/> of the first request to send when starting enumeration of this statement.
      /// </summary>
      /// <value>The <see cref="HTTPRequestInfo{TRequestMetaData}"/> of the first request to send when starting enumeration of this statement.</value>
      HTTPRequestInfo<TRequestMetaData> InitialRequest { get; }

      ///// <summary>
      ///// Gets or sets single <see cref="HTTPRequest"/> that will be sent when enumerating result of <see cref="CBAM.Abstractions.Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.PrepareStatementForExecution"/>.
      ///// </summary>
      ///// <value>The single <see cref="HTTPRequest"/> that will be sent.</value>
      ///// <remarks>
      ///// Notice that if <see cref="MessageGenerator"/> is not <c>null</c>, it will take precedence over this property.
      ///// </remarks>
      //HTTPRequest StaticMessage { get; set; }

      ///// <summary>
      ///// Gets or sets the callback to return <see cref="HTTPRequest"/> that will be sent when enumerating result of <see cref="Abstractions.Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.PrepareStatementForExecution"/>.
      ///// By returning <c>null</c>, the enumeration will stop.
      ///// </summary>
      ///// <value>The callback to return <see cref="HTTPRequest"/> that will be sent until the callback returns <c>null</c>.</value>
      ///// <remarks>
      ///// Notice that this property takes precendence over <see cref="StaticMessage"/>.
      ///// </remarks>
      //Func<HTTPRequest> MessageGenerator { get; set; }

      /// <summary>
      /// Gets the read-only API of this <see cref="HTTPStatement{TRequestMetaData}"/>.
      /// </summary>
      /// <value>The read-only API of this <see cref="HTTPStatement{TRequestMetaData}"/>.</value>
      HTTPStatementInformation<TRequestMetaData> Information { get; }
   }
}
