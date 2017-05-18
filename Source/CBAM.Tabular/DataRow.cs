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
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Reflection;
using UtilPack;
using CBAM.Tabular;

namespace CBAM.Tabular
{
   public interface DataRow
   {
      DataColumn GetColumn( Int32 index );

      DataRowMetaData Metadata { get; }
   }

   public interface DataRowMetaData
   {
      Int32 ColumnCount { get; }
      Int32 GetIndexFor( String columnName );
      DataColumnMetaData GetColumnMetaData( Int32 columnIndex );
   }

   public interface DataColumnMetaData
   {
      Type ColumnCLRType { get; }
      Object ChangeType( Object value, Type targetType );
      String Label { get; }
   }

   public interface DataColumn
   {
      // Will return "none" if read bytes has been started
      Task<ResultOrNone<Object>> TryGetValueAsync();

      // Will return -1 if value reading via GetValueAsync has started.
      // Will return null on concurrent read
      Task<Int32?> TryReadBytesAsync( Byte[] array, Int32 offset, Int32 count );

      // TODO maybe move to DataColumnMetaData ?
      Task<Object> ConvertFromBytesAsync( Stream stream, Int32 byteCount );

      DataColumnMetaData MetaData { get; }

      Int32 ColumnIndex { get; }

   }
}

public static partial class E_CBAM
{
   public static async Task<Object> GetValueAsync( this DataRow row, Int32 index, Type type )
   {
      return await row.GetColumn( index ).GetValueAsync( type );
   }

   public static async Task<T> GetValueAsync<T>( this DataRow row, Int32 index )
   {
      return (T) ( await row.GetValueAsync( index, typeof( T ) ) );
   }

   public static async Task<Object> GetValueAsObjectAsync( this DataRow row, Int32 index )
   {
      return await row.GetValueAsync( index, typeof( Object ) );
   }

   public static async Task<Object> GetValueAsync( this DataColumn column, Type type )
   {
      var retValOrNone = await column.TryGetValueAsync();

      Object retVal;
      if ( retValOrNone.HasResult )
      {
         retVal = retValOrNone.Result;
         if ( retVal != null && !type.GetTypeInfo().IsAssignableFrom( retVal.GetType().GetTypeInfo() ) )
         {
            retVal = column.MetaData.ChangeType( retVal, type );
         }
      }
      else
      {
         throw new InvalidOperationException( $"No value for index {column.ColumnIndex}." );
      }

      return retVal;
   }

   public static async Task<T> GetValueAsync<T>( this DataColumn column )
   {
      return (T) ( await column.GetValueAsync( typeof( T ) ) );
   }

   public static async Task<Object> GetValueAsObjectAsync( this DataColumn column )
   {
      return await column.GetValueAsync( typeof( Object ) );
   }

   public static async Task<T> GetValueByNameAsync<T>( this DataRow row, String name )
   {
      return await row.GetValueAsync<T>( row.Metadata.GetIndexFor( name ) );
   }


}