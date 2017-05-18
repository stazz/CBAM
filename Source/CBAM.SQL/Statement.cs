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
   public interface StatementBuilder
   {

      void SetParameterObjectWithType( Int32 parameterIndex, Object value, Type clrType );

      Int32 SQLParameterCount { get; }

      Int32 BatchParameterCount { get; }

      StatementParameter GetParameterInfo( Int32 parameterIndex );

      StatementParameter GetBatchParameterInfo( Int32 batchIndex, Int32 parameterIndex );

      String SQL { get; }

      void AddBatch();

   }

   public interface StatementParameter // : IEquatable<StatementParameter>
   {
      Type ParameterCILType { get; }
      Object ParameterValue { get; }
   }
}

public static partial class E_CBAM
{
   // TODO no BigInteger in PCL for .NET 4...

   public static void SetParameterBoolean( this StatementBuilder stmt, Int32 parameterIndex, Boolean? value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( Boolean ) );
   }

   public static void SetParameterInt32( this StatementBuilder stmt, Int32 parameterIndex, Int32? value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( Int32 ) );
   }

   public static void SetParameterInt16( this StatementBuilder stmt, Int32 parameterIndex, Int16? value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( Int16 ) );
   }

   public static void SetParameterInt64( this StatementBuilder stmt, Int32 parameterIndex, Int64? value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( Int64 ) );
   }

   public static void SetParameterDate( this StatementBuilder stmt, Int32 parameterIndex, DateTime? value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( DateTime ) );
   }

   public static void SetParameterTimestamp( this StatementBuilder stmt, Int32 parameterIndex, DateTime? value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( DateTime ) );
   }

   public static void SetParameterString( this StatementBuilder stmt, Int32 parameterIndex, String value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( String ) );
   }

   public static void SetParameterArray<TElement>( this StatementBuilder stmt, Int32 parameterIndex, Array value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( TElement[] ) );
   }

   public static void SetParameterObject<T>( this StatementBuilder stmt, Int32 parameterIndex, Object value )
   {
      stmt.SetParameterObjectWithType( parameterIndex, value, typeof( T ) );
   }

   public static void SetNull( this StatementBuilder stmt, Int32 parameterIndex, Type clrType )
   {
      stmt.SetParameterObjectWithType( parameterIndex, null, clrType );
   }

   public static Boolean IsSimple( this StatementBuilder stmt )
   {
      return !stmt.HasBatchParameters() && stmt.SQLParameterCount == 0;
   }

   public static Boolean HasBatchParameters( this StatementBuilder stmt )
   {
      return ( stmt?.BatchParameterCount ?? 0 ) > 0;
   }

   public static IEnumerable<StatementParameter> GetParameterEnumerable( this StatementBuilder stmt )
   {
      var max = stmt.SQLParameterCount;
      for ( var i = 0; i < max; ++i )
      {
         yield return stmt.GetParameterInfo( i );
      }
   }

   public static IEnumerable<StatementParameter> GetParameterEnumerable( this StatementBuilder stmt, Int32 batchIndex )
   {
      var max = stmt.SQLParameterCount;
      for ( var i = 0; i < max; ++i )
      {
         yield return stmt.GetBatchParameterInfo( batchIndex, i );
      }
   }

   // TODO move to utilpack

   /// <summary>
   /// Checks whether <paramref name="type"/> is not <c>null</c> and is vector array type.
   /// </summary>
   /// <param name="type">The type to check.</param>
   /// <returns><c>true</c> if <paramref name="type"/> is not <c>null</c> and is vector array type; <c>false</c> otherwise.</returns>
   public static Boolean IsVectorArray( this Type type )
   {
      return type != null && type.IsArray && type.Name.EndsWith( "[]" );
   }

   /// <summary>
   /// Checks whether <paramref name="type"/> is not <c>null</c> and is multi-dimensional array type.
   /// </summary>
   /// <param name="type">The type to check.</param>
   /// <returns><c>true</c> if <paramref name="type"/> is not <c>null</c> and is multi-dimensional array type; <c>false</c> otherwise.</returns>
   /// <remarks>
   /// This method bridges the gap in native Reflection API which doesn't offer a way to properly detect single-rank "multidimensional" array.
   /// This method detects such array by checking whether second-to-last character is something else than <c>[</c>.
   /// Multidimensional arrays with rank greater than <c>1</c> will have a number there, and "multidimensional" array with rank <c>1</c> will have character <c>*</c> there.
   /// </remarks>
   public static Boolean IsMultiDimensionalArray( this Type type )
   {
      String name;
      return type != null && type.IsArray && ( type.GetArrayRank() > 1 || ( ( name = type.Name ).EndsWith( "]" ) && name[name.Length - 2] != '[' ) );
   }

   //public static IEnumerable<DataRow> ExecuteQuery( this Statement stmt )
   //{
   //   return new DataRowEnumerable( stmt );
   //}

   //public static ResultSet ExecuteResultSet( this Statement stmt, CancellationToken token = default( CancellationToken ) )
   //{
   //   return stmt.Execute( token ).ResultSet;
   //}


}


