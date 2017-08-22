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
using System.Linq;
using System.Text;
using System.Threading;
using CBAM.SQL;
using System.Reflection;
using System.Threading.Tasks;
using UtilPack;

namespace CBAM.SQL
{
   /// <summary>
   /// This interface provides read-only API for <see cref="SQLStatementBuilder"/>.
   /// </summary>
   /// <seealso cref="SQLStatementBuilder"/>
   /// <seealso cref="SQLStatementBuilder.StatementBuilderInformation"/>
   public interface SQLStatementBuilderInformation
   {
      /// <summary>
      /// Gets the amount of SQL parameters (question marks) in this SQL statement.
      /// </summary>
      /// <value>The amount of SQL parameters (question marks) in this SQL statement.</value>
      Int32 SQLParameterCount { get; }

      /// <summary>
      /// Gets the current count of batch parameters.
      /// </summary>
      /// <value>The current count of batch parameters.</value>
      Int32 BatchParameterCount { get; }

      /// <summary>
      /// Gets information about parameter at given index.
      /// Maximum amount of parametrs can be queried via <see cref="SQLParameterCount"/> property.
      /// </summary>
      /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLParameterCount"/></c>.</param>
      /// <returns>The parameter information at given index.</returns>
      /// <exception cref="ArgumentException">If <paramref name="parameterIndex"/> is out of bounds.</exception>
      /// <seealso cref="StatementParameter"/>
      StatementParameter GetParameterInfo( Int32 parameterIndex );

      /// <summary>
      /// Gets information about parameter which has been previously added to batch of parameters.
      /// </summary>
      /// <param name="batchIndex">The batch index. Should be <c>0 ≤ <paramref name="batchIndex"/> &lt; <see cref="BatchParameterCount"/></c>.</param>
      /// <param name="parameterIndex">The parameter index. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLParameterCount"/></c>.</param>
      /// <returns>The parameter information at given batch and parameter indices.</returns>
      /// <exception cref="ArgumentException">If either of <paramref name="batchIndex"/> or <paramref name="parameterIndex"/> is out of bounds.</exception>
      /// <seealso cref="StatementParameter"/>
      StatementParameter GetBatchParameterInfo( Int32 batchIndex, Int32 parameterIndex );

      /// <summary>
      /// Gets the textual SQL statement of this <see cref="SQLStatementBuilderInformation"/>.
      /// </summary>
      /// <value>The textual SQL statement of this <see cref="SQLStatementBuilderInformation"/>.</value>
      String SQL { get; }
   }

   /// <summary>
   /// This interface extends read-only <see cref="SQLStatementBuilderInformation"/> with modifiable API.
   /// Not that just like in JDBC, the parameters for prepared statement should be question marks in statement SQL given to <see cref="CBAM.Abstractions.ConnectionVendorFunctionality{TStatement, TStatementCreationArgs}.CreateStatementBuilder(TStatementCreationArgs)"/> method.
   /// </summary>
   public interface SQLStatementBuilder : SQLStatementBuilderInformation
   {

      /// <summary>
      /// Sets the parameter at given index to given value
      /// </summary>
      /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLStatementBuilderInformation.SQLParameterCount"/></c>.</param>
      /// <param name="value">The value to set. May be <c>null</c>.</param>
      /// <param name="clrType">The type of the value. If value is not <c>null</c> and this parameter is <c>null</c>, then the result of <see cref="Object.GetType"/> obtained from <paramref name="value"/> will be used.</param>
      /// <exception cref="ArgumentNullException">If both <paramref name="value"/> and <paramref name="clrType"/> are <c>null</c></exception>
      /// <exception cref="ArgumentException">If <paramref name="parameterIndex"/> is out of bounds.</exception>
      /// <seealso cref="E_CBAM.SetNull(SQLStatementBuilder, int, Type)"/>
      /// <seealso cref="E_CBAM.SetParameterBoolean(SQLStatementBuilder, int, bool?)"/>
      /// <seealso cref="E_CBAM.SetParameterInt16(SQLStatementBuilder, int, short?)"/>
      /// <seealso cref="E_CBAM.SetParameterInt32(SQLStatementBuilder, int, int?)"/>
      /// <seealso cref="E_CBAM.SetParameterInt64(SQLStatementBuilder, int, long?)"/>
      /// <seealso cref="E_CBAM.SetParameterString(SQLStatementBuilder, int, string)"/>
      /// <seealso cref="E_CBAM.SetParameterDateTime(SQLStatementBuilder, int, DateTime?)"/>
      /// <seealso cref="E_CBAM.SetParameterArray{TElement}(SQLStatementBuilder, int, Array)"/>
      void SetParameterObjectWithType( Int32 parameterIndex, Object value, Type clrType );

