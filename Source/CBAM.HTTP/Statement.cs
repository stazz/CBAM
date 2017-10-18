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

namespace CBAM.HTTP
{
   /// <summary>
   /// This is read-only interface for <see cref="HTTPStatement"/>. Right now, it has no public API.
   /// </summary>
   public interface HTTPStatementInformation
   {
      // This interface is exposed via async enumerator observability, so we probably don't want to expose generator here.
      //Func<HTTPRequest> MessageGenerator { get; }
   }

   /// <summary>
   /// This is read-write API for controlling how <see cref="HTTPConnection"/> will send HTTP requests.
   /// </summary>
   public interface HTTPStatement : HTTPStatementInformation
   {
      /// <summary>
      /// Gets or sets single <see cref="HTTPRequest"/> that will be sent when enumerating result of <see cref="CBAM.Abstractions.Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TEnumerable}.PrepareStatementForExecution"/>.
      /// </summary>
      /// <value>The single <see cref="HTTPRequest"/> that will be sent.</value>
      /// <remarks>
      /// Notice that if <see cref="MessageGenerator"/> is not <c>null</c>, it will take precedence over this property.
      /// </remarks>
      HTTPRequest StaticMessage { get; set; }

      /// <summary>
      /// Gets or sets the callback to return <see cref="HTTPRequest"/> that will be sent when enumerating result of <see cref="Abstractions.Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TEnumerable}.PrepareStatementForExecution"/>.
      /// By returning <c>null</c>, the enumeration will stop.
      /// </summary>
      /// <value>The callback to return <see cref="HTTPRequest"/> that will be sent until the callback returns <c>null</c>.</value>
      /// <remarks>
      /// Notice that this property takes precendence over <see cref="StaticMessage"/>.
      /// </remarks>
      Func<HTTPRequest> MessageGenerator { get; set; }

      /// <summary>
      /// Gets the read-only API of this <see cref="HTTPStatement"/>.
      /// </summary>
      /// <value>The read-only API of this <see cref="HTTPStatement"/>.</value>
      HTTPStatementInformation Information { get; }
   }
}
