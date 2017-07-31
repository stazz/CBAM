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
using UtilPack.AsyncEnumeration;

namespace CBAM.Abstractions
{
   /// <summary>
   /// This is common interface for any kind of connection to the potentially remote resource (e.g. SQL or LDAP server).
   /// </summary>
   /// <typeparam name="TStatement">The type of objects used to manipulate or query remote resource.</typeparam>
   /// <typeparam name="TStatementInformation">The type of objects describing <typeparamref name="TStatement"/>s.</typeparam>
   /// <typeparam name="TStatementCreationArgs">The type of object used to create an instance of <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TEnumerableItem">The type of object representing the response of manipulation or querying remote resource.</typeparam>
   /// <typeparam name="TVendorFunctionality">The type of object describing vendor-specific information.</typeparam>
   public interface Connection<in TStatement, out TStatementInformation, in TStatementCreationArgs, out TEnumerableItem, out TVendorFunctionality> : AsyncEnumerationObservation<TEnumerableItem, TStatementInformation>
      where TVendorFunctionality : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
      where TStatement : TStatementInformation
   {
      /// <summary>
      /// Prepares object that will manipulate or query remote resource to be ready for execution.
      /// </summary>
      /// <param name="statement">The statement, which describes querying or manipulating the remote resource.</param>
      /// <returns>Prepared <see cref="AsyncEnumerator{T}"/> to be used to execute the statement.</returns>
      /// <remarks>This method does not execute the <paramref name="statement"/>. Use the methods in <see cref="AsyncEnumerator{T}"/> interface (e.g. <see cref="AsyncEnumerator{T}.MoveNextAsync(CancellationToken)"/>) to actually execute the statement.</remarks>
      AsyncEnumeratorObservable<TEnumerableItem, TStatementInformation> PrepareStatementForExecution( TStatement statement );

      /// <summary>
      /// Gets the <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/> of this connection.
      /// </summary>
      /// <value>The <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/> of this connection.</value>
      TVendorFunctionality VendorFunctionality { get; }

      /// <summary>
      /// Gets the current cancellation token for asynchronous operations.
      /// </summary>
      /// <value>The current cancellation token for asynchronous operations.</value>
      /// <remarks>
      /// This will be the <see cref="CancellationToken"/> passed to <see cref="ConnectionPool{TConnection}.UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/> method.
      /// </remarks>
      /// <seealso cref="ConnectionPool{TConnection}.UseConnectionAsync(Func{TConnection, Task}, CancellationToken)"/>
      CancellationToken CurrentCancellationToken { get; }
   }

   /// <summary>
   /// This interface represents vendor-specific functionality that is required by <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>.
   /// </summary>
   /// <typeparam name="TStatement">The type of statement object used to manipulate or query the remote resource.</typeparam>
   /// <typeparam name="TStatementCreationArgs">The type of parameters used to create statement object.</typeparam>
   public interface ConnectionVendorFunctionality<out TStatement, in TStatementCreationArgs>
   {
      /// <summary>
      /// Creates a modifiable statement object, which can be customized and parametrized as needed in order to manipulate or query remote resource.
      /// </summary>
      /// <param name="creationArgs">The object that contains information that will not be customizable in resulting statement builder.</param>
      /// <returns>Customizable statement object.</returns>
      TStatement CreateStatementBuilder( TStatementCreationArgs creationArgs );
   }
}
