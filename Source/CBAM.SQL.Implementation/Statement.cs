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
using CBAM.SQL.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;


namespace CBAM.SQL.Implementation
{
   /// <summary>
   /// This class provides default implementation for <see cref="SQLStatementBuilderInformation"/>.
   /// </summary>
   /// <typeparam name="TParameter">The actual type of parameter, derivable from <see cref="StatementParameter"/>.</typeparam>
   /// <typeparam name="TList">The type of list holding batch parameters.</typeparam>
   public class StatementBuilderInformationImpl<TParameter, TList> : SQLStatementBuilderInformation
      where TParameter : StatementParameter
      where TList : class,
#if NET40
      IList<TParameter[]>
#else
      IReadOnlyList<TParameter[]>
#endif
   {
      /// <summary>
      /// This field holds the batch parameter set list.
      /// </summary>
      protected readonly TList _batchParameters;

      /// <summary>
      /// This field holds the current parameter set as array of parameters.
      /// </summary>
      protected readonly TParameter[] _currentParameters;

      /// <summary>
      /// Creates a new instance of <see cref="StatementBuilderInformationImpl{TParameter, TList}"/>.
      /// </summary>
      /// <param name="sql">The textual SQL statement.</param>
      /// <param name="currentParameters">Current array of parameters. May be <c>null</c>.</param>
      /// <param name="batchParams">The batch parameter list.</param>
      /// <exception cref="ArgumentNullException">If either of <paramref name="sql"/> or <paramref name="batchParams"/> is <c>null</c>.</exception>
      public StatementBuilderInformationImpl(
         String sql,
         TParameter[] currentParameters,
         TList batchParams
         )
      {
         this.SQL = ArgumentValidator.ValidateNotEmpty( nameof( sql ), sql );
         this._currentParameters = currentParameters ?? Empty<TParameter>.Array;
         this._batchParameters = ArgumentValidator.ValidateNotNull( nameof( batchParams ), batchParams );
      }

      /// <summary>
      /// Implements <see cref="SQLStatementBuilderInformation.SQLParameterCount"/> and gets the amount of parameters in this statement.
      /// </summary>
      /// <value>The amount of parameters in this statement.</value>
      /// <remarks>
      /// If this returns <c>0</c>, this is not considred a prepared statement, but just simple statement instead.
      /// </remarks>
      public Int32 SQLParameterCount => this._currentParameters.Length;

      /// <summary>
      /// Implements <see cref="SQLStatementBuilderInformation.SQL"/> and gets the textual SQL statement of this statement builder.
      /// </summary>
      /// <value>The textual SQL statement of this statement builder.</value>
      public String SQL { get; }

      /// <summary>
      /// Implements <see cref="SQLStatementBuilderInformation.BatchParameterCount"/> and gets the amount of batched parameter sets.
      /// </summary>
      /// <value>The amount of batched parameter sets.</value>
      /// <remarks>
      /// Both simple and prepared statements may be batched.
      /// </remarks>
      public Int32 BatchParameterCount => this._batchParameters.Count;

      /// <summary>
      /// Implements <see cref="SQLStatementBuilderInformation.GetParameterInfo(int)"/> and returns <see cref="StatementParameter"/> at given index.
      /// </summary>
      /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLParameterCount"/></c>.</param>
      /// <returns>The parameter information at given index.</returns>
      /// <exception cref="ArgumentException">If <paramref name="parameterIndex"/> is out of bounds.</exception>
      /// <seealso cref="StatementParameter"/>
      public StatementParameter GetParameterInfo( Int32 parameterIndex )
      {
         return this._currentParameters.CheckArrayIndexAndReturnOrThrow( parameterIndex, nameof( parameterIndex ) )[parameterIndex];
      }

      /// <summary>
      /// Implements <see cref="SQLStatementBuilderInformation.GetBatchParameterInfo(int, int)"/> and returns <see cref="StatementParameter"/> at given batch and parameter index.
      /// </summary>
      /// <param name="batchIndex">The batch index. Should be <c>0 ≤ <paramref name="batchIndex"/> &lt; <see cref="BatchParameterCount"/></c>.</param>
      /// <param name="parameterIndex">The parameter index. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLParameterCount"/></c>.</param>
      /// <returns>The parameter information at given batch and parameter indices.</returns>
      /// <exception cref="ArgumentException">If either of <paramref name="batchIndex"/> or <paramref name="parameterIndex"/> is out of bounds.</exception>
      /// <seealso cref="StatementParameter"/>
      public StatementParameter GetBatchParameterInfo( Int32 batchIndex, Int32 parameterIndex )
      {
         return this._batchParameters
            .CheckListIndexAndReturnOrThrow( batchIndex, nameof( batchIndex ) )[batchIndex]
            .CheckArrayIndexAndReturnOrThrow( parameterIndex, nameof( parameterIndex ) )[parameterIndex];
      }
   }

