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
using CBAM.Abstractions;

namespace CBAM.Abstractions.Implementation
{
   public abstract class ConnectionImpl<TStatement, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TConnectionFunctionality> : Connection<TStatement, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality>
      where TVendorFunctionality : class, ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
      where TConnectionFunctionality : DefaultConnectionFunctionality<TStatement, TEnumerableItem>
   {

      public ConnectionImpl(
         TVendorFunctionality vendorFunctionality,
         TConnectionFunctionality functionality
         )
      {
         this.VendorFunctionality = ArgumentValidator.ValidateNotNull( nameof( vendorFunctionality ), vendorFunctionality );
         this.ConnectionFunctionality = ArgumentValidator.ValidateNotNull( nameof( functionality ), functionality );
      }

      public TVendorFunctionality VendorFunctionality { get; }

      public event GenericEventHandler<StatementExecutionStartedEventArgs<TStatement>> BeforeStatementExecutionStart
      {
         add
         {
            this.ConnectionFunctionality.BeforeStatementExecutionStart += value;
         }
         remove
         {
            this.ConnectionFunctionality.BeforeStatementExecutionStart -= value;
         }
      }

      public event GenericEventHandler<StatementExecutionEndedEventArgs<TStatement>> BeforeStatementExecutionEnd
      {
         add
         {
            this.ConnectionFunctionality.BeforeStatementExecutionEnd += value;
         }
         remove
         {
            this.ConnectionFunctionality.BeforeStatementExecutionEnd -= value;
         }
      }

      public event GenericEventHandler<StatementExecutionEndedEventArgs<TStatement>> AfterStatementExecutionEnd
      {
         add
         {
            this.ConnectionFunctionality.AfterStatementExecutionEnd += value;
         }
         remove
         {
            this.ConnectionFunctionality.AfterStatementExecutionEnd -= value;
         }
      }

      public event GenericEventHandler<StatementExecutionResultEventArgs<TEnumerableItem>> AfterStatementExecutionItemEncountered
      {
         add
         {
            this.ConnectionFunctionality.AfterStatementExecutionItemEncountered += value;
         }
         remove
         {
            this.ConnectionFunctionality.AfterStatementExecutionItemEncountered -= value;
         }
      }

      public AsyncEnumeratorObservable<TEnumerableItem, TStatement> PrepareStatementForExecution( TStatement statementBuilder )
      {
         return this.ConnectionFunctionality.CreateIterationArguments( statementBuilder );
      }

      protected TConnectionFunctionality ConnectionFunctionality { get; }

      public CancellationToken CurrentCancellationToken => this.ConnectionFunctionality.CurrentCancellationToken;
   }

   public abstract class DefaultConnectionVendorFunctionality<TConnection, TStatement, TStatementCreationArgs, TEnumerableItem, TConnectionCreationParameters, TConnectionFunctionality> : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>, ConnectionFactory<TConnection, TConnectionCreationParameters>
      where TConnection : class
      where TConnectionFunctionality : DefaultConnectionFunctionality<TStatement, TEnumerableItem>
   {

      public abstract TStatement CreateStatementBuilder( TStatementCreationArgs sql );

      public async Task<ConnectionAcquireInfo<TConnection>> AcquireConnection( TConnectionCreationParameters parameters, CancellationToken token )
      {
         TConnectionFunctionality functionality = null;
         TConnection connection = null;
         try
         {
            functionality = await this.CreateConnectionFunctionality( parameters, token );
            functionality.CurrentCancellationToken = token;
            connection = await this.CreateConnection( functionality );
            return this.CreateConnectionAcquireInfo( functionality, connection );
         }
         catch ( Exception exc )
         {
            try
            {
               await this.OnConnectionAcquirementError( functionality, connection, token, exc );
            }
            catch
            {
               // Ignore this one
            }
            throw;
         }
         finally
         {
            functionality?.ResetCancellationToken();
         }
      }

      protected abstract Task<TConnectionFunctionality> CreateConnectionFunctionality( TConnectionCreationParameters parameters, CancellationToken token );
      protected abstract Task<TConnection> CreateConnection( TConnectionFunctionality functionality );
      protected abstract ConnectionAcquireInfo<TConnection> CreateConnectionAcquireInfo( TConnectionFunctionality functionality, TConnection connection );
      protected abstract Task OnConnectionAcquirementError( TConnectionFunctionality functionality, TConnection connection, CancellationToken token, Exception error );
   }