      /// <summary>
      /// Given that this builder contains a set of parameters added via <see cref="SetParameterObjectWithType"/>, adds that set to a list of batched parameters.
      /// Then clears the current set of parameters.
      /// </summary>
      /// <exception cref="InvalidOperationException">If there is at least one parameter that has not been set.</exception>
      void AddBatch();

      /// <summary>
      /// Gets the read-only <see cref="SQLStatementBuilderInformation"/> object which has same state as this <see cref="SQLStatementBuilder"/>.
      /// </summary>
      /// <value>The read-only <see cref="SQLStatementBuilderInformation"/> object which has same state as this <see cref="SQLStatementBuilder"/>.</value>
      /// <remarks>
      /// The returned object must not be castable back to this <see cref="SQLStatementBuilder"/>.
      /// </remarks>
      SQLStatementBuilderInformation StatementBuilderInformation { get; }
   }

   /// <summary>
   /// This interface contains information about a single parameter in <see cref="SQLStatementBuilderInformation"/>.
   /// </summary>
   public interface StatementParameter // : IEquatable<StatementParameter>
   {
      /// <summary>
      /// Gets the <see cref="Type"/> of the parameter value.
      /// </summary>
      /// <value>The <see cref="Type"/> of the parameter value.</value>
      Type ParameterCILType { get; }

      /// <summary>
      /// Gets the parameter value.
      /// May be <c>null</c>.
      /// </summary>
      /// <value>The parameter value.</value>
      Object ParameterValue { get; }
   }
}

