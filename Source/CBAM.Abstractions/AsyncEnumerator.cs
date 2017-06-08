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

namespace CBAM.Abstractions
{
   public interface AsyncEnumerator<out T>
   {
      // Initial call will execute the statement.
      Task<Boolean> MoveNextAsync();
      T Current { get; }
      Task ResetAsync();
   }

   public interface AsyncEnumeratorObservable<out T> : AsyncEnumerator<T>, AsyncEnumerationObservation<T>
   {

   }

   public interface AsyncEnumeratorObservable<out T, out TMetadata> : AsyncEnumeratorObservable<T>, AsyncEnumerationObservation<T, TMetadata>
   {

   }

   public interface AsyncEnumerationObservation<out T>
   {
      event GenericEventHandler<EnumerationStartedEventArgs> BeforeEnumerationStart;
      event GenericEventHandler<EnumerationStartedEventArgs> AfterEnumerationStart;

      event GenericEventHandler<EnumerationItemEventArgs<T>> AfterEnumerationItemEncountered;

      event GenericEventHandler<EnumerationEndedEventArgs> BeforeEnumerationEnd;
      event GenericEventHandler<EnumerationEndedEventArgs> AfterEnumerationEnd;
   }

   public interface AsyncEnumerationObservation<out T, out TMetadata> : AsyncEnumerationObservation<T>
   {
      new event GenericEventHandler<EnumerationStartedEventArgs<TMetadata>> BeforeEnumerationStart;
      new event GenericEventHandler<EnumerationStartedEventArgs<TMetadata>> AfterEnumerationStart;

      new event GenericEventHandler<EnumerationItemEventArgs<T, TMetadata>> AfterEnumerationItemEncountered;

      new event GenericEventHandler<EnumerationEndedEventArgs<TMetadata>> BeforeEnumerationEnd;
      new event GenericEventHandler<EnumerationEndedEventArgs<TMetadata>> AfterEnumerationEnd;
   }

   public interface EnumerationStartedEventArgs
   {
   }

   public interface EnumerationItemEventArgs<out T>
   {
      T Item { get; }
   }

   public interface EnumerationEndedEventArgs : EnumerationStartedEventArgs
   {

   }

   public interface EnumerationStartedEventArgs<out TMetadata> : EnumerationStartedEventArgs
   {
      TMetadata Metadata { get; }
   }

   public interface EnumerationEndedEventArgs<out TMetadata> : EnumerationStartedEventArgs<TMetadata>, EnumerationEndedEventArgs
   {

   }

   public interface EnumerationItemEventArgs<out T, out TMetadata> : EnumerationItemEventArgs<T>
   {
      TMetadata Metadata { get; }
   }

   public static class EnumerationEventArgsUtility
   {
      private sealed class EnumerationStarted : EnumerationStartedEventArgs
      {
      }

      private sealed class EnumerationEnded : EnumerationEndedEventArgs
      {

      }

      static EnumerationEventArgsUtility()
      {
         StatelessStart = new EnumerationStarted();
         StatelessEnd = new EnumerationEnded();
      }

      public static EnumerationStartedEventArgs StatelessStart { get; }
      public static EnumerationEndedEventArgs StatelessEnd { get; }
   }

}

public static partial class E_CBAM
{
   public static async Task EnumerateAsync<T>( this AsyncEnumerator<T> enumerator, Action<T> action )
   {
      try
      {
         while ( await enumerator.MoveNextAsync() )
         {
            action?.Invoke( enumerator.Current );
         }
      }
      catch
      {
         try
         {
            while ( await enumerator.MoveNextAsync() ) ;
         }
         catch
         {
            // Ignore
         }

         throw;
      }
   }

   public static async Task EnumerateAsync<T>( this AsyncEnumerator<T> enumerator, Func<T, Task> asyncAction )
   {
      try
      {
         while ( await enumerator.MoveNextAsync() )
         {
            if ( asyncAction != null )
            {
               await asyncAction( enumerator.Current );
            }
         }
      }
      catch
      {
         try
         {
            while ( await enumerator.MoveNextAsync() ) ;
         }
         catch
         {
            // Ignore
         }

         throw;
      }
   }
}