   /// <summary>
   /// This class implements read-write functionality of <see cref="SQLStatementBuilder"/> by extending <see cref="StatementBuilderInformationImpl{TParameter, TList}"/> and providing implementation which should be common for all SQL vendors.
   /// </summary>
   /// <typeparam name="TParameter">The actual type of parameter, derivable from <see cref="StatementParameter"/>.</typeparam>
   public abstract class StatementBuilderImpl<TParameter> : StatementBuilderInformationImpl<TParameter, List<TParameter[]>>, SQLStatementBuilder
      where TParameter : StatementParameter
   {

      /// <summary>
      /// Initializes a new instance of <see cref="StatementBuilderImpl{TParameter}"/> with given parameters.
      /// </summary>
      /// <param name="information">The <see cref="SQLStatementBuilderInformation"/> that will hold the read-only access to this builder.</param>
      /// <param name="currentParams">The current parameter set as array of parameters.</param>
      /// <param name="batchParams">The batch parameter set list.</param>
      /// <exception cref="ArgumentNullException">If either of <paramref name="information"/> or <paramref name="batchParams"/> is <c>null</c>. Also occurs when <see cref="SQLStatementBuilderInformation.SQL"/> property of <paramref name="information"/> returns <c>null</c>.</exception>
      public StatementBuilderImpl(
         SQLStatementBuilderInformation information,
         TParameter[] currentParams,
         List<TParameter[]> batchParams
         ) : base( ArgumentValidator.ValidateNotNull( nameof( information ), information ).SQL, currentParams, batchParams )
      {
         this.StatementBuilderInformation = information;
      }

      /// <summary>
      /// Implements <see cref="SQLStatementBuilder.StatementBuilderInformation"/> and gets the read-only <see cref="SQLStatementBuilderInformation"/> of this statement builder.
      /// </summary>
      /// <value>The read-only <see cref="SQLStatementBuilderInformation"/> of this statement builder.</value>
      public SQLStatementBuilderInformation StatementBuilderInformation { get; }

      /// <summary>
      /// Implements <see cref="SQLStatementBuilder.AddBatch"/> method and adds current parameter set to list of batched parameter sets.
      /// </summary>
      /// <exception cref="InvalidOperationException">If there is at least one parameter that has not been set.</exception>
      public void AddBatch()
      {
         Int32 idx;
         if ( (idx = Array.FindIndex( this._currentParameters, p => p == null )) >= 0 )
         {
            throw new InvalidOperationException( $"The parameter at index {idx} has not been set." );
         }

         //if ( this._batchParameters.Count > 0 )
         //{
         //   // Must verify batch parameters
         //   var prevRow = this._batchParameters[this._batchParameters.Count - 1];
         //   for ( var i = 0; i < this._currentParameters.Length; ++i )
         //   {
         //      var exc = this.VerifyBatchParameters( prevRow[i], this._currentParameters[i] );
         //      if ( exc != null )
         //      {
         //         throw exc;
         //      }
         //   }
         //}

         this._batchParameters.Add( this._currentParameters.CreateArrayCopy() );
         Array.Clear( this._currentParameters, 0, this._currentParameters.Length );
      }

      /// <summary>
      /// Implements <see cref="SQLStatementBuilder.SetParameterObjectWithType(int, object, Type)"/> and adds given parameter to current parameter set.
      /// </summary>
      /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLStatementBuilderInformation.SQLParameterCount"/></c>.</param>
      /// <param name="value">The value to set. May be <c>null</c>.</param>
      /// <param name="clrType">The type of the value. If value is not <c>null</c> and this parameter is <c>null</c>, then the result of <see cref="Object.GetType"/> obtained from <paramref name="value"/> will be used.</param>
      /// <exception cref="ArgumentNullException">If both <paramref name="value"/> and <paramref name="clrType"/> are <c>null</c></exception>
      /// <exception cref="ArgumentException">If <paramref name="parameterIndex"/> is out of bounds.</exception>
      public void SetParameterObjectWithType( Int32 parameterIndex, Object value, Type clrType )
      {
         if ( clrType == null && value == null )
         {
            throw new ArgumentNullException( $"Both {nameof( value )} and {nameof( clrType )} were null." );
         }
         this._currentParameters[parameterIndex] = this.CreateStatementParameter( parameterIndex, value, clrType ?? value.GetType() );
      }

      /// <summary>
      /// Derived classes should implement this method to return custom instances of <see cref="StatementParameter"/>.
      /// </summary>
      /// <param name="parameterIndex">The index of the parameter.</param>
      /// <param name="value">The value to set.</param>
      /// <param name="clrType">The type of the value.</param>
      /// <returns>A new instance of <see cref="StatementParameter"/>.</returns>
      protected abstract TParameter CreateStatementParameter( Int32 parameterIndex, Object value, Type clrType );

   }

