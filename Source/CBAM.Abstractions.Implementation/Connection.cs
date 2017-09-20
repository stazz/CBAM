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
using UtilPack.AsyncEnumeration;
using CBAM.Abstractions;

namespace CBAM.Abstractions.Implementation
{
   /// <summary>
   /// This class provides facade implementation of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> interface.
   /// It does so by having a reference to another <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> object, which it receives as argument to constructor.
   /// This way, any components of this connection may use connection-related services (e.g. creating <see cref="AsyncEnumerator{T}"/>) without lifecycle problems (e.g. calling virtual method in constructor).
   /// </summary>
   /// <typeparam name="TStatement">The type of objects used to manipulate or query remote resource.</typeparam>
   /// <typeparam name="TStatementInformation">The type of objects describing <typeparamref name="TStatement"/>s.</typeparam>
   /// <typeparam name="TStatementCreationArgs">The type of object used to create an instance of <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TEnumerableItem">The type of object representing the response of manipulation or querying remote resource.</typeparam>
   /// <typeparam name="TVendorFunctionality">The type of object describing vendor-specific information, as specified by the interface generic parameter.</typeparam>
   /// <typeparam name="TActualVendorFunctionality">The actual type of object describing vendor-specific information.</typeparam>
   /// <typeparam name="TConnectionFunctionality">The type of object actually implementing functionality for this facade.</typeparam>
   public abstract class ConnectionImpl<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality> : Connection<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality>
      where TStatement : TStatementInformation
      where TVendorFunctionality : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
      where TConnectionFunctionality : class, Connection<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TActualVendorFunctionality>
      where TActualVendorFunctionality : TVendorFunctionality
   {
      /// <summary>
      /// Creats a new instance of <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> with given parameters.
      /// </summary>
      /// <param name="functionality">The object containing the actual <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> implementation.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="functionality"/> is <c>null</c>.</exception>
      public ConnectionImpl(
         TConnectionFunctionality functionality
         )
      {
         this.ConnectionFunctionality = ArgumentValidator.ValidateNotNull( nameof( functionality ), functionality );
      }

      /// <summary>
      /// Forwards the property to <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.VendorFunctionality"/> of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
      /// <value>The value of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.VendorFunctionality"/> of this <see cref="ConnectionFunctionality"/>.</value>
      public TActualVendorFunctionality VendorFunctionality => this.ConnectionFunctionality.VendorFunctionality;

      /// <summary>
      /// Forwards the property to <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.VendorFunctionality"/> of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
      /// <value>The value of <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.VendorFunctionality"/> of this <see cref="ConnectionFunctionality"/>.</value>
      TVendorFunctionality Connection<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality>.VendorFunctionality => this.VendorFunctionality;

      /// <summary>
      /// Forwards the event (un)registration to the <see cref="AsyncEnumerationObservation{T, TMetadata}.BeforeEnumerationStart"/> event of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
      public event GenericEventHandler<EnumerationStartedEventArgs<TStatementInformation>> BeforeEnumerationStart
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

      /// <summary>
      /// Forwards the event (un)registration to the <see cref="AsyncEnumerationObservation{T, TMetadata}.AfterEnumerationStart"/> event of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
      public event GenericEventHandler<EnumerationStartedEventArgs<TStatementInformation>> AfterEnumerationStart
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

      /// <summary>
      /// Forwards the event (un)registration to the <see cref="AsyncEnumerationObservation{T, TMetadata}.BeforeEnumerationEnd"/> event of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
      public event GenericEventHandler<EnumerationEndedEventArgs<TStatementInformation>> BeforeEnumerationEnd
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

      /// <summary>
      /// Forwards the event (un)registration to the <see cref="AsyncEnumerationObservation{T, TMetadata}.AfterEnumerationEnd"/> event of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
      public event GenericEventHandler<EnumerationEndedEventArgs<TStatementInformation>> AfterEnumerationEnd
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

      /// <summary>
      /// Forwards the event (un)registration to the <see cref="AsyncEnumerationObservation{T, TMetadata}.AfterEnumerationItemEncountered"/> event of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
      public event GenericEventHandler<EnumerationItemEventArgs<TEnumerableItem, TStatementInformation>> AfterEnumerationItemEncountered
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

      /// <summary>
      /// Forwards the event (un)registration to the <see cref="AsyncEnumerationObservation{T}.BeforeEnumerationStart"/> event of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
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

      /// <summary>
      /// Forwards the event (un)registration to the <see cref="AsyncEnumerationObservation{T}.AfterEnumerationStart"/> event of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
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

      /// <summary>
      /// Forwards the event (un)registration to the <see cref="AsyncEnumerationObservation{T}.AfterEnumerationItemEncountered"/> event of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
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

      /// <summary>
      /// Forwards the event (un)registration to the <see cref="AsyncEnumerationObservation{T}.BeforeEnumerationEnd"/> event of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
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

      /// <summary>
      /// Forwards the event (un)registration to the <see cref="AsyncEnumerationObservation{T}.AfterEnumerationEnd"/> event of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
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

      /// <summary>
      /// Forwards the method call to <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}.PrepareStatementForExecution(TStatementInformation)"/> method of this <see cref="ConnectionFunctionality"/>.
      /// </summary>
      /// <param name="statementBuilder">The statement builder, created by <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}.CreateStatementBuilder(TStatementCreationArgs)"/> of this <see cref="VendorFunctionality"/>.</param>
      /// <returns>A new instance of <see cref="AsyncEnumeratorObservable{T, TMetadata}"/>.</returns>
      public AsyncEnumeratorObservable<TEnumerableItem, TStatementInformation> PrepareStatementForExecution( TStatementInformation statementBuilder )
      {
         return this.ConnectionFunctionality.PrepareStatementForExecution( statementBuilder );
      }

      /// <summary>
      /// Gets the <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> that this <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> is facade of.
      /// </summary>
      /// <value>The <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> that this <see cref="ConnectionImpl{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality, TActualVendorFunctionality, TConnectionFunctionality}"/> is facade of.</value>
      internal protected TConnectionFunctionality ConnectionFunctionality { get; }

      /// <summary>
      /// Gets the current <see cref="CancellationToken"/> of this connection.
      /// </summary>
      /// <value>The current <see cref="CancellationToken"/>.</value>
      /// <exception cref="InvalidOperationException">If there currently is no cancellation token available.</exception>
      public CancellationToken CurrentCancellationToken => this.ConnectionFunctionality.CurrentCancellationToken;
   }

