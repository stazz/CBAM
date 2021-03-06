﻿/*
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
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using TStaticTypeCacheValue = System.Collections.Generic.IDictionary<System.String, CBAM.SQL.PostgreSQL.PgSQLTypeDatabaseData>;
using TStaticTypeCache = System.Collections.Generic.IDictionary<System.Version, System.Collections.Generic.IDictionary<System.String, CBAM.SQL.PostgreSQL.PgSQLTypeDatabaseData>>;
using TextBackendSizeInfo = UtilPack.EitherOr<System.ValueTuple<System.Int32, System.Object>, System.String>;
using TypeFunctionalityInfo = System.ValueTuple<System.ValueTuple<System.Type, CBAM.SQL.PostgreSQL.PgSQLTypeFunctionality>, System.Boolean>;
using CBAM.SQL.PostgreSQL;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   using TSyncTextualSizeInfo = ValueTuple<Int32, String>;

   internal sealed partial class PostgreSQLProtocol
   {
      private static readonly TStaticTypeCache _StaticTypeCache;
      private static readonly IDictionary<String, TypeFunctionalityInfo> _DefaultTypes;

      static PostgreSQLProtocol()
      {
         var typeInfo = PostgreSQLProtocol.CreateDefaultTypesInfos()
            .ToDictionary( tInfo => tInfo.Item1, tInfo => tInfo );

         _DefaultTypes = new Dictionary<String, TypeFunctionalityInfo>()
         {
            { "text", (typeInfo[typeof(String)], true) },
            { "void", (typeInfo[typeof(String)], false) },
            { "char", (typeInfo[typeof(String)], false) },
            { "varchar", (typeInfo[typeof(String)], false) },
            { "bool", (typeInfo[typeof(Boolean)], true) },
            { "int2", (typeInfo[typeof(Int16)], true) },
            { "int4", (typeInfo[typeof(Int32)], true) },
            { "int8", (typeInfo[typeof(Int64)], true) },
            { "oid", (typeInfo[typeof(Int32)], false) },
            { "float4", (typeInfo[typeof(Single)], true) },
            { "float8", (typeInfo[typeof(Double)], true) },
            { "numeric", (typeInfo[typeof(Decimal)], true) },
            //{ "inet", typeof(PgSQLInternetAddress) },
            //{ "macaddr", typeof(PgSQLMacAddress) },
            { "money", (typeInfo[typeof(Decimal)], false) },
            { "uuid", (typeInfo[typeof(Guid)], true) },
            { "xml", (typeInfo[typeof(System.Xml.Linq.XElement)], true) },
            { "interval", (typeInfo[typeof(PgSQLInterval)], true) },
            { "date", (typeInfo[typeof(PgSQLDate)], true) },
            { "time", (typeInfo[typeof(PgSQLTime)], true) },
            { "timetz", (typeInfo[typeof(PgSQLTimeTZ)], true) },
            { "timestamp", (typeInfo[typeof(PgSQLTimestamp)], true) },
            { "timestamptz", (typeInfo[typeof(PgSQLTimestampTZ)], true) },
            { "abstime", (typeInfo[typeof(PgSQLTimestampTZ)], false) },
            { "bytea", (typeInfo[typeof(Byte[])], true) }

         };
         _StaticTypeCache = new Dictionary<Version, TStaticTypeCacheValue>();
      }

      private async Task ReadTypesFromServer( Boolean force, CancellationToken token )
      {
         var serverVersion = this._serverVersion;
         var typeCache = _StaticTypeCache;
         if ( force || !typeCache.TryGetValue( serverVersion, out TStaticTypeCacheValue types ) )
         {
            this.CurrentCancellationToken = token;
            try
            {
               types = await this.TypeRegistry.ReadTypeDataFromServer( _DefaultTypes.Keys );
            }
            finally
            {
               this.ResetCancellationToken();
            }

            lock ( typeCache )
            {
               typeCache[serverVersion] = types;
            }
         }

         this.TypeRegistry.AssignTypeData(
            types,
            tName => _DefaultTypes[tName].Item1.Item1,
            tuple =>
            {
               var valTuple = _DefaultTypes[tuple.DBTypeName];
               return new TypeFunctionalityCreationResult( valTuple.Item1.Item2, valTuple.Item2 );
            } );
      }

      private static IEnumerable<(Type, PgSQLTypeFunctionality)> CreateDefaultTypesInfos()
      {
         // Assumes that string is non-empty
         Boolean ArrayElementStringNeedsQuotesSingleDelimiter( PgSQLTypeDatabaseData boundData, String value )
         {
            Boolean needsQuotes = false;
            var delimChar = boundData.ArrayDelimiter[0];
            for ( var i = 0; i < value.Length && !needsQuotes; ++i )
            {
               var c = value[i];
               switch ( c )
               {
                  case (Char) PgSQLTypeFunctionalityForArrays.ARRAY_START:
                  case (Char) PgSQLTypeFunctionalityForArrays.ARRAY_END:
                  case PgSQLTypeFunctionalityForArrays.ESCAPE:
                  case PgSQLTypeFunctionalityForArrays.QUOTE:
                     needsQuotes = true;
                     break;
                  default:
                     if ( c == delimChar || Char.IsWhiteSpace( c ) )
                     {
                        needsQuotes = true;
                     }
                     break;
               }
            }

            return needsQuotes;
         }

         Boolean ArrayElementStringNeedsQuotesMultiDelimiter( PgSQLTypeDatabaseData boundData, String value )
         {
            var needsQuotes = value.IndexOf( boundData.ArrayDelimiter ) >= 0;
            if ( !needsQuotes )
            {
               for ( var i = 0; i < value.Length && !needsQuotes; ++i )
               {
                  var c = value[i];
                  switch ( c )
                  {
                     case (Char) PgSQLTypeFunctionalityForArrays.ARRAY_START:
                     case (Char) PgSQLTypeFunctionalityForArrays.ARRAY_END:
                     case PgSQLTypeFunctionalityForArrays.ESCAPE:
                     case PgSQLTypeFunctionalityForArrays.QUOTE:
                        needsQuotes = true;
                        break;
                     default:
                        if ( Char.IsWhiteSpace( c ) )
                        {
                           needsQuotes = true;
                        }
                        break;
                  }
               }
            }
            return needsQuotes;
         }

         Int32 CalculateArrayElementStringSizeSingleDelimiter( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, String value )
         {
            var asciiByteSize = encoding.BytesPerASCIICharacter;
            var retVal = 2 * asciiByteSize + encoding.Encoding.GetByteCount( value );
            var delimChar = boundData.ArrayDelimiter[0];
            for ( var i = 0; i < value.Length; ++i )
            {
               var c = value[i];
               switch ( c )
               {
                  //case (Char) PgSQLTypeFunctionalityForArrays.ARRAY_START:
                  //case (Char) PgSQLTypeFunctionalityForArrays.ARRAY_END:
                  case PgSQLTypeFunctionalityForArrays.ESCAPE:
                  case PgSQLTypeFunctionalityForArrays.QUOTE:
                     retVal += asciiByteSize;
                     break;
                     //default:
                     //   if ( delimChar == c || Char.IsWhiteSpace( c ) )
                     //   {
                     //      retVal += asciiByteSize;
                     //   }
                     //   break;
               }
            }
            return retVal;
         }

         Int32 CalculateArrayElementStringSizeMultiDelimiter( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, String value )
         {
            var asciiByteSize = encoding.BytesPerASCIICharacter;
            var retVal = 2 * asciiByteSize + encoding.Encoding.GetByteCount( value ) + asciiByteSize * value.CountOccurrances( boundData.ArrayDelimiter, StringComparison.Ordinal );
            for ( var i = 0; i < value.Length; ++i )
            {
               var c = value[i];
               switch ( c )
               {
                  //case (Char) PgSQLTypeFunctionalityForArrays.ARRAY_START:
                  //case (Char) PgSQLTypeFunctionalityForArrays.ARRAY_END:
                  case PgSQLTypeFunctionalityForArrays.ESCAPE:
                  case PgSQLTypeFunctionalityForArrays.QUOTE:
                     retVal += asciiByteSize;
                     break;
                     //default:
                     //   if ( Char.IsWhiteSpace( c ) )
                     //   {
                     //      retVal += asciiByteSize;
                     //   }
                     //   break;
               }
            }
            return retVal;
         }

         void WriteArrayElementStringSingleDelimiter( PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Byte[] array, String value, ref Int32 offset )
         {
            var prevIdx = 0;
            var delimChar = boundData.ArrayDelimiter[0];
            var encoding = helper.Encoding.Encoding;
            for ( var i = 0; i < value.Length; ++i )
            {
               var c = value[i];
               switch ( c )
               {
                  //case (Char) PgSQLTypeFunctionalityForArrays.ARRAY_START:
                  //case (Char) PgSQLTypeFunctionalityForArrays.ARRAY_END:
                  case PgSQLTypeFunctionalityForArrays.ESCAPE:
                  case PgSQLTypeFunctionalityForArrays.QUOTE:
                     offset += encoding.GetBytes( value, prevIdx, i - prevIdx, array, offset );
                     helper.Encoding.WriteASCIIByte( array, ref offset, (Byte) PgSQLTypeFunctionalityForArrays.ESCAPE );
                     prevIdx = i;
                     break;
                  default:
                     //if ( delimChar == c || Char.IsWhiteSpace( c ) )
                     //{
                     //   arrayIdx += encoding.GetBytes( value, prevIdx, i - prevIdx, array, arrayIdx );
                     //   helper.Encoding.WriteASCIIByte( array, ref arrayIdx, (Byte) PgSQLTypeFunctionalityForArrays.ESCAPE );
                     //   prevIdx = i;
                     //}
                     break;
               }
            }

            // Last chunk
            offset += encoding.GetBytes( value, prevIdx, value.Length - prevIdx, array, offset );
         }

         void WriteArrayElementStringMultiDelimiter( PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Byte[] array, String value, ref Int32 offset )
         {
            var prevIdx = 0;
            var encoding = helper.Encoding.Encoding;
            var delimCharStart = boundData.ArrayDelimiter[0];
            var delimCharEnd = boundData.ArrayDelimiter[1];
            for ( var i = 0; i < value.Length; ++i )
            {
               var c = value[i];
               switch ( c )
               {
                  //case (Char) PgSQLTypeFunctionalityForArrays.ARRAY_START:
                  //case (Char) PgSQLTypeFunctionalityForArrays.ARRAY_END:
                  case PgSQLTypeFunctionalityForArrays.ESCAPE:
                  case PgSQLTypeFunctionalityForArrays.QUOTE:
                     offset += encoding.GetBytes( value, prevIdx, i - prevIdx, array, offset );
                     helper.Encoding.WriteASCIIByte( array, ref offset, (Byte) PgSQLTypeFunctionalityForArrays.ESCAPE );
                     prevIdx = i;
                     break;
                     //default:
                     //   var isDelimStart = c == delimCharStart && i < value.Length - 1 && value[i + 1] == delimCharEnd;
                     //   if ( isDelimStart || Char.IsWhiteSpace( c ) )
                     //   {
                     //      arrayIdx += encoding.GetBytes( value, prevIdx, i - prevIdx, array, arrayIdx );
                     //      helper.Encoding.WriteASCIIByte( array, ref arrayIdx, (Byte) PgSQLTypeFunctionalityForArrays.ESCAPE );
                     //      prevIdx = i;
                     //      if ( isDelimStart )
                     //      {
                     //         ++i;
                     //      }
                     //   }
                     //   break;
               }
            }

            // Last chunk
            offset += encoding.GetBytes( value, prevIdx, value.Length - prevIdx, array, offset );
         }


         // String
         yield return CreateSingleBodyInfoWithType(
         ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return args.GetStringWithPool( array, offset, count );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return args.GetStringWithPool( array, offset, count );
            },
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, String value, Boolean isArrayElement ) =>
            {
               EitherOr<Int32, String> retVal;
               if ( isArrayElement )
               {
                  // From https://www.postgresql.org/docs/current/static/arrays.html :
                  // The array output routine will put double quotes around element values if they are empty strings, contain curly braces, delimiter characters, double quotes, backslashes, or white space, or match the word NULL.
                  // Assumes that value is non-empty
                  var isNullString = String.Equals( value, "null", StringComparison.OrdinalIgnoreCase );
                  var isSingleDelimiter = boundData.ArrayDelimiter.Length == 1;
                  if (
                     value.Length == 0
                     || isNullString
                     || ( isSingleDelimiter ? ArrayElementStringNeedsQuotesSingleDelimiter( boundData, value ) : ArrayElementStringNeedsQuotesMultiDelimiter( boundData, value ) )
                  )
                  {
                     retVal = isNullString ?
                        6 * encoding.BytesPerASCIICharacter :
                        ( isSingleDelimiter ?
                           CalculateArrayElementStringSizeSingleDelimiter( boundData, encoding, value ) :
                           CalculateArrayElementStringSizeMultiDelimiter( boundData, encoding, value )
                        );
                  }
                  else
                  {
                     // We can serialize without quotes
                     retVal = value;
                  }

               }
               else
               {
                  retVal = value;
               }

               return retVal;
            },
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, String value, Boolean isArrayElement ) =>
            {
               return encoding.Encoding.GetByteCount( value );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, String value, TSyncTextualSizeInfo sizeInfo, Boolean isArrayElement ) =>
            {
               if ( sizeInfo.Item2 == null )
               {
                  // Because of how text format size is calculated, and because we are handling string type, we will come here *only* when we are serializing array element, *and* it has data to be escaped.

                  // Write starting quote
                  args.Encoding.WriteASCIIByte( array, ref offset, (Byte) PgSQLTypeFunctionalityForArrays.QUOTE );

                  // Write content, and escape any double quotes and backslashes
                  if ( boundData.ArrayDelimiter.Length == 1 )
                  {
                     WriteArrayElementStringSingleDelimiter( boundData, args, array, value, ref offset );
                  }
                  else
                  {
                     WriteArrayElementStringMultiDelimiter( boundData, args, array, value, ref offset );
                  }

                  // Write ending quote
                  args.Encoding.WriteASCIIByte( array, ref offset, (Byte) PgSQLTypeFunctionalityForArrays.QUOTE );
               }
               else
               {
                  offset += args.Encoding.Encoding.GetBytes( value, 0, value.Length, array, offset );
               }
               System.Diagnostics.Debug.Assert( offset == sizeInfo.Item1 );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, String value, Int32 sizeInfo, Boolean isArrayElement ) =>
            {
               args.Encoding.Encoding.GetBytes( value, 0, value.Length, array, offset );
            },
            null,
            null
            );

         // Boolean
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               Byte b;
               return count > 0
                  && (
                     ( b = args.Encoding.ReadASCIIByte( array, ref offset ) ) == 'T'
                     || b == 't'
                     );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return count > 0 && array[offset] != 0;
            },
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, Boolean value, Boolean isArrayElement ) =>
            {
               return encoding.BytesPerASCIICharacter;
            },
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, Boolean value, Boolean isArrayElement ) =>
            {
               return 1;
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Boolean value, TSyncTextualSizeInfo sizeInfo, Boolean isArrayElement ) =>
            {
               args.Encoding.WriteASCIIByte( array, ref offset, (Byte) ( value ? 't' : 'f' ) );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Boolean value, Int32 sizeInfo, Boolean isArrayElement ) =>
            {
               array[offset] = (Byte) ( value ? 1 : 0 );
            },
            null,
            null
            );

         // Int16
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return (Int16) args.Encoding.ParseInt32Textual( array, ref offset, (count / args.Encoding.BytesPerASCIICharacter, true) );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return array.ReadInt16BEFromBytes( ref offset );
            },
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, Int16 value, Boolean isArrayElement ) =>
            {
               return encoding.GetTextualIntegerRepresentationSize( value );
            },
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, Int16 value, Boolean isArrayElement ) =>
            {
               return sizeof( Int16 );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int16 value, TSyncTextualSizeInfo sizeInfo, Boolean isArrayElement ) =>
            {
               args.Encoding.WriteIntegerTextual( array, ref offset, value );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int16 value, Int32 sizeInfo, Boolean isArrayElement ) =>
            {
               array.WriteInt16BEToBytes( ref offset, value );
            },
            null,
            null
            );

         // Int32
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return args.Encoding.ParseInt32Textual( array, ref offset, (count / args.Encoding.BytesPerASCIICharacter, true) );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return array.ReadInt32BEFromBytes( ref offset );
            },
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, Int32 value, Boolean isArrayElement ) =>
            {
               return encoding.GetTextualIntegerRepresentationSize( value );
            },
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, Int32 value, Boolean isArrayElement ) =>
            {
               return sizeof( Int32 );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 value, TSyncTextualSizeInfo sizeInfo, Boolean isArrayElement ) =>
            {
               args.Encoding.WriteIntegerTextual( array, ref offset, value );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 value, Int32 sizeInfo, Boolean isArrayElement ) =>
            {
               array.WriteInt32BEToBytes( ref offset, value );
            },
            null,
            null
            );

         // Int64
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return args.Encoding.ParseInt64Textual( array, ref offset, (count / args.Encoding.BytesPerASCIICharacter, true) );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return array.ReadInt64BEFromBytes( ref offset );
            },
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, Int64 value, Boolean isArrayElement ) =>
            {
               return encoding.GetTextualIntegerRepresentationSize( value );
            },
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, Int64 value, Boolean isArrayElement ) =>
            {
               return sizeof( Int64 );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int64 value, TSyncTextualSizeInfo sizeInfo, Boolean isArrayElement ) =>
            {
               args.Encoding.WriteIntegerTextual( array, ref offset, value );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int64 value, Int32 sizeInfo, Boolean isArrayElement ) =>
            {
               array.WriteInt64BEToBytes( ref offset, value );
            },
            null,
            null
            );

         // Unfortunately, parsing real numbers (single, double, decimal) directly from byte array containing the string is quite a bit more complicated than integers,
         // so right now we have to allocate a new string and parse it.
         // It's a bit of a performance killer, so maybe should be fixed one day.
         // Single
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return Single.Parse( args.GetStringWithPool( array, offset, count ), CommonPgSQLTypeFunctionalityInfo.NumberFormat );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return array.ReadSingleBEFromBytes( ref offset );
            },
            null,
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, Single value, Boolean isArrayElement ) =>
            {
               return sizeof( Single );
            },
            null,
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Single value, Int32 sizeInfo, Boolean isArrayElement ) =>
            {
               array.WriteSingleBEToBytes( ref offset, value );
            },
            null,
            null
            );

         // Double
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return Double.Parse( args.GetStringWithPool( array, offset, count ), CommonPgSQLTypeFunctionalityInfo.NumberFormat );
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return array.ReadDoubleBEFromBytes( ref offset );
            },
            null,
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, Double value, Boolean isArrayElement ) =>
            {
               return sizeof( Double );
            },
            null,
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Double value, Int32 sizeInfo, Boolean isArrayElement ) =>
            {
               array.WriteDoubleBEToBytes( ref offset, value );
            },
            null,
            null
            );

         // Decimal
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return Decimal.Parse( args.GetStringWithPool( array, offset, count ), CommonPgSQLTypeFunctionalityInfo.NumberFormat );
            },
            null,
            null,
            null,
            null,
            null,
            null,
            null
            );

         // Guid
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return Guid.Parse( args.GetStringWithPool( array, offset, count ) );
            },
            null,
            null,
            null,
            null,
            null,
            null,
            null
            );

         // Xml
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               // If there would be "LoadAsync" in XEelement, we could use new PgSQLTypeUnboundInfo constructor directly.
               using ( var mStream = new System.IO.MemoryStream( array, offset, count, false ) )
               {
                  return System.Xml.Linq.XElement.Load( mStream );
               }
            },
            null,
            null,
            null,
            null,
            null,
            null,
            null
            );

         // PgSQLInterval
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               // TODO interval parsing without string allocation (PgSQLInterval.Load(Stream stream))
               // Or PgSQLInterval.ParseBinaryText
               return PgSQLInterval.Parse( args.GetStringWithPool( array, offset, count ) );
            },
            null,
            // TODO interval string writing without string allocation
            null,
            null,
            null,
            null,
            ( PgSQLTypeDatabaseData dbData, PgSQLInterval pgSQLObject, Type targetType ) =>
            {
               return (TimeSpan) pgSQLObject;
            },
            ( PgSQLTypeDatabaseData dbData, Object systemObject ) =>
            {
               return (TimeSpan) systemObject;
            }
            );

         // PgSQLDate
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return PgSQLDate.ParseBinaryText( args.Encoding, array, ref offset, count );
            },
            null,
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, PgSQLDate value, Boolean isArrayElement ) =>
            {
               var retVal = value.GetTextByteCount( encoding );
               if ( isArrayElement && value.NeedsQuotingInArrayElement() )
               {
                  // The " BC" substring contains whitespace, so we must put quotes around this
                  retVal += 2 * encoding.BytesPerASCIICharacter;
               }
               return retVal;
            },
            null,
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, PgSQLDate value, TSyncTextualSizeInfo sizeInfo, Boolean isArrayElement ) =>
            {
               value.WriteBytesAndPossiblyQuote( args.Encoding, array, ref offset, isArrayElement && value.NeedsQuotingInArrayElement(), ( PgSQLDate v, IEncodingInfo e, Byte[] a, ref Int32 i ) => v.WriteTextBytes( e, a, ref i ) );
            },
            null,
            ( PgSQLTypeDatabaseData dbData, PgSQLDate pgSQLObject, Type targetType ) =>
            {
               return (DateTime) pgSQLObject;
            },
            ( PgSQLTypeDatabaseData dbData, Object systemObject ) =>
            {
               return (PgSQLDate) (DateTime) systemObject;
            }
            );

         // PgSQLTime
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return PgSQLTime.ParseBinaryText( args.Encoding, array, ref offset, count );
            },
            null,
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, PgSQLTime value, Boolean isArrayElement ) =>
            {
               return value.GetTextByteCount( encoding );
            },
            null,
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, PgSQLTime value, TSyncTextualSizeInfo sizeInfo, Boolean isArrayElement ) =>
            {
               value.WriteTextBytes( args.Encoding, array, ref offset );
            },
            null,
            ( PgSQLTypeDatabaseData dbData, PgSQLTime pgSQLObject, Type targetType ) =>
            {
               if ( typeof( DateTime ).Equals( targetType ) )
               {
                  return (DateTime) pgSQLObject;
               }
               else if ( typeof( TimeSpan ).Equals( targetType ) )
               {
                  return (TimeSpan) pgSQLObject;
               }
               else if ( typeof( PgSQLInterval ).Equals( targetType ) )
               {
                  return (PgSQLInterval) pgSQLObject;
               }
               else
               {
                  throw new InvalidCastException( "Can't cast time " + pgSQLObject + " to " + targetType + "." );
               }
            },
            ( PgSQLTypeDatabaseData dbData, Object systemObject ) =>
            {
               var tt = systemObject.GetType();
               if ( typeof( DateTime ).Equals( tt ) )
               {
                  return (PgSQLTime) (DateTime) systemObject;
               }
               else if ( typeof( TimeSpan ).Equals( tt ) )
               {
                  return (PgSQLTime) (TimeSpan) systemObject;
               }
               else if ( typeof( PgSQLInterval ).Equals( tt ) )
               {
                  return (PgSQLTime) (PgSQLInterval) systemObject;
               }
               else
               {
                  throw new InvalidCastException( "Can't cast object " + systemObject + " to time." );
               }
            }
            );

         // PgSQLTimeTZ
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return PgSQLTimeTZ.ParseBinaryText( args.Encoding, array, ref offset, count );
            },
            null,
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, PgSQLTimeTZ value, Boolean isArrayElement ) =>
            {
               return value.GetTextByteCount( encoding );
            },
            null,
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, PgSQLTimeTZ value, TSyncTextualSizeInfo sizeInfo, Boolean isArrayElement ) =>
            {
               value.WriteTextBytes( args.Encoding, array, ref offset );
            },
            null,
            ( PgSQLTypeDatabaseData dbData, PgSQLTimeTZ pgSQLObject, Type targetType ) =>
            {
               if ( typeof( DateTime ).Equals( targetType ) )
               {
                  return (DateTime) pgSQLObject;
               }
               else if ( typeof( TimeSpan ).Equals( targetType ) )
               {
                  return (TimeSpan) pgSQLObject;
               }
               else
               {
                  throw new InvalidCastException( "Can't cast time " + pgSQLObject + " to " + targetType + "." );
               }
            },
            ( PgSQLTypeDatabaseData dbData, Object systemObject ) =>
            {
               var tt = systemObject.GetType();
               if ( typeof( DateTime ).Equals( tt ) )
               {
                  return (PgSQLTimeTZ) (DateTime) systemObject;
               }
               else if ( typeof( TimeSpan ).Equals( tt ) )
               {
                  return (PgSQLTimeTZ) (TimeSpan) systemObject;
               }
               else
               {
                  throw new InvalidCastException( "Can't cast object " + systemObject + " to time with time zone." );
               }
            }
            );

         // PgSQLTimestamp
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return PgSQLTimestamp.ParseBinaryText( args.Encoding, array, ref offset, count );
            },
            null,
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, PgSQLTimestamp value, Boolean isArrayElement ) =>
            {
               var retVal = value.GetTextByteCount( encoding );
               if ( isArrayElement && value.NeedsQuotingInArrayElement() )
               {
                  // There will be a space -> the value must be quoted
                  retVal += 2 * encoding.BytesPerASCIICharacter;
               }
               return retVal;
            },
            null,
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, PgSQLTimestamp value, TSyncTextualSizeInfo sizeInfo, Boolean isArrayElement ) =>
            {
               value.WriteBytesAndPossiblyQuote( args.Encoding, array, ref offset, isArrayElement && value.NeedsQuotingInArrayElement(), ( PgSQLTimestamp v, IEncodingInfo e, Byte[] a, ref Int32 i ) => v.WriteTextBytes( e, a, ref i ) );
            },
            null,
            ( PgSQLTypeDatabaseData dbData, PgSQLTimestamp pgSQLObject, Type targetType ) =>
            {
               if ( typeof( DateTime ).Equals( targetType ) )
               {
                  return (DateTime) pgSQLObject;
               }
               else
               {
                  throw new InvalidCastException( "Can't cast time " + pgSQLObject + " to " + targetType + "." );
               }
            },
            ( PgSQLTypeDatabaseData dbData, Object systemObject ) =>
            {
               var tt = systemObject.GetType();
               if ( typeof( DateTime ).Equals( tt ) )
               {
                  return (PgSQLTimestamp) (DateTime) systemObject;
               }
               else
               {
                  throw new InvalidCastException( "Can't cast object " + systemObject + " to timestamp." );
               }
            }
            );

         // PgSQLTimestampTZ
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               return PgSQLTimestampTZ.ParseBinaryText( args.Encoding, array, ref offset, count );
            },
            null,
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, PgSQLTimestampTZ value, Boolean isArrayElement ) =>
            {
               var retVal = value.GetTextByteCount( encoding );
               if ( isArrayElement && value.NeedsQuotingInArrayElement() )
               {
                  retVal += 2 * encoding.BytesPerASCIICharacter;
               }
               return retVal;
            },
            null,
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, PgSQLTimestampTZ value, TSyncTextualSizeInfo sizeInfo, Boolean isArrayElement ) =>
            {
               value.WriteBytesAndPossiblyQuote( args.Encoding, array, ref offset, isArrayElement && value.NeedsQuotingInArrayElement(), ( PgSQLTimestampTZ v, IEncodingInfo e, Byte[] a, ref Int32 i ) => v.WriteTextBytes( e, a, ref i ) );
            },
            null,
            ( PgSQLTypeDatabaseData dbData, PgSQLTimestampTZ pgSQLObject, Type targetType ) =>
            {
               if ( typeof( DateTime ).Equals( targetType ) )
               {
                  return (DateTime) pgSQLObject;
               }
               else if ( typeof( DateTimeOffset ).Equals( targetType ) )
               {
                  return (DateTimeOffset) pgSQLObject;
               }
               else
               {
                  throw new InvalidCastException( "Can't cast time " + pgSQLObject + " to " + targetType + "." );
               }
            },
            ( PgSQLTypeDatabaseData dbData, Object systemObject ) =>
            {
               var tt = systemObject.GetType();
               if ( typeof( DateTime ).Equals( tt ) )
               {
                  return (PgSQLTimestampTZ) (DateTime) systemObject;
               }
               else if ( typeof( DateTimeOffset ).Equals( tt ) )
               {
                  return (PgSQLTimestampTZ) (DateTimeOffset) systemObject;
               }
               else
               {
                  throw new InvalidCastException( "Can't cast object " + systemObject + " to timestamp with time zone." );
               }
            }
            );

         // Byte[]
         yield return CreateSingleBodyInfoWithType(
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               // Byte[] textual format is "\x<hex decimal pairs>"
               var encoding = args.Encoding;
               Byte[] retVal;
               if ( count > 2
               && encoding.ReadASCIIByte( array, ref offset ) == '\\'
               && encoding.ReadASCIIByte( array, ref offset ) == 'x'
               )
               {
                  var len = ( count - offset ) / encoding.BytesPerASCIICharacter / 2;
                  retVal = new Byte[len];
                  for ( var i = 0; i < len; ++i )
                  {
                     retVal[i] = encoding.ReadHexDecimal( array, ref offset );
                  }
               }
               else
               {
                  throw new PgSQLException( "Bytea strings must start with \"\\x\"." );
               }

               return retVal;
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count ) =>
            {
               // Binary format is just raw byte array
               var retVal = new Byte[count];
               Array.Copy( array, offset, retVal, 0, count );
               return retVal;
            },
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, Byte[] value, Boolean isArrayElement ) =>
            {
               // Text size is 2 ASCII bytes + 2 ASCII bytes per each actual byte
               var retVal = ( 2 + 2 * value.Length ) * encoding.BytesPerASCIICharacter;
               if ( isArrayElement )
               {
                  // Always need quotation since there is a '\\' character
                  retVal += 2 * encoding.BytesPerASCIICharacter;
               }
               return retVal;
            },
            ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, Byte[] value, Boolean isArrayElement ) =>
            {
               // Binary size is same as array size
               return value.Length;
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Byte[] value, TSyncTextualSizeInfo additionalInfoFromSize, Boolean isArrayElement ) =>
            {
               // Write all text to array
               var encoding = args.Encoding;
               if ( isArrayElement )
               {
                  encoding.WriteASCIIByte( array, ref offset, (Byte) PgSQLTypeFunctionalityForArrays.QUOTE );
               }

               encoding
                  .WriteASCIIByte( array, ref offset, (Byte) '\\' )
                                 .WriteASCIIByte( array, ref offset, (Byte) 'x' );
               foreach ( var b in value )
               {
                  encoding.WriteHexDecimal( array, ref offset, b );
               }
               if ( isArrayElement )
               {
                  encoding.WriteASCIIByte( array, ref offset, (Byte) PgSQLTypeFunctionalityForArrays.QUOTE );
               }
            },
            ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Byte[] value, Int32 additionalInfoFromSize, Boolean isArrayElement ) =>
            {
               // Just copy array
               Array.Copy( value, 0, array, offset, value.Length );
            },
            null,
            null
            );
      }

      private static (Type Type, DefaultPgSQLTypeFunctionality<TValue> Functionality) CreateSingleBodyInfoWithType<TValue>(
         ReadFromBackendSync<TValue> text2CLR,
         ReadFromBackendSync<TValue> binary2CLR,
         CalculateBackendSize<TValue, EitherOr<Int32, String>> textSize,
         CalculateBackendSize<TValue, Int32> clr2BinarySize,
         WriteToBackendSync<TValue, TSyncTextualSizeInfo> clr2Text,
         WriteToBackendSync<TValue, Int32> clr2Binary,
         ChangePgSQLToSystem<TValue> pgSQL2System,
         ChangeSystemToPgSQL<TValue> system2PgSQL
         )
      {
         return (typeof( TValue ), DefaultPgSQLTypeFunctionality<TValue>.CreateSingleBodyUnboundInfo( text2CLR, binary2CLR, textSize, clr2BinarySize, clr2Text, clr2Binary, pgSQL2System, system2PgSQL ));
      }
   }

}

public static partial class E_CBAM
{

   internal static Boolean NeedsQuotingInArrayElement( this PgSQLDate date )
   {
      // Values with year less than 0 will be prefixed with " BC" suffix, thus requiring quoting
      return date.Year < 0;
   }

   internal static Boolean NeedsQuotingInArrayElement( this PgSQLTimestamp timestamp )
   {
      // Anything other than "Infinity" and "-Infinity" will need quoting since there will be a space
      return timestamp.IsFinite;
   }

   internal static Boolean NeedsQuotingInArrayElement( this PgSQLTimestampTZ timestamp )
   {
      // Anything other than "Infinity" and "-Infinity" will need quoting since there will be a space
      return timestamp.IsFinite;
   }

   internal delegate void WriteTextBytes<T>( T value, IEncodingInfo encoding, Byte[] array, ref Int32 idx );

   internal static void WriteBytesAndPossiblyQuote<T>(
      this T value,
      IEncodingInfo encoding,
      Byte[] array,
      ref Int32 index,
      Boolean needsQuoting,
      WriteTextBytes<T> writeTextBytes
      )
   {
      if ( needsQuoting )
      {
         encoding.WriteASCIIByte( array, ref index, (Byte) '"' );
      }
      writeTextBytes( value, encoding, array, ref index );
      if ( needsQuoting )
      {
         encoding.WriteASCIIByte( array, ref index, (Byte) '"' );
      }
   }
}