public static partial class E_CBAM
{
   /// <summary>
   /// This is shortcut method to set parameter as a <see cref="Boolean"/> value at given index.
   /// </summary>
   /// <param name="stmt">This <see cref="SQLStatementBuilder"/>.</param>
   /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLStatementBuilderInformation.SQLParameterCount"/></c>.</param>
   /// <param name="value">The nullable <see cref="Boolean"/> value to set.</param>
   /// <exception cref="NullReferenceException">If this <see cref="SQLStatementBuilder"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentException">If <paramref name="parameterIndex"/> is out of bounds.</exception>
   public static void SetParameterBoolean( this SQLStatementBuilder stmt, Int32 parameterIndex, Boolean? value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( Boolean ) );
   }

   /// <summary>
   /// This is shortcut method to set parameter as a <see cref="Int32"/> value at given index.
   /// </summary>
   /// <param name="stmt">This <see cref="SQLStatementBuilder"/>.</param>
   /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLStatementBuilderInformation.SQLParameterCount"/></c>.</param>
   /// <param name="value">The nullable <see cref="Int32"/> value to set.</param>
   /// <exception cref="NullReferenceException">If this <see cref="SQLStatementBuilder"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentException">If <paramref name="parameterIndex"/> is out of bounds.</exception>
   public static void SetParameterInt32( this SQLStatementBuilder stmt, Int32 parameterIndex, Int32? value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( Int32 ) );
   }

   /// <summary>
   /// This is shortcut method to set parameter as a <see cref="Int16"/> value at given index.
   /// </summary>
   /// <param name="stmt">This <see cref="SQLStatementBuilder"/>.</param>
   /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLStatementBuilderInformation.SQLParameterCount"/></c>.</param>
   /// <param name="value">The nullable <see cref="Int16"/> value to set.</param>
   /// <exception cref="NullReferenceException">If this <see cref="SQLStatementBuilder"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentException">If <paramref name="parameterIndex"/> is out of bounds.</exception>
   public static void SetParameterInt16( this SQLStatementBuilder stmt, Int32 parameterIndex, Int16? value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( Int16 ) );
   }

   /// <summary>
   /// This is shortcut method to set parameter as a <see cref="Int64"/> value at given index.
   /// </summary>
   /// <param name="stmt">This <see cref="SQLStatementBuilder"/>.</param>
   /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLStatementBuilderInformation.SQLParameterCount"/></c>.</param>
   /// <param name="value">The nullable <see cref="Int64"/> value to set.</param>
   /// <exception cref="ArgumentException">If <paramref name="parameterIndex"/> is out of bounds.</exception>
   public static void SetParameterInt64( this SQLStatementBuilder stmt, Int32 parameterIndex, Int64? value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( Int64 ) );
   }

   /// <summary>
   /// This is shortcut method to set parameter as a <see cref="DateTime"/> value at given index.
   /// </summary>
   /// <param name="stmt">This <see cref="SQLStatementBuilder"/>.</param>
   /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLStatementBuilderInformation.SQLParameterCount"/></c>.</param>
   /// <param name="value">The nullable <see cref="DateTime"/> value to set.</param>
   /// <exception cref="NullReferenceException">If this <see cref="SQLStatementBuilder"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentException">If <paramref name="parameterIndex"/> is out of bounds.</exception>
   public static void SetParameterDateTime( this SQLStatementBuilder stmt, Int32 parameterIndex, DateTime? value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( DateTime ) );
   }

   /// <summary>
   /// This is shortcut method to set parameter as a <see cref="String"/> value at given index.
   /// </summary>
   /// <param name="stmt">This <see cref="SQLStatementBuilder"/>.</param>
   /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLStatementBuilderInformation.SQLParameterCount"/></c>.</param>
   /// <param name="value">The nullable <see cref="String"/> value to set.</param>
   /// <exception cref="NullReferenceException">If this <see cref="SQLStatementBuilder"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentException">If <paramref name="parameterIndex"/> is out of bounds.</exception>
   public static void SetParameterString( this SQLStatementBuilder stmt, Int32 parameterIndex, String value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( String ) );
   }

   /// <summary>
   /// This is shortcut method to set parameter as an array value at given index.
   /// </summary>
   /// <param name="stmt">This <see cref="SQLStatementBuilder"/>.</param>
   /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLStatementBuilderInformation.SQLParameterCount"/></c>.</param>
   /// <param name="value">The array to set.</param>
   /// <exception cref="NullReferenceException">If this <see cref="SQLStatementBuilder"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="parameterIndex"/> is out of bounds.</exception>
   public static void SetParameterArray<TElement>( this SQLStatementBuilder stmt, Int32 parameterIndex, Array value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( TElement[] ) );
   }


   /// <summary>
   /// This is shortcut method to set parameter as value of specified type, when the type is known at compile time.
   /// </summary>
   /// <typeparam name="T">The type of the value.</typeparam>
   /// <param name="stmt">This <see cref="SQLStatementBuilder"/>.</param>
   /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLStatementBuilderInformation.SQLParameterCount"/></c>.</param>
   /// <param name="value">The value to set.</param>
   /// <exception cref="NullReferenceException">If this <see cref="SQLStatementBuilder"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentNullException">If <paramref name="parameterIndex"/> is out of bounds.</exception>
   public static void SetParameterObject<T>( this SQLStatementBuilder stmt, Int32 parameterIndex, Object value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( T ) );
   }

   /// <summary>
   /// Sets the parameter at given index to <c>null</c>.
   /// </summary>
   /// <param name="stmt">This <see cref="SQLStatementBuilder"/>.</param>
   /// <param name="parameterIndex">The index of the parameter. Should be <c>0 ≤ <paramref name="parameterIndex"/> &lt; <see cref="SQLStatementBuilderInformation.SQLParameterCount"/></c>.</param>
   /// <param name="clrType">The expected type of the SQL value.</param>
   public static void SetNull( this SQLStatementBuilder stmt, Int32 parameterIndex, Type clrType )
   {
      stmt.SetParameterObjectWithType( parameterIndex, null, clrType );
   }


   /// <summary>
   /// Helper method to detect whether this <see cref="SQLStatementBuilderInformation"/> is simple - that is, it contains no parameters and has no batched parameters either.
   /// </summary>
   /// <param name="stmt">This <see cref="SQLStatementBuilderInformation"/>.</param>
   /// <returns><c>true</c> if this <see cref="SQLStatementBuilderInformation"/> has no batched parameter sets, and also has no SQL parameters; <c>false</c> otherwise.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLStatementBuilderInformation"/> is <c>null</c>.</exception>
   public static Boolean IsSimple( this SQLStatementBuilderInformation stmt )
   {
      return !stmt.HasBatchParameters() && stmt.SQLParameterCount == 0;
   }

   /// <summary>
   /// Helper method to detect whether this <see cref="SQLStatementBuilderInformation"/> has any batch parameters - that is, its <see cref="SQLStatementBuilderInformation.BatchParameterCount"/> is greater than <c>0</c>.
   /// </summary>
   /// <param name="stmt">This <see cref="SQLStatementBuilderInformation"/>.</param>
   /// <returns><c>true</c> if this <see cref="SQLStatementBuilderInformation"/> has batch parameters; <c>false</c> otherwise.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLStatementBuilderInformation"/> is <c>null</c>.</exception>
   public static Boolean HasBatchParameters( this SQLStatementBuilderInformation stmt )
   {
      return stmt.BatchParameterCount > 0;
   }

   /// <summary>
   /// Helper method to get enumerable of <see cref="StatementParameter"/>s of this <see cref="SQLStatementBuilderInformation"/>.
   /// </summary>
   /// <param name="stmt">This <see cref="SQLStatementBuilderInformation"/>.</param>
   /// <returns>An enumerable of <see cref="StatementParameter"/>s of this <see cref="SQLStatementBuilderInformation"/>.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLStatementBuilderInformation"/> is <c>null</c>.</exception>
   public static IEnumerable<StatementParameter> GetParametersEnumerable( this SQLStatementBuilderInformation stmt )
   {
      var max = stmt.SQLParameterCount;
      for ( var i = 0; i < max; ++i )
      {
         yield return stmt.GetParameterInfo( i );
      }
   }

   /// <summary>
   /// Helper method to get enumerable of batched <see cref="StatementParameter"/>s at given batch index.
   /// </summary>
   /// <param name="stmt">This <see cref="SQLStatementBuilderInformation"/>.</param>
   /// <param name="batchIndex">The batch index of the parameter set to enumerate. Should be <c>0 ≤ <paramref name="batchIndex"/> &lt; <see cref="SQLStatementBuilderInformation.BatchParameterCount"/></c>.</param>
   /// <returns>An enumerable of batched <see cref="StatementParameter"/>s at given batch index.</returns>
   /// <exception cref="NullReferenceException">If this <see cref="SQLStatementBuilderInformation"/> is <c>null</c>.</exception>
   /// <exception cref="ArgumentException">If <paramref name="batchIndex"/> is out of bounds.</exception>
   public static IEnumerable<StatementParameter> GetParametersEnumerable( this SQLStatementBuilderInformation stmt, Int32 batchIndex )
   {
      var max = stmt.SQLParameterCount;
      for ( var i = 0; i < max; ++i )
      {
         yield return stmt.GetBatchParameterInfo( batchIndex, i );
      }
   }

}


