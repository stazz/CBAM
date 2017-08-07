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

namespace UtilPack.ResourcePooling
{
   /// <summary>
   /// This interface provides API to create instances of <see cref="AsyncResourcePoolObservable{TConnection}"/> and <see cref="AsyncResourcePoolObservable{TConnection, TCleanUpParameter}"/> connection pools.
   /// The creation parameters are not type-constrained in order for this interface to be used in generic scenarios.
   /// E.g. in SQL connections, the type of connection is constrained for all SQL connections, but creation parameter type is usually very vendor-specific.
   /// </summary>
   /// <typeparam name="TConnection"></typeparam>
   /// <remarks>
   /// Typically, most client code won't need to use this interface - it is provided for the sake of generic scenarios, where connection pool needs to be instantiated dynamically based on some kind of configuration.
   /// Most common scenario to create connection pools is to directly use vendor-specific class.
   /// </remarks>
   public interface ConnectionPoolProvider<out TConnection>
   {
      /// <summary>
      /// Creates a new instance of <see cref="AsyncResourcePoolObservable{TConnection}"/>, which will close all connections as they are returned to pool.
      /// This is typically useful in test scenarios.
      /// </summary>
      /// <param name="creationParameters">The creation parameters for the connection pool.</param>
      /// <returns>A new instance of <see cref="AsyncResourcePoolObservable{TConnection}"/>.</returns>
      /// <exception cref="ArgumentException">If <paramref name="creationParameters"/> is somehow invalid, e.g. of wrong type.</exception>
      AsyncResourcePoolObservable<TConnection> CreateOneTimeUseConnectionPool( Object creationParameters );

      /// <summary>
      /// Creates a new instance of <see cref="AsyncResourcePoolObservable{TConnection, TCleanUpParameter}"/>, which will provide a method to clean up connections which have been idle for longer than given time.
      /// </summary>
      /// <param name="creationParameters">The creation parameters for the connection pool.</param>
      /// <returns>A new instance of <see cref="AsyncResourcePoolObservable{TConnection, TCleanUpParameter}"/>.</returns>
      /// <exception cref="ArgumentException">If <paramref name="creationParameters"/> is somehow invalid, e.g. of wrong type.</exception>
      /// <remarks>
      /// Note that the returned pool will not clean up connections automatically.
      /// The <see cref="AsyncResourcePoolCleanUp{TCleanUpParameter}.CleanUpAsync(TCleanUpParameter, System.Threading.CancellationToken)"/> method must be invoked explicitly by the user of connection pool.
      /// </remarks>
      AsyncResourcePoolObservable<TConnection, TimeSpan> CreateTimeoutingConnectionPool( Object creationParameters );

      /// <summary>
      /// Gets the default type of parameter for <see cref="CreateOneTimeUseConnectionPool(object)"/> and <see cref="CreateTimeoutingConnectionPool(object)"/> methods.
      /// </summary>
      /// <value>The default type of parameter for <see cref="CreateOneTimeUseConnectionPool(object)"/> and <see cref="CreateTimeoutingConnectionPool(object)"/> methods.</value>
      Type DefaultTypeForCreationParameter { get; }
   }
}
