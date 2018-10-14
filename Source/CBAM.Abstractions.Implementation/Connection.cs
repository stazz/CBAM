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
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using UtilPack.AsyncEnumeration;

namespace CBAM.Abstractions.Implementation
{
   /// <summary>
   /// This is base class for <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> but with less generic parameters.
   /// </summary>
   /// <typeparam name="TConnectionFunctionality">The type of actual connection functionality.</typeparam>
   public abstract class ConnectionImpl<TConnectionFunctionality>
      where TConnectionFunctionality : class
   {
      internal ConnectionImpl(
         TConnectionFunctionality functionality
         )
      {
         this.ConnectionFunctionality = ArgumentValidator.ValidateNotNull( nameof( functionality ), functionality );

      }

      /// <summary>
      /// Gets the <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> that this <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> is facade of.
      /// </summary>
      /// <value>The <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> that this <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> is facade of.</value>
      internal protected TConnectionFunctionality ConnectionFunctionality { get; }
   }

   /// <summary>
   /// This class provides facade implementation of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> interface.
   /// It does so by having a reference to <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TVendor, TEnumerableItem}"/> object, which it receives as argument to constructor.
   /// This way, any components of this connection may use connection-related services (e.g. creating <see cref="IAsyncEnumerable{T}"/>) without lifecycle problems (e.g. calling virtual method in constructor).
   /// </summary>
   /// <typeparam name="TStatement">The type of objects used to manipulate or query remote resource.</typeparam>
   /// <typeparam name="TStatementInformation">The type of objects describing <typeparamref name="TStatement"/>s.</typeparam>
   /// <typeparam name="TStatementCreationArgs">The type of object used to create an instance of <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TEnumerableItem">The type of object representing the response of manipulation or querying remote resource.</typeparam>
   /// <typeparam name="TVendorFunctionality">The type of object describing vendor-specific information, as specified by the interface generic parameter.</typeparam>
   /// <typeparam name="TActualVendorFunctionality">The actual type of object describing vendor-specific information.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The type of object actually implementing functionality for this facade.</typeparam>
   public abstract class ConnectionImpl<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality> : ConnectionImpl<TConnectionFunctionality>, Connection<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality>
      where TStatement : TStatementInformation
      where TVendorFunctionality : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
      where TConnectionFunctionality : DefaultConnectionFunctionality<TStatement, TStatementInformation, TStatementCreationArgs, TActualVendorFunctionality, TEnumerableItem>
      where TActualVendorFunctionality : TVendorFunctionality
   {
      /// <summary>
      /// Creats a new instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> with given parameters.
      /// </summary>
      /// <param name="functionality">The <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TVendor, TEnumerableItem}"/>.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="functionality"/> is <c>null</c>.</exception>
      public ConnectionImpl(
         TConnectionFunctionality functionality
         ) : base( functionality )
      {
      }

      /// <summary>
      /// Forwards the property to <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.VendorFunctionality"/> of this <see cref="ConnectionImpl{TConnectionFunctionality}.ConnectionFunctionality"/>.
      /// </summary>
      /// <value>The value of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.VendorFunctionality"/> of this <see cref="ConnectionImpl{TConnectionFunctionality}.ConnectionFunctionality"/>.</value>
      public TActualVendorFunctionality VendorFunctionality => this.ConnectionFunctionality.VendorFunctionality;

      /// <summary>
      /// Forwards the property to <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.VendorFunctionality"/> of this <see cref="ConnectionImpl{TConnectionFunctionality}.ConnectionFunctionality"/>.
      /// </summary>
      /// <value>The value of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.VendorFunctionality"/> of this <see cref="ConnectionImpl{TConnectionFunctionality}.ConnectionFunctionality"/>.</value>
      TVendorFunctionality Connection<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality>.VendorFunctionality => this.VendorFunctionality;

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T, TMetadata}.BeforeEnumerationStart"/> event.
      /// </summary>
      public event GenericEventHandler<EnumerationStartedEventArgs<TStatementInformation>> BeforeEnumerationStart;
      //{
      //   add
      //   {
      //      this.ConnectionFunctionality.BeforeEnumerationStart += value;
      //   }
      //   remove
      //   {
      //      this.ConnectionFunctionality.BeforeEnumerationStart -= value;
      //   }
      //}

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T, TMetadata}.AfterEnumerationStart"/> event.
      /// </summary>
      public event GenericEventHandler<EnumerationStartedEventArgs<TStatementInformation>> AfterEnumerationStart;
      //{
      //   add
      //   {
      //      this.ConnectionFunctionality.AfterEnumerationStart += value;
      //   }
      //   remove
      //   {
      //      this.ConnectionFunctionality.AfterEnumerationStart -= value;
      //   }
      //}

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T, TMetadata}.BeforeEnumerationEnd"/> event.
      /// </summary>
      public event GenericEventHandler<EnumerationEndedEventArgs<TStatementInformation>> BeforeEnumerationEnd;
      //{
      //   add
      //   {
      //      this.ConnectionFunctionality.BeforeEnumerationEnd += value;
      //   }
      //   remove
      //   {
      //      this.ConnectionFunctionality.BeforeEnumerationEnd -= value;
      //   }
      //}

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T, TMetadata}.AfterEnumerationEnd"/> event.
      /// </summary>
      public event GenericEventHandler<EnumerationEndedEventArgs<TStatementInformation>> AfterEnumerationEnd;
      //{
      //   add
      //   {
      //      this.ConnectionFunctionality.AfterEnumerationEnd += value;
      //   }
      //   remove
      //   {
      //      this.ConnectionFunctionality.AfterEnumerationEnd -= value;
      //   }
      //}

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T, TMetadata}.AfterEnumerationItemEncountered"/>.
      /// </summary>
      public event GenericEventHandler<EnumerationItemEventArgs<TEnumerableItem, TStatementInformation>> AfterEnumerationItemEncountered;
      //{
      //   add
      //   {
      //      this.ConnectionFunctionality.AfterEnumerationItemEncountered += value;
      //   }
      //   remove
      //   {
      //      this.ConnectionFunctionality.AfterEnumerationItemEncountered -= value;
      //   }
      //}

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T}.BeforeEnumerationStart"/> event by forwarding it to this <see cref="BeforeEnumerationStart"/> event.
      /// </summary>
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

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T}.AfterEnumerationStart"/> event by forwarding it to this <see cref="AfterEnumerationStart"/> event.
      /// </summary>
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

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T}.AfterEnumerationItemEncountered"/> event by forwarding it to this <see cref="AfterEnumerationItemEncountered"/> event.
      /// </summary>
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

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T}.BeforeEnumerationEnd"/> event by forwarding it to this <see cref="BeforeEnumerationEnd"/> event.
      /// </summary>
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

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T}.AfterEnumerationEnd"/> event by forwarding it to this <see cref="AfterEnumerationEnd"/> event.
      /// </summary>
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

      /// <summary>
      /// Implements the <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.DisableEnumerableObservability"/>.
      /// </summary>
      /// <value>Whether to disable observability aspect of enumerables created by <see cref="PrepareStatementForExecution"/> method.</value>
      public Boolean DisableEnumerableObservability { get; set; }

      /// <summary>
      /// Forwards the method call to <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.PrepareStatementForExecution(TStatementInformation)"/> method of this <see cref="ConnectionImpl{TConnectionFunctionality}.ConnectionFunctionality"/>.
      /// </summary>
      /// <param name="statementBuilder">The statement builder, created by <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}.CreateStatementBuilder(TStatementCreationArgs)"/> of this <see cref="VendorFunctionality"/>.</param>
      /// <returns>A new instance of <see cref="IAsyncEnumerable{T}"/>.</returns>
      public IAsyncEnumerable<TEnumerableItem> PrepareStatementForExecution( TStatementInformation statementBuilder )
      {
         var retVal = this.ConnectionFunctionality.PrepareStatementForExecution( statementBuilder, out var info );
         if ( !this.DisableEnumerableObservability )
         {
            var observable = retVal.AsObservable( info );
            observable.BeforeEnumerationStart += args => this.BeforeEnumerationStart?.InvokeAllEventHandlers( evt => evt( args ), throwExceptions: false );
            observable.AfterEnumerationStart += args => this.AfterEnumerationStart?.InvokeAllEventHandlers( evt => evt( args ), throwExceptions: false );
            observable.AfterEnumerationItemEncountered += args => this.AfterEnumerationItemEncountered?.InvokeAllEventHandlers( evt => evt( args ), throwExceptions: false );
            observable.BeforeEnumerationEnd += args => this.BeforeEnumerationEnd?.InvokeAllEventHandlers( evt => evt( args ), throwExceptions: false );
            observable.AfterEnumerationEnd += args => this.AfterEnumerationEnd?.InvokeAllEventHandlers( evt => evt( args ), throwExceptions: false );
            retVal = observable;
         }

         return retVal;
      }
   }

   /// <summary>
   /// This interface exists so that life would be a bit easier when using properties of <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> that do not require any generic parameters.
   /// </summary>
   public interface PooledConnectionFunctionality
   {
      /// <summary>
      /// Gets or sets the current cancellation token.
      /// </summary>
      /// <value>The current cancellation token.</value>
      CancellationToken CurrentCancellationToken { get; set; }


      /// <summary>
      /// This method resets current cancellation token, so that <see cref="CurrentCancellationToken"/> property getter will throw.
      /// </summary>
      void ResetCancellationToken();

      /// <summary>
      /// Gets the value indicating whether this connection can be returned to the pool (e.g. underlying stream is open).
      /// </summary>
      /// <value>The value indicating whether this connection can be returned to the pool (e.g. underlying stream is open).</value>
      Boolean CanBeReturnedToPool { get; }
   }

   /// <summary>
   /// This class provides the skeleton implementation for <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> interface.
   /// </summary>
   /// <typeparam name="TStatement">The type of objects used to manipulate or query remote resource.</typeparam>
   /// <typeparam name="TStatementInformation">The type of objects describing <typeparamref name="TStatement"/>s.</typeparam>
   /// <typeparam name="TStatementCreationArgs">The type of object used to create an instance of <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TVendor">The type of object describing vendor-specific information.</typeparam>
   /// <typeparam name="TEnumerableItem">The type parameter of <see cref="IAsyncEnumerable{T}"/> returned by <see cref="PrepareStatementForExecution"/> method.</typeparam>
   public abstract class DefaultConnectionFunctionality<TStatement, TStatementInformation, TStatementCreationArgs, TVendor, TEnumerableItem>
      where TStatement : TStatementInformation
      where TVendor : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
   {
      /// <summary>
      /// Creates a new instance of <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> with given <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/>.
      /// </summary>
      /// <param name="vendorFunctionality">The connection vendor.</param>
      public DefaultConnectionFunctionality( TVendor vendorFunctionality )
      {
         this.VendorFunctionality = vendorFunctionality;
      }

      /// <summary>
      /// Gets the <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/> of this <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.
      /// </summary>
      /// <value>The <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/> of this <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</value>
      public TVendor VendorFunctionality { get; }

      /// <summary>
      /// Validates the statement by calling <see cref="ValidateStatementOrThrow(TStatementInformation)"/> and then creates a new <see cref="AsyncEnumeratorObservable{T, TMetadata}"/> by calling <see cref="CreateEnumerable"/>.
      /// </summary>
      /// <param name="statement">The statement which describes how to manipulate/query remote resource.</param>
      /// <param name="info">This parameter will contain the read-only statement information, which is either <paramref name="statement"/> itself or extracted by <see cref="GetInformationFromStatement"/> method.</param>
      /// <returns>The enumerable which can be used to execute the <paramref name="statement"/> statement and iterate the possible results.</returns>
      public IAsyncEnumerable<TEnumerableItem> PrepareStatementForExecution( TStatementInformation statement, out TStatementInformation info )
      {
         info = statement is TStatement stmt ? this.GetInformationFromStatement( stmt ) : statement;
         this.ValidateStatementOrThrow( info );
         return this.CreateEnumerable(
            info
            );
      }



      /// <summary>
      /// Derived classes should override this abstract method in order to extract read-only statement information object from modifiable statement.
      /// </summary>
      /// <param name="statement">The modifiable statement object.</param>
      /// <returns>Read-only information about the <paramref name="statement"/>.</returns>
      protected abstract TStatementInformation GetInformationFromStatement( TStatement statement );

      /// <summary>
      /// This method should create actual <see cref="AsyncEnumeratorObservable{T, TMetadata}"/> from given parameters.
      /// </summary>
      /// <param name="metadata">The statement information.</param>
      /// <returns>The enumerable used to asynchronously enumerate over the results of statement execution.</returns>
      protected abstract IAsyncEnumerable<TEnumerableItem> CreateEnumerable(
         TStatementInformation metadata
         );


      /// <summary>
      /// This method should validate the given read-only information about a statement.
      /// </summary>
      /// <param name="statement">The read-only information about the statement.</param>
      /// <remarks>
      /// The <paramref name="statement"/> may be <c>null</c> at this point.
      /// </remarks>
      protected abstract void ValidateStatementOrThrow( TStatementInformation statement );
   }

   /// <summary>
   /// This class extends <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TVendor, TEnumerableItem}"/> and implements <see cref="PooledConnectionFunctionality"/>.
   /// </summary>
   /// <typeparam name="TStatement">The type of objects used to manipulate or query remote resource.</typeparam>
   /// <typeparam name="TStatementInformation">The type of objects describing <typeparamref name="TStatement"/>s.</typeparam>
   /// <typeparam name="TStatementCreationArgs">The type of object used to create an instance of <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TVendor">The type of object describing vendor-specific information.</typeparam>
   /// <typeparam name="TEnumerableItem">The type parameter of <see cref="IAsyncEnumerable{T}"/> returned by <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TVendor, TEnumerableItem}.PrepareStatementForExecution"/> method.</typeparam>
   public abstract class PooledConnectionFunctionality<TStatement, TStatementInformation, TStatementCreationArgs, TVendor, TEnumerableItem> : DefaultConnectionFunctionality<TStatement, TStatementInformation, TStatementCreationArgs, TVendor, TEnumerableItem>, PooledConnectionFunctionality //, Connection<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor, TEnumerable>
      where TStatement : TStatementInformation
      where TVendor : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
   {

      private Object _cancellationToken;

      /// <summary>
      /// Creates a new instance of <see cref="PooledConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> with given <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/>.
      /// </summary>
      /// <param name="vendorFunctionality">The connection vendor.</param>
      public PooledConnectionFunctionality( TVendor vendorFunctionality )
         : base( vendorFunctionality )
      {
      }


      /// <summary>
      /// Gets or sets the current cancellation token.
      /// </summary>
      /// <value>The current cancellation token.</value>
      public CancellationToken CurrentCancellationToken
      {
         get
         {
            var retVal = this._cancellationToken;
            if ( retVal == null )
            {
               throw new InvalidOperationException( "There currently is no cancellation token set for connection." );
            }
            return (CancellationToken) retVal;
         }
         set
         {
            Interlocked.Exchange( ref this._cancellationToken, value );
         }
      }


      /// <summary>
      /// This method resets current cancellation token, so that <see cref="CurrentCancellationToken"/> property getter will throw.
      /// </summary>
      public void ResetCancellationToken()
      {
         Interlocked.Exchange( ref this._cancellationToken, null );
      }

      /// <summary>
      /// Gets the value indicating whether this connection can be returned to the pool (e.g. underlying stream is open).
      /// </summary>
      /// <value>The value indicating whether this connection can be returned to the pool (e.g. underlying stream is open).</value>
      public abstract Boolean CanBeReturnedToPool { get; }
   }

}