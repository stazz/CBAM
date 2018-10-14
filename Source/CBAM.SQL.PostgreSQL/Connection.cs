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
using CBAM.SQL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.SQL.PostgreSQL
{
   /// <summary>
   /// This interface extends <see cref="SQLConnection"/> to provide PostgreSQL-specific API.
   /// </summary>
   /// <remarks>
   /// The <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.VendorFunctionality"/> property of this connection will always return object which is castable to <see cref="PgSQLConnectionVendorFunctionality"/>.
   /// Also, the <see cref="UtilPack.TabularData.AsyncDataColumnMetaData"/> objects of <see cref="SQLDataRow"/>s returned by this connection are of type <see cref="PgSQLDataColumnMetaData"/>.
   /// The PostgreSQL-specific <see cref="SQLException"/> type is <see cref="PgSQLException"/>.
   /// </remarks>
   public partial interface PgSQLConnection : SQLConnection
   {
      ///// <summary>
      ///// This event will be fired whenever a notification is processed, which happens *only* when <see cref="CheckNotificationsAsync"/> is called.
      ///// </summary>
      ///// <seealso cref="NotificationEventArgs"/>
      //event GenericEventHandler<NotificationEventArgs> NotificationEvent;

      /// <summary>
      /// Checks whether any notifications are pending.
      /// Please note that this will NOT cause SQL query (<c>SELECT 1</c>) to be sent to backend, unless this connection was explicitly created from stream (which should happen extremely rarely).
      /// </summary>
      /// <returns>Task which will have completed after processing all pending notifies. The returned integer will be amount of event arguments processed.</returns>
      /// <remarks>
      /// During normal SQL statement processing, all encountered notifications will be queued to list.
      /// This method will empty that list, and also check for any pending notifications from backend.
      /// </remarks>
      ValueTask<NotificationEventArgs[]> CheckNotificationsAsync();

      /// <summary>
      /// This method will return an <see cref="IAsyncEnumerable{T}"/> that can be used to continuously and asynchronously to extract notifications from this connection.
      /// </summary>
      /// <returns>An <see cref="IAsyncEnumerable{T}"/> that can be used to continuously and asynchronously to extract notifications from this connection.</returns>
      IAsyncEnumerable<NotificationEventArgs> ContinuouslyListenToNotificationsAsync(); // TODO argument Func<Boolean> shouldContinue - would be invoked within WaitForNext() and TryGetNext() methods. This will cause wait-based polling for socket if non-null. If null, just use current implementation.

      /// <summary>
      /// Gets the <see cref="PostgreSQL.TypeRegistry"/> object which manages the conversions between CLR types and PostgreSQL types.
      /// </summary>
      /// <value>The <see cref="PostgreSQL.TypeRegistry"/> object which manages the conversions between CLR types and PostgreSQL types.</value>
      /// <seealso cref="PostgreSQL.TypeRegistry"/>
      TypeRegistry TypeRegistry { get; }

      /// <summary>
      /// Gets the process ID of the backend process this connection is connected to.
      /// </summary>
      /// <value>The process ID of the backend process this connection is connected to.</value>
      Int32 BackendProcessID { get; }

      /// <summary>
      /// Gets the last seen transaction status.
      /// </summary>
      /// <value>The last seen transaction status.</value>
      /// <see cref="TransactionStatus"/>
      TransactionStatus LastSeenTransactionStatus { get; }
   }

   /// <summary>
   /// This interface extends <see cref="SQLConnectionVendorFunctionality"/> to provide PostgreSQL-specific vendor functionality.
   /// Instances of this class are obtaineable from <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.VendorFunctionality"/> property of <see cref="PgSQLConnection"/>.
   /// </summary>
   public interface PgSQLConnectionVendorFunctionality : SQLConnectionVendorFunctionality
   {
      /// <summary>
      /// Tries to advance the given <see cref="PeekablePotentiallyAsyncReader{TValue}"/> to the end of <c>COPY IN</c> statement.
      /// </summary>
      /// <param name="reader">The <see cref="PeekablePotentiallyAsyncReader{TValue}"/>.</param>
      /// <returns>A task which always returns <c>true</c>.</returns>
      ValueTask<Boolean> TryAdvanceReaderOverCopyInStatement( PeekablePotentiallyAsyncReader<Char?> reader );
   }

   /// <summary>
   /// This class encapsulates all information about the data of single PostgreSQL notification (data received as a result of <c>NOTIFY</c> statement).
   /// </summary>
   public class NotificationEventArgs
   {

      /// <summary>
      /// Creates a new instance of <see cref="NotificationEventArgs"/> with given parameters.
      /// </summary>
      /// <param name="pid">The process ID of the backend which issued notify.</param>
      /// <param name="name">The name of the notification.</param>
      /// <param name="payload">The payload of the notification.</param>
      public NotificationEventArgs( Int32 pid, String name, String payload )
      {
         this.ProcessID = pid;
         this.Name = name;
         this.Payload = payload;
      }

      /// <summary>
      /// Gets the process ID of the backend which issued notify.
      /// </summary>
      /// <value>The process ID of the backend which issued notify.</value>
      public Int32 ProcessID { get; }

      /// <summary>
      /// Gets the name of the notification.
      /// </summary>
      /// <value>The name of the notification.</value>
      public String Name { get; }

      /// <summary>
      /// Gets the textual payload that was supplied with <c>NOTIFY</c> command.
      /// </summary>
      /// <value>The textual payload that was supplied with <c>NOTIFY</c> command.</value>
      public String Payload { get; }
   }

   /// <summary>
   /// This enumeration describes the transaction status of the connection.
   /// </summary>
   public enum TransactionStatus
   {
      /// <summary>
      /// No transaction is currently going on.
      /// </summary>
      Idle = 'I',

      /// <summary>
      /// The transaction is going on.
      /// </summary>
      InTransaction = 'T',

      /// <summary>
      /// Error was resulted.
      /// </summary>
      ErrorInTransaction = 'E'
   }
}