   /// <summary>
   /// This class provides default implementation for <see cref="StatementParameter"/>.
   /// </summary>
   public class StatementParameterImpl : StatementParameter
   {
      /// <summary>
      /// Creates a new instance of <see cref="StatementParameterImpl"/> with given type and value.
      /// </summary>
      /// <param name="cilType">The parameter type.</param>
      /// <param name="value">The parameter value.</param>
      /// <exception cref="ArgumentNullException">If <paramref name="cilType"/> is <c>null</c>.</exception>
      public StatementParameterImpl(
         Type cilType,
         Object value
         )
      {
         this.ParameterCILType = ArgumentValidator.ValidateNotNull( nameof( cilType ), cilType );
         this.ParameterValue = value;
      }

      /// <summary>
      /// Implements <see cref="StatementParameter.ParameterCILType"/> and gets the type of the parameter value.
      /// </summary>
      /// <value>The type of the parameter value.</value>
      public Type ParameterCILType { get; }

      /// <summary>
      /// Implements <see cref="StatementParameter.ParameterValue"/> and gets the parameter value.
      /// May be <c>null</c>.
      /// </summary>
      /// <value>The parameter value.</value>
      public Object ParameterValue { get; }

      //public Boolean Equals( StatementParameter other )
      //{
      //   return ReferenceEquals( this, other )
      //      || ( other != null
      //      && Equals( this.ParameterCILType, other.ParameterCILType )
      //      && Equals( this.ParameterValue, other.ParameterValue )
      //      );
      //}
   }

   // TODO move these to UtilPack
   internal static class CBAMExtensions
   {

      public static Boolean CheckArrayIndex( this Array array, Int32 index )
      {
         return array != null && index >= 0 && index < array.Length;
      }

      public static void CheckArrayIndexOrThrow( this Array array, Int32 index, String indexParameterName = null )
      {
         if ( !array.CheckArrayIndex( index ) )
         {
            throw new ArgumentException( String.IsNullOrEmpty( indexParameterName ) ? "array index" : indexParameterName );
         }
      }

      public static T[] CheckArrayIndexAndReturnOrThrow<T>( this T[] array, Int32 index, String indexParameterName = null )
      {
         array.CheckArrayIndexOrThrow( index, indexParameterName );
         return array;
      }

      // TODO Collections.Generic.IList<T> does not extend Collections.List...

      public static Boolean CheckListIndex<T>( this
#if NET40
      IList<T>
#else
      IReadOnlyList<T>
#endif
         list, Int32 index )
      {
         return list != null && index >= 0 && index < list.Count;
      }

      public static void CheckListIndexOrThrow<T>( this
#if NET40
      IList<T>
#else
      IReadOnlyList<T>
#endif
         list, Int32 index, String indexParameterName = null )
      {
         if ( !list.CheckListIndex( index ) )
         {
            throw new ArgumentException( String.IsNullOrEmpty( indexParameterName ) ? "list index" : indexParameterName );
         }
      }


      public static
#if NET40
      IList<T[]>
#else
      IReadOnlyList<T[]>
#endif
         CheckListIndexAndReturnOrThrow<T>( this
#if NET40
      IList<T[]>
#else
      IReadOnlyList<T[]>
#endif
         list, Int32 index, String indexParameterName = null )
      {
         list.CheckListIndexOrThrow( index, indexParameterName );
         return list;
      }

      public static T[] NewArrayOfLength<T>( this Int32 newLength, String msg = null )
      {
         if ( newLength < 0 )
         {
            throw new ArgumentException( msg ?? "Invalid array length" );
         }
         else if ( newLength == 0 )
         {
            return Empty<T>.Array;
         }
         else
         {
            return new T[newLength];
         }
      }
   }
}