   public interface ConnectionFunctionality<TStatement, out TEnumerableItem> : ConnectionObservable<TStatement, TEnumerableItem>
   {
      AsyncEnumeratorObservable<TEnumerableItem, TStatement> CreateIterationArguments( TStatement stmt );
   }

   public abstract class DefaultConnectionFunctionality<TStatement, TEnumerableItem> : ConnectionFunctionality<TStatement, TEnumerableItem>
   {

      private Object _cancellationToken;
      public CancellationToken CurrentCancellationToken
      {
         get
         {
            return (CancellationToken) this._cancellationToken;
         }
         internal protected set
         {
            Interlocked.Exchange( ref this._cancellationToken, value );
         }
      }

      public event GenericEventHandler<StatementExecutionStartedEventArgs<TStatement>> BeforeStatementExecutionStart;
      public event GenericEventHandler<StatementExecutionEndedEventArgs<TStatement>> BeforeStatementExecutionEnd;
      public event GenericEventHandler<StatementExecutionEndedEventArgs<TStatement>> AfterStatementExecutionEnd;
      public event GenericEventHandler<StatementExecutionResultEventArgs<TEnumerableItem>> AfterStatementExecutionItemEncountered;

      internal protected void ResetCancellationToken()
      {
         Interlocked.Exchange( ref this._cancellationToken, null );
      }

      public AsyncEnumeratorObservable<TEnumerableItem, TStatement> CreateIterationArguments( TStatement statement )
      {
         this.ValidateStatementOrThrow( statement );
         return this.PerformCreateIterationArguments(
            statement,
            () => this.BeforeStatementExecutionStart,
            () => this.BeforeStatementExecutionEnd,
            () => this.AfterStatementExecutionEnd,
            () => this.AfterStatementExecutionItemEncountered
            );
      }

      public abstract Boolean CanBeReturnedToPool { get; }

      protected abstract AsyncEnumeratorObservable<TEnumerableItem, TStatement> PerformCreateIterationArguments(
         TStatement statement,
         Func<GenericEventHandler<StatementExecutionStartedEventArgs<TStatement>>> getGlobalBeforeStatementExecutionStart,
         Func<GenericEventHandler<StatementExecutionEndedEventArgs<TStatement>>> getGlobalBeforeStatementExecutionEnd,
         Func<GenericEventHandler<StatementExecutionEndedEventArgs<TStatement>>> getGlobalAfterStatementExecutionEnd,
         Func<GenericEventHandler<StatementExecutionResultEventArgs<TEnumerableItem>>> getGlobalAfterStatementExecutionItemEncountered
         );

      protected abstract void ValidateStatementOrThrow( TStatement statement );
   }

   public class StatementExecutionStartedEventArgsImpl<TStatement> : StatementExecutionStartedEventArgs<TStatement>
   {
      public StatementExecutionStartedEventArgsImpl( TStatement statement )
      {
         this.Statement = statement;
      }

      public TStatement Statement { get; }
   }

   public class StatementExecutionResultEventArgsImpl<TEnumerableItem> : StatementExecutionResultEventArgs<TEnumerableItem>
   {
      public StatementExecutionResultEventArgsImpl( TEnumerableItem item )
      {
         this.Item = item;
      }

      public TEnumerableItem Item { get; }
   }

   public class StatementExecutionEndedEventArgsImpl<TStatement> : StatementExecutionStartedEventArgsImpl<TStatement>, StatementExecutionEndedEventArgs<TStatement>
   {
      public StatementExecutionEndedEventArgsImpl(
         TStatement statement
         ) : base( statement )
      {
      }
   }

}