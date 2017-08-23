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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;

using BackendSizeInfo = System.ValueTuple<System.Int32, System.Object>;

namespace CBAM.SQL.PostgreSQL.JSON
{
   internal class DefaultPgSQLJSONTypeFunctionality : AbstractPgSQLTypeFunctionality
   {
      public static readonly PgSQLTypeFunctionality Instance = new DefaultPgSQLJSONTypeFunctionality();


      public override Boolean SupportsReadingBinaryFormat => false;

      public override Boolean SupportsWritingBinaryFormat => false;

      public override Object ChangeTypeFrameworkToPgSQL( Object obj )
      {
         // JToken is abstract class, so we will enter here always
         return obj is JToken ? obj : throw new InvalidCastException( $"The object must be descendant of {typeof( JToken ).FullName}." );
      }

      public override Object ChangeTypePgSQLToFramework( PgSQLTypeDatabaseData boundData, Object obj, Type typeTo )
      {
         throw new NotSupportedException();
      }

      public override BackendSizeInfo GetBackendBinarySize( PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value )
      {
         throw new NotSupportedException();
      }

      public override BackendSizeInfo GetBackendTextSize( PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value, Boolean isArrayElement )
      {
         return (helper.Encoding.CalculateJTokenTextSize( (JToken) value ), null);
      }

      public override async ValueTask<Object> ReadBackendValueAsync(
         DataFormat dataFormat,
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamReaderWithResizableBufferAndLimitedSize stream
         )
      {
         // No truly async support for deserializing JTokens in Newtonsoft, at least yet, so let's do it ourselves
         switch ( dataFormat )
         {
            case DataFormat.Text:

               var boundReader = ReaderFactory.NewNullableMemorizingValueReader(
                     helper.CharacterReader,
                     stream
                     );
               // Allow underlying stream buffer to become roughly max 1024 bytes length
               using ( boundReader.ClearStreamWhenStreamBufferTooBig( stream, 1024 ) )
               {
                  return await UtilPack.JSON.JTokenStreamReader.Instance.TryReadNextAsync( boundReader );
               }
            case DataFormat.Binary:
               throw new InvalidOperationException( "This data format is not supported" );
            default:
               throw new NotSupportedException( $"Unrecognized data format: ${dataFormat}." );
         }
      }

      public override async Task WriteBackendValueAsync(
         DataFormat dataFormat,
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamWriterWithResizableBufferAndLimitedSize stream,
         Object value,
         BackendSizeInfo additionalInfoFromSize,
         Boolean isArrayElement
         )
      {
         // No truly async support for serializing JTokens in Newtonsoft, at least yet, so let's do it ourselves
         switch ( dataFormat )
         {
            case DataFormat.Text:
               var bytesWritten = await helper.CharacterWriter.CreateJTokenWriter( stream ).TryWriteAsync( (JToken) value );
               break;
            case DataFormat.Binary:
               throw new InvalidOperationException( "This data format is not supported" );
            default:
               throw new NotSupportedException( $"Unrecognized data format: ${dataFormat}." );
         }
      }



   }
}