   /// <summary>
   /// This class exists so that life would be a bit easier when using properties of <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> that do not require any generic parameters.
   /// </summary>
   /// <remarks>
   /// This class can not be instantiated directly, use <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> with generic arguments instead.
   /// </remarks>
   public abstract class DefaultConnectionFunctionality
   {
      private Object _cancellationToken;

      // Don't let this be subclassed directly - concrete implementations must use DefaultConnectionFunctionality with generic arguments.
      internal DefaultConnectionFunctionality()
      {

      }

      /// <summary>
      /// Gets or sets the current cancellation token.
      /// </summary>
      /// <value>The current cancellation token.</value>
      /// <remarks>
      /// Unlike <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/>, this class also provides a setter for this property. 
      /// </remarks>
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
         internal protected set
         {
            Interlocked.Exchange( ref this._cancellationToken, value );
         }
      }


      /// <summary>
      /// This method resets current cancellation token, so that <see cref="CurrentCancellationToken"/> property getter will throw.
      /// </summary>
      internal protected void ResetCancellationToken()
      {
         Interlocked.Exchange( ref this._cancellationToken, null );
      }

      /// <summary>
      /// Gets the value indicating whether this connection can be returned to the pool (e.g. underlying stream is open).
      /// </summary>
      /// <value>The value indicating whether this connection can be returned to the pool (e.g. underlying stream is open).</value>
      public abstract Boolean CanBeReturnedToPool { get; }
   }

   /// <summary>
   /// This class provides the actual implementation for <see cref="Connection{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendorFunctionality}"/> interface.
   /// </summary>
   /// <typeparam name="TStatement">The type of objects used to manipulate or query remote resource.</typeparam>
   /// <typeparam name="TStatementInformation">The type of objects describing <typeparamref name="TStatement"/>s.</typeparam>
   /// <typeparam name="TStatementCreationArgs">The type of object used to create an instance of <typeparamref name="TStatement"/>.</typeparam>
   /// <typeparam name="TEnumerableItem">The type of object representing the response of manipulation or querying remote resource.</typeparam>
   /// <typeparam name="TVendor">The type of object describing vendor-specific information.</typeparam>
   public abstract class DefaultConnectionFunctionality<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor> : DefaultConnectionFunctionality, Connection<TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor>
      where TStatement : TStatementInformation
      where TVendor : ConnectionVendorFunctionality<TStatement, TStatementCreationArgs>
   {

      /// <summary>
      /// Creates a new instance of <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/> with given <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/>.
      /// </summary>
      /// <param name="vendorFunctionality">The connection vendor.</param>
      public DefaultConnectionFunctionality( TVendor vendorFunctionality )
      {
         this.VendorFunctionality = vendorFunctionality;
      }


      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T, TMetadata}.BeforeEnumerationStart"/> event (initial call to <see cref="AsyncEnumerator{T}.MoveNextAsync(CancellationToken)"/>).
      /// Will be invoked every time something will start enumerating statement results for this connection.
      /// </summary>
      /// <seealso cref="AsyncEnumerationObservation{T, TMetadata}.BeforeEnumerationStart"/>
      public event GenericEventHandler<EnumerationStartedEventArgs<TStatementInformation>> BeforeEnumerationStart;

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T, TMetadata}.AfterEnumerationStart"/> event.
      /// Will be invoked every time something will start enumerating statement results for this connection (initial call to <see cref="AsyncEnumerator{T}.MoveNextAsync(CancellationToken)"/>).
      /// </summary>
      /// <seealso cref="AsyncEnumerationObservation{T, TMetadata}.AfterEnumerationStart"/>
      public event GenericEventHandler<EnumerationStartedEventArgs<TStatementInformation>> AfterEnumerationStart;

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T, TMetadata}.BeforeEnumerationEnd"/> event.
      /// Will be invoked every time something will encounter end of enumerating statement results for this connection (<see cref="AsyncEnumerator{T}.MoveNextAsync(CancellationToken)"/> will return <c>false</c>).
      /// </summary>
      /// <seealso cref="AsyncEnumerationObservation{T, TMetadata}.BeforeEnumerationEnd"/>
      public event GenericEventHandler<EnumerationEndedEventArgs<TStatementInformation>> BeforeEnumerationEnd;

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T, TMetadata}.AfterEnumerationEnd"/> event.
      /// Will be invoked every time something will encounter end of enumerating statement results for this connection (<see cref="AsyncEnumerator{T}.MoveNextAsync(CancellationToken)"/> will return <c>false</c>).
      /// </summary>
      /// <seealso cref="AsyncEnumerationObservation{T, TMetadata}.AfterEnumerationEnd"/>
      public event GenericEventHandler<EnumerationEndedEventArgs<TStatementInformation>> AfterEnumerationEnd;

      /// <summary>
      /// Implements the <see cref="AsyncEnumerationObservation{T, TMetadata}.AfterEnumerationItemEncountered"/> event.
      /// Will be invoked every time something will encounter an item when enumerating statement results for this connection (<see cref="AsyncEnumerator{T}.MoveNextAsync(CancellationToken)"/> will return <c>true</c>).
      /// </summary>
      /// <seealso cref="AsyncEnumerationObservation{T, TMetadata}.AfterEnumerationItemEncountered"/>
      public event GenericEventHandler<EnumerationItemEventArgs<TEnumerableItem, TStatementInformation>> AfterEnumerationItemEncountered;

      /// <summary>
      /// Explicitly implements the <see cref="AsyncEnumerationObservation{T}.BeforeEnumerationStart"/> event (initial call to <see cref="AsyncEnumerator{T}.MoveNextAsync(CancellationToken)"/>).
      /// Will be invoked every time something will start enumerating statement results for this connection.
      /// </summary>
      /// <seealso cref="AsyncEnumerationObservation{T}.BeforeEnumerationStart"/>
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
      /// Explicitly implements the <see cref="AsyncEnumerationObservation{T}.AfterEnumerationStart"/> event.
      /// Will be invoked every time something will start enumerating statement results for this connection (initial call to <see cref="AsyncEnumerator{T}.MoveNextAsync(CancellationToken)"/>).
      /// </summary>
      /// <seealso cref="AsyncEnumerationObservation{T}.AfterEnumerationStart"/>
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
      /// Explicitly implements the <see cref="AsyncEnumerationObservation{T}.AfterEnumerationItemEncountered"/> event.
      /// Will be invoked every time something will encounter an item when enumerating statement results for this connection (<see cref="AsyncEnumerator{T}.MoveNextAsync(CancellationToken)"/> will return <c>true</c>).
      /// </summary>
      /// <seealso cref="AsyncEnumerationObservation{T}.AfterEnumerationItemEncountered"/>
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
      /// Explicitly implements the <see cref="AsyncEnumerationObservation{T}.BeforeEnumerationEnd"/> event.
      /// Will be invoked every time something will encounter end of enumerating statement results for this connection (<see cref="AsyncEnumerator{T}.MoveNextAsync(CancellationToken)"/> will return <c>false</c>).
      /// </summary>
      /// <seealso cref="AsyncEnumerationObservation{T}.BeforeEnumerationEnd"/>
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
      /// Explicitly implements the <see cref="AsyncEnumerationObservation{T}.AfterEnumerationEnd"/> event.
      /// Will be invoked every time something will encounter end of enumerating statement results for this connection (<see cref="AsyncEnumerator{T}.MoveNextAsync(CancellationToken)"/> will return <c>false</c>).
      /// </summary>
      /// <seealso cref="AsyncEnumerationObservation{T}.AfterEnumerationEnd"/>
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
      /// Gets the <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/> of this <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.
      /// </summary>
      /// <value>The <see cref="ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}"/> of this <see cref="DefaultConnectionFunctionality{TStatement, TStatementInformation, TStatementCreationArgs, TEnumerableItem, TVendor}"/>.</value>
      public TVendor VendorFunctionality { get; }

      /// <summary>
      /// Validates the statement by calling <see cref="ValidateStatementOrThrow(TStatementInformation)"/> and then creates a new <see cref="AsyncEnumeratorObservable{T, TMetadata}"/> by calling <see cref="CreateEnumerator"/>.
      /// </summary>
      /// <param name="statement">The statement which describes how to manipulate/query remote resource.</param>
      /// <returns>The <see cref="AsyncEnumeratorObservable{T, TMetadata}"/> which can be used to execute the <paramref name="statement"/> statement and iterate the possible results.</returns>
      public AsyncEnumeratorObservable<TEnumerableItem, TStatementInformation> PrepareStatementForExecution( TStatementInformation statement )
      {
         var info = statement is TStatement stmt ? this.GetInformationFromStatement( stmt ) : statement;
         this.ValidateStatementOrThrow( info );
         return this.CreateEnumerator(
            info,
            () => this.BeforeEnumerationStart,
            () => this.AfterEnumerationStart,
            () => this.BeforeEnumerationEnd,
            () => this.AfterEnumerationEnd,
            () => this.AfterEnumerationItemEncountered
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
      /// <param name="getGlobalBeforeEnumerationExecutionStart">Callback to get global before enumeration start -event.</param>
      /// <param name="getGlobalAfterEnumerationExecutionStart">Callback to get global after enumeration start -event.</param>
      /// <param name="getGlobalBeforeEnumerationExecutionEnd">Callback to get global before enumeration end -event.</param>
      /// <param name="getGlobalAfterEnumerationExecutionEnd">Callback to get global after enumeration end -event.</param>
      /// <param name="getGlobalAfterEnumerationExecutionItemEncountered">Callback to get global enumeration encountered -event.</param>
      /// <returns>The <see cref="AsyncEnumeratorObservable{T, TMetadata}"/> used to asynchronously enumerate over the results of statement execution.</returns>
      /// <seealso cref="AsyncEnumeratorFactory.CreateSequentialObservableEnumerator{T, TMetadata}(InitialMoveNextAsyncDelegate{T}, TMetadata, Func{GenericEventHandler{EnumerationStartedEventArgs{TMetadata}}}, Func{GenericEventHandler{EnumerationStartedEventArgs{TMetadata}}}, Func{GenericEventHandler{EnumerationEndedEventArgs{TMetadata}}}, Func{GenericEventHandler{EnumerationEndedEventArgs{TMetadata}}}, Func{GenericEventHandler{EnumerationItemEventArgs{T, TMetadata}}})"/>
      /// <seealso cref="AsyncEnumeratorFactory.CreateParallelObservableEnumerator{T, TMoveNext, TMetadata}(SynchronousMoveNextDelegate{TMoveNext}, GetNextItemAsyncDelegate{T, TMoveNext}, EnumerationEndedDelegate, TMetadata, Func{GenericEventHandler{EnumerationStartedEventArgs{TMetadata}}}, Func{GenericEventHandler{EnumerationStartedEventArgs{TMetadata}}}, Func{GenericEventHandler{EnumerationEndedEventArgs{TMetadata}}}, Func{GenericEventHandler{EnumerationEndedEventArgs{TMetadata}}}, Func{GenericEventHandler{EnumerationItemEventArgs{T, TMetadata}}})"/>
      protected abstract AsyncEnumeratorObservable<TEnumerableItem, TStatementInformation> CreateEnumerator(
         TStatementInformation metadata,
         Func<GenericEventHandler<EnumerationStartedEventArgs<TStatementInformation>>> getGlobalBeforeEnumerationExecutionStart,
         Func<GenericEventHandler<EnumerationStartedEventArgs<TStatementInformation>>> getGlobalAfterEnumerationExecutionStart,
         Func<GenericEventHandler<EnumerationEndedEventArgs<TStatementInformation>>> getGlobalBeforeEnumerationExecutionEnd,
         Func<GenericEventHandler<EnumerationEndedEventArgs<TStatementInformation>>> getGlobalAfterEnumerationExecutionEnd,
         Func<GenericEventHandler<EnumerationItemEventArgs<TEnumerableItem, TStatementInformation>>> getGlobalAfterEnumerationExecutionItemEncountered
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

}