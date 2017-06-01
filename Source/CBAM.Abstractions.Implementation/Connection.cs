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

      public event GenericEventHandler<EnumerationStartedEventArgs<TStatement>> BeforeEnumerationStart
      {
         add
         {
            this.ConnectionFunctionality.BeforeEnumerationStart += value;
         }
         remove
         {
            this.ConnectionFunctionality.BeforeEnumerationStart -= value;
         }
      }

      public event GenericEventHandler<EnumerationStartedEventArgs<TStatement>> AfterEnumerationStart
      {
         add
         {
            this.ConnectionFunctionality.AfterEnumerationStart += value;
         }
         remove
         {
            this.ConnectionFunctionality.AfterEnumerationStart -= value;
         }
      }

      public event GenericEventHandler<EnumerationEndedEventArgs<TStatement>> BeforeEnumerationEnd
      {
         add
         {
            this.ConnectionFunctionality.BeforeEnumerationEnd += value;
         }
         remove
         {
            this.ConnectionFunctionality.BeforeEnumerationEnd -= value;
         }
      }

      public event GenericEventHandler<EnumerationEndedEventArgs<TStatement>> AfterEnumerationEnd
      {
         add
         {
            this.ConnectionFunctionality.AfterEnumerationEnd += value;
         }
         remove
         {
            this.ConnectionFunctionality.AfterEnumerationEnd -= value;
         }
      }

      public event GenericEventHandler<EnumerationItemEventArgs<TEnumerableItem, TStatement>> AfterEnumerationItemEncountered
      {
         add
         {
            this.ConnectionFunctionality.AfterEnumerationItemEncountered += value;
         }
         remove
         {
            this.ConnectionFunctionality.AfterEnumerationItemEncountered -= value;
         }
      }

      event GenericEventHandler<EnumerationStartedEventArgs> AsyncEnumerationObservation<TEnumerableItem>.BeforeEnumerationStart
      {
         add
         {
            this.ConnectionFunctionality.BeforeEnumerationStart += value;
         }
         remove
         {
            this.ConnectionFunctionality.BeforeEnumerationStart -= value;
         }
      }

      event GenericEventHandler<EnumerationStartedEventArgs> AsyncEnumerationObservation<TEnumerableItem>.AfterEnumerationStart
      {
         add
         {
            this.ConnectionFunctionality.AfterEnumerationStart += value;
         }
         remove
         {
            this.ConnectionFunctionality.AfterEnumerationStart -= value;
         }
      }

      event GenericEventHandler<EnumerationItemEventArgs<TEnumerableItem>> AsyncEnumerationObservation<TEnumerableItem>.AfterEnumerationItemEncountered
      {
         add
         {
            this.ConnectionFunctionality.AfterEnumerationItemEncountered += value;
         }
         remove
         {
            this.ConnectionFunctionality.AfterEnumerationItemEncountered -= value;
         }
      }

      event GenericEventHandler<EnumerationEndedEventArgs> AsyncEnumerationObservation<TEnumerableItem>.BeforeEnumerationEnd
      {
         add
         {
            this.ConnectionFunctionality.BeforeEnumerationEnd += value;
         }
         remove
         {
            this.ConnectionFunctionality.BeforeEnumerationEnd -= value;
         }
      }

      event GenericEventHandler<EnumerationEndedEventArgs> AsyncEnumerationObservation<TEnumerableItem>.AfterEnumerationEnd
      {
         add
         {
            this.ConnectionFunctionality.AfterEnumerationEnd += value;
         }
         remove
         {
            this.ConnectionFunctionality.AfterEnumerationEnd -= value;
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

   public interface ConnectionFunctionality<TStatement, out TEnumerableItem> : AsyncEnumerationObservation<TEnumerableItem, TStatement>
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

      public event GenericEventHandler<EnumerationStartedEventArgs<TStatement>> BeforeEnumerationStart;
      public event GenericEventHandler<EnumerationStartedEventArgs<TStatement>> AfterEnumerationStart;

      public event GenericEventHandler<EnumerationEndedEventArgs<TStatement>> BeforeEnumerationEnd;
      public event GenericEventHandler<EnumerationEndedEventArgs<TStatement>> AfterEnumerationEnd;

      public event GenericEventHandler<EnumerationItemEventArgs<TEnumerableItem, TStatement>> AfterEnumerationItemEncountered;

      event GenericEventHandler<EnumerationStartedEventArgs> AsyncEnumerationObservation<TEnumerableItem>.BeforeEnumerationStart
      {
         add
         {
            this.BeforeEnumerationStart += value;
         }

         remove
         {
            this.BeforeEnumerationStart -= value;
         }
      }

      event GenericEventHandler<EnumerationStartedEventArgs> AsyncEnumerationObservation<TEnumerableItem>.AfterEnumerationStart
      {
         add
         {
            this.AfterEnumerationStart += value;
         }

         remove
         {
            this.AfterEnumerationStart -= value;
         }
      }

      event GenericEventHandler<EnumerationItemEventArgs<TEnumerableItem>> AsyncEnumerationObservation<TEnumerableItem>.AfterEnumerationItemEncountered
      {
         add
         {
            this.AfterEnumerationItemEncountered += value;
         }
         remove
         {
            this.AfterEnumerationItemEncountered -= value;
         }
      }

      event GenericEventHandler<EnumerationEndedEventArgs> AsyncEnumerationObservation<TEnumerableItem>.BeforeEnumerationEnd
      {
         add
         {
            this.BeforeEnumerationEnd += value;
         }

         remove
         {
            this.BeforeEnumerationEnd -= value;
         }
      }

      event GenericEventHandler<EnumerationEndedEventArgs> AsyncEnumerationObservation<TEnumerableItem>.AfterEnumerationEnd
      {
         add
         {
            this.AfterEnumerationEnd += value;
         }

         remove
         {
            this.AfterEnumerationEnd -= value;
         }
      }

      internal protected void ResetCancellationToken()
      {
         Interlocked.Exchange( ref this._cancellationToken, null );
      }

      public AsyncEnumeratorObservable<TEnumerableItem, TStatement> CreateIterationArguments( TStatement statement )
      {
         this.ValidateStatementOrThrow( statement );
         return this.PerformCreateIterationArguments(
            statement,
            () => this.BeforeEnumerationStart,
            () => this.AfterEnumerationStart,
            () => this.BeforeEnumerationEnd,
            () => this.AfterEnumerationEnd,
            () => this.AfterEnumerationItemEncountered
            );
      }

      public abstract Boolean CanBeReturnedToPool { get; }

      protected abstract AsyncEnumeratorObservable<TEnumerableItem, TStatement> PerformCreateIterationArguments(
         TStatement statement,
         Func<GenericEventHandler<EnumerationStartedEventArgs<TStatement>>> getGlobalBeforeStatementExecutionStart,
         Func<GenericEventHandler<EnumerationStartedEventArgs<TStatement>>> getGlobalAfterStatementExecutionStart,
         Func<GenericEventHandler<EnumerationEndedEventArgs<TStatement>>> getGlobalBeforeStatementExecutionEnd,
         Func<GenericEventHandler<EnumerationEndedEventArgs<TStatement>>> getGlobalAfterStatementExecutionEnd,
         Func<GenericEventHandler<EnumerationItemEventArgs<TEnumerableItem, TStatement>>> getGlobalAfterStatementExecutionItemEncountered
         );

      protected abstract void ValidateStatementOrThrow( TStatement statement );
   }

   public class StatementExecutionStartedEventArgsImpl<TStatement> : EnumerationStartedEventArgs<TStatement>
   {
      public StatementExecutionStartedEventArgsImpl( TStatement statement )
      {
         this.Metadata = statement;
      }

      public TStatement Metadata { get; }
   }

   public class StatementExecutionResultEventArgsImpl<TEnumerableItem> : EnumerationItemEventArgs<TEnumerableItem>
   {
      public StatementExecutionResultEventArgsImpl( TEnumerableItem item )
      {
         this.Item = item;
      }

      public TEnumerableItem Item { get; }
   }

   public class StatementExecutionEndedEventArgsImpl<TStatement> : StatementExecutionStartedEventArgsImpl<TStatement>, EnumerationEndedEventArgs<TStatement>
   {
      public StatementExecutionEndedEventArgsImpl(
         TStatement statement
         ) : base( statement )
      {
      }
   }

   public class StatementExecutionResultEventArgsImpl<TEnumerableItem, TMetadata> : StatementExecutionResultEventArgsImpl<TEnumerableItem>, EnumerationItemEventArgs<TEnumerableItem, TMetadata>
   {
      public StatementExecutionResultEventArgsImpl(
         TEnumerableItem item,
         TMetadata metadata
         )
         : base( item )
      {
         this.Metadata = metadata;
      }

      public TMetadata Metadata { get; }
   }

}