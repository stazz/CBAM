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

namespace CBAM.Abstractions
{
   public interface AsyncEnumerator<out T>
   {
      // Initial call will execute the statement.
      Task<Boolean> MoveNextAsync();
      T Current { get; }
      Task ResetAsync();
   }

   public class IterationEndedEventArgs : EventArgs
   {

   }

   public interface AsyncEnumeratorObservable<out T> : AsyncEnumerator<T>, ExecutionItemObservable<T>
   {

   }

   public interface AsyncEnumeratorObservable<out T, out TStatement> : AsyncEnumeratorObservable<T>, ConnectionObservable<TStatement, T>
   {

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
            action( enumerator.Current );
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
            await asyncAction( enumerator.Current );
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