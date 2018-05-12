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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;


namespace CBAM.SQL.PostgreSQL.Implementation
{
   using TextSizeAdditionalInfo = ValueTuple<BackendSizeInfo[], ValueTuple<Int32[], Int32[], Int32>>;

   internal sealed class PgSQLTypeFunctionalityForArrays : PgSQLTypeFunctionality
   {
      internal const Byte ARRAY_START = (Byte) '{';
      internal const Byte ARRAY_END = (Byte) '}';
      internal const Char ESCAPE = '\\';
      internal const Char QUOTE = '"';
      internal const Byte ARRAY_LOBO_START = (Byte) '[';
      internal const Byte ARRAY_LOBO_END = (Byte) ']';
      internal const Byte ARRAY_LOBO_SPEC_SEPARATOR = (Byte) '=';
      internal const Char DIM_SEPARATOR = ':';
      internal const Int32 NULL_CHAR_COUNT = 4;
      internal const String NULL_STRING = "NULL";

      private readonly Array _emptyArray;
      private readonly Type _arrayElementType;
      private readonly Lazy<TypeFunctionalityInformation> _elementTypeInfo;

      public PgSQLTypeFunctionalityForArrays(
         TypeRegistry protocol,
         ref Type arrayElementType,
         Int32 elementTypeID
         )
      {
         this._elementTypeInfo = new Lazy<TypeFunctionalityInformation>( () => protocol.TryGetTypeInfo( elementTypeID ), LazyThreadSafetyMode.PublicationOnly );
         if ( arrayElementType.GetTypeInfo().IsValueType && !arrayElementType.IsNullable() )
         {
            // Allow nulls
            arrayElementType = typeof( Nullable<> ).MakeGenericType( arrayElementType );
         }

         // TODO maybe make CLRType getter throw exception? Since the actual type may be X[], X[,], X[,,], etc...?
         this._arrayElementType = arrayElementType;
         this._emptyArray = Array.CreateInstance( arrayElementType, 0 );
      }

      public Boolean SupportsReadingBinaryFormat => this._elementTypeInfo.Value.Functionality.SupportsReadingBinaryFormat;

      public Boolean SupportsWritingBinaryFormat => this._elementTypeInfo.Value.Functionality.SupportsWritingBinaryFormat;

      public Object ChangeTypeFrameworkToPgSQL( PgSQLTypeDatabaseData dbData, Object obj )
      {
         // We will enter here for multidimensional arrays, from BindMessage
         var objType = obj.GetType();
         return objType.IsArray
            && EqualsIgnoreNullability( objType.GetElementType(), this._arrayElementType ) ? // The WriteArrayText and WriteArrayBinary methods work for both X[] and X?[] array types.
               obj :
               throw new InvalidCastException( $"The object to cast must be single- or multidimensionsal array with element type of {this._arrayElementType.FullName}." );
      }

      private static Boolean EqualsIgnoreNullability( Type x, Type y )
      {
         return Equals( x, y )
            || Equals( GetActualIfNullable( x ), GetActualIfNullable( y ) );
      }

      private static Type GetActualIfNullable( Type type )
      {
         return type.IsNullable( out var actual ) ? actual : type;
      }

      public Object ChangeTypePgSQLToFramework( PgSQLTypeDatabaseData dbData, Object obj, Type typeTo )
      {
         // TODO cast all elements of array...?
         throw new InvalidCastException();
      }

      public BackendSizeInfo GetBackendSize( DataFormat dataFormat, PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value, Boolean isArrayElement )
      {
         switch ( dataFormat )
         {
            case DataFormat.Text:
               return this.GetBackendTextSize( boundData, helper, value );
            case DataFormat.Binary:
               return this.GetBackendBinarySize( boundData, helper, value );
            default:
               throw new NotSupportedException( $"Data format {dataFormat} is not recognized." );
         }
      }

      private BackendSizeInfo GetBackendBinarySize( PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value )
      {
         var array = (Array) value;
         BackendSizeInfo retVal;
         var arrayLength = array.Length;
         // The header size is three integers (rank, null map, element type id), and then two integers for each rank
         var size = sizeof( Int32 ) * 3;
         BackendSizeInfo[] elementSizes;
         if ( arrayLength > 0 )
         {
            size += array.Rank * 2 * sizeof( Int32 );
            elementSizes = new BackendSizeInfo[arrayLength];
            var i = 0;
            var elementInfo = this._elementTypeInfo.Value;
            foreach ( var elem in array )
            {
               var sizeInfo = elementInfo.Functionality.GetBackendSizeCheckNull( DataFormat.Binary, elementInfo.DatabaseData, helper, elem, true );
               elementSizes[i++] = sizeInfo;
               size += sizeof( Int32 );
               if ( sizeInfo.ByteCount > 0 )
               {
                  size += sizeInfo.ByteCount;
               }
            }
         }
         else
         {
            elementSizes = null;
         }
         retVal = new BackendSizeInfo( size, elementSizes );

         return retVal;
      }

      private BackendSizeInfo GetBackendTextSize( PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value )
      {
         var array = (Array) value;
         var helperEncoding = helper.Encoding;
         var encoding = helperEncoding.Encoding;
         var asciiSize = helperEncoding.BytesPerASCIICharacter;
         var length = array.Length;
         BackendSizeInfo retVal;
         if ( length <= 0 )
         {
            retVal = new BackendSizeInfo( 2 * asciiSize );
         }
         else
         {
            var rank = array.Rank;
            var bracesCount = 0; // Amount of array start/end braces
            var innermostLength = array.GetLength( rank - 1 );
            var delimitersCount = Math.Max( innermostLength - 1, 0 ); // Amount of delimiter characters
                                                                      // Iterate from second-innermost dimension towards outermost dimension
            for ( var i = rank - 2; i >= 0; --i )
            {
               var curLen = array.GetLength( i );
               bracesCount = bracesCount * curLen + 2 * curLen;
               delimitersCount = curLen - 1 + delimitersCount * curLen;
            }

            // Remember outermost braces
            bracesCount += 2;
            var elementSizes = new BackendSizeInfo[length];
            var elementInfo = this._elementTypeInfo.Value;
            var j = 0;
            foreach ( var elem in array )
            {
               elementSizes[j++] = elementInfo.Functionality.GetBackendSizeCheckNull( DataFormat.Text, elementInfo.DatabaseData, helper, elem, true );
            }

            // All the space taken by array structure information
            var sizeForArrayInfra = encoding.GetByteCount( boundData.ArrayDelimiter ) * delimitersCount + bracesCount * asciiSize;
            var lobos = array.GetLowerBounds();
            var loboSpecByteCount = 0;
            Int32[] upbos = null;
            if ( lobos != null )
            {
               // Bounds specification: "[lobo1:upbo1][lobo2:upbo2]...="
               loboSpecByteCount = rank * 3 * asciiSize + asciiSize; // Amount of '[', ']', ':', and '='
               upbos = new Int32[rank];
               for ( var i = 0; i < rank; ++i )
               {
                  // Remember that (Pg)SQL array indexing starts from 1 by default
                  upbos[i] = array.GetUpperBound( i ) + 1;
                  ++lobos[i];
                  // Increment spec count
                  loboSpecByteCount += helperEncoding.GetTextualIntegerRepresentationSize( lobos[i] ) + helperEncoding.GetTextualIntegerRepresentationSize( upbos[i] );
               }
               sizeForArrayInfra += loboSpecByteCount;
            }

            var nullSize = NULL_CHAR_COUNT * asciiSize;
            retVal = new BackendSizeInfo(
               sizeForArrayInfra + elementSizes.Aggregate( 0, ( cur, item ) => cur + ( item.ByteCount >= 0 ? item.ByteCount : nullSize ) ), // All the space taken by actual values
               (elementSizes, (lobos, upbos, loboSpecByteCount))
               );
         }

         return retVal;
      }

      public async ValueTask<Object> ReadBackendValueAsync(
         DataFormat dataFormat,
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamReaderWithResizableBufferAndLimitedSize stream
         )
      {
         Array retVal;
         switch ( dataFormat )
         {
            case DataFormat.Text:
               if ( stream.TotalByteCount > 2 * helper.Encoding.BytesPerASCIICharacter )
               {
                  retVal = await this.ReadArrayText( boundData, helper, stream );
               }
               else
               {
                  // Empty array
                  retVal = this._emptyArray;
               }
               break;
            case DataFormat.Binary:
               retVal = await this.ReadArrayBinary( boundData, helper, stream );
               break;
            default:
               throw new NotSupportedException( $"Data format {dataFormat} is not recognized." );
         }

         return retVal;
      }

      public async Task WriteBackendValueAsync(
         DataFormat dataFormat,
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamWriterWithResizableBufferAndLimitedSize stream,
         Object value,
         BackendSizeInfo sizeInfo,
         Boolean isArrayElement
         )
      {
         switch ( dataFormat )
         {
            case DataFormat.Text:
               await this.WriteArrayText( boundData, helper, stream, (Array) value, sizeInfo );
               break;
            case DataFormat.Binary:
               await this.WriteArrayBinary( boundData, helper, stream, (Array) value, sizeInfo );
               break;
            default:
               throw new NotSupportedException( $"Data format {dataFormat} is not recognized." );
         }
      }

      private async Task<Array> ReadArrayText(
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamReaderWithResizableBufferAndLimitedSize stream
         )
      {
         (var rank, var lobos, var lengths, var retVal) = await this.ReadArrayTextHeader( boundData, helper, stream );

         // Use exponentially expanding array instead of list. That way we can use Array.Copy right away when creating rank-1 array.
         var useTempArray = retVal == null;
         var totalCount = 0;
         // We only need temporary array if we are creating array without dimension specification prefix
         Int32[] retValIndices;
         ResizableArray<Object> tempArray;
         Int32 arrayDelimiterByteCount;
         if ( useTempArray )
         {
            tempArray = new ResizableArray<Object>( initialSize: 2, exponentialResize: true );
            lengths = new Int32[rank];
            retValIndices = null;
            arrayDelimiterByteCount = -1;
         }
         else
         {
            retValIndices = new Int32[rank];
            Array.Copy( lobos, retValIndices, rank );
            tempArray = null;
            arrayDelimiterByteCount = helper.Encoding.Encoding.GetByteCount( boundData.ArrayDelimiter );
         }

         // We start with innermost array
         var innermostArrayIndex = rank - 1;
         var lowestEncounteredArrayEnd = innermostArrayIndex;
         var asciiSize = helper.Encoding.BytesPerASCIICharacter;
         Boolean hasMore = true;
         while ( hasMore )
         {
            (var value, var ending) = await this.ReadArrayElementText( boundData, helper, stream );

            if ( useTempArray )
            {
               tempArray.CurrentMaxCapacity = totalCount + 1;
               tempArray.Array[totalCount++] = value;
               if ( lowestEncounteredArrayEnd == innermostArrayIndex )
               {
                  ++lengths[innermostArrayIndex];
               }

               // If array end encountered, we must find start of next element, if possible
               if ( ending == ElementEndingWay.ArrayEnd )
               {
                  (lowestEncounteredArrayEnd, lengths, hasMore) = await this.ReadArrayTextDimensionEnd(
                     boundData,
                     helper,
                     stream,
                     lowestEncounteredArrayEnd,
                     lengths,
                     rank
                     );
               }
            }
            else
            {
               retVal.SetValue( value, retValIndices );
               var dimsEnded = MoveNextMultiDimensionalIndex( lengths, retValIndices, lobos );
               // At this point, the ReadArrayElementText method has already read either complete array delimiter, or one array end character
               // So we only need to skip thru bytes if we have read array end character (dimsEnded > 0)
               if ( dimsEnded > 0 )
               {
                  hasMore = dimsEnded < rank;
                  if ( hasMore )
                  {
                     // Skip thru array end, array delimiter, and array start characters
                     await stream.ReadMoreOrThrow( ( dimsEnded * 2 - 1 ) * asciiSize + arrayDelimiterByteCount );
                  }
               }
            }

            stream.EraseReadBytesFromBuffer();
         }

         // Now, construct the actual array to return, if needed
         // If array to be returned is null at this stage, this means that no lower bound specifications were given.
         if ( retVal == null )
         {
            if ( rank == 1 )
            {
               // Create normal one-dimensional array (we always need to create it, since we must return X[] instead of Object[])
               retVal = Array.CreateInstance( this._arrayElementType, totalCount );
               // Populate it
               Array.Copy( tempArray.Array, retVal, totalCount );
            }
            else
            {
               // Create multi-dimensional array
               retVal = Array.CreateInstance( this._arrayElementType, lengths );
               // Populate it
               var curIndices = new Int32[lengths.Length];
               var idx = 0;
               var actualArray = tempArray.Array;
               do
               {
                  var elem = actualArray[idx++];
                  if ( elem != null )
                  {
                     retVal.SetValue( elem, curIndices );
                  }
                  MoveNextMultiDimensionalIndex( lengths, curIndices );
               } while ( idx < totalCount );
            }
         }

         return retVal;

      }

      private async ValueTask<(Int32 Rank, Int32[] Lobos, Int32[] Lengths, Array CreatedArray)> ReadArrayTextHeader(
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamReaderWithResizableBufferAndLimitedSize stream
         )
      {
         stream.EraseReadBytesFromBuffer();

         Char curChar;
         // Read the optional "[lobo1:upbo1][lobo2:upbo2]...=" dimension specification into buffer.
         var rank = 0;
         var charReader = helper.CharacterReader;
         do
         {
            curChar = await charReader.ReadNextAsync( stream );
            if ( curChar == DIM_SEPARATOR )
            {
               ++rank;
            }
         } while ( curChar != ARRAY_START );

         Int32[] lobos = null;
         Int32[] lengths = null;
         Array retVal = null;
         if ( rank > 0 )
         {
            // We encountered the explicit dimension specification. Backend should issue this only when there are 'special' lower bounds.
            // As a bonus, we will know the array dimensions before array elements start, and we can create the array to be returned right away,
            // instead of reading elements into temporary array
            lobos = new Int32[rank];
            lengths = new Int32[rank];
            var encoding = helper.Encoding;
            var asciiSize = encoding.BytesPerASCIICharacter;
            var byteArray = stream.Buffer;
            var idx = asciiSize; // Skip first '['
            for ( var i = 0; i < rank; ++i )
            {
               // In (Pg)SQL, lower bounds normally start at 1. So 1 translates to 0 in CLR, 0 to -1, etc.
               lobos[i] = encoding.ParseInt32Textual( byteArray, ref idx ) - 1;
               idx += asciiSize; // Skip ':'
               lengths[i] = encoding.ParseInt32Textual( byteArray, ref idx ) - lobos[i];
               idx += asciiSize * 2; // Skip ']' and next '[' or '='
            }
            retVal = Array.CreateInstance( this._arrayElementType, lengths, lobos );
         }

         // Read amount of starting '{' characters. That will be the array rank (unless we already learned about the rank in the dimension specification header).
         rank = 0;
         Int32 prevIdx;
         do
         {
            ++rank;
            prevIdx = stream.ReadBytesCount;
            curChar = await charReader.ReadNextAsync( stream );
         } while ( curChar == ARRAY_START );

         if ( retVal != null && rank != retVal.Rank )
         {
            throw new PgSQLException( "Backend array lower-bound specification had different rank than actual array specifciation." );
         }

         // Back one character (the one we read, that wasn't array start character)
         stream.UnreadBytes( stream.ReadBytesCount - prevIdx );

         // Remember to get rid of array start characters currently in buffer (ReadArrayElementText expects clean buffer start)
         stream.EraseReadBytesFromBuffer();

         return (rank, lobos, lengths, retVal);
      }

      private async ValueTask<(Int32 LowestEncounteredArrayEnd, Int32[] Lengths, Boolean HasMore)> ReadArrayTextDimensionEnd(
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamReaderWithResizableBufferAndLimitedSize stream,
         Int32 lowestEncounteredArrayEnd,
         Int32[] lengths,
         Int32 rank
         )
      {
         stream.EraseReadBytesFromBuffer();

         var wasArrayEnd = true;
         var innermostArrayIndex = rank - 1;
         var curArrayIndex = innermostArrayIndex;
         Char curChar;
         var charReader = helper.CharacterReader;

         while ( wasArrayEnd && --curArrayIndex >= 0 )
         {
            // End current array block
            lowestEncounteredArrayEnd = Math.Min( lowestEncounteredArrayEnd, curArrayIndex );
            if ( curArrayIndex <= lowestEncounteredArrayEnd )
            {
               lengths[curArrayIndex]++;
            }

            // Read next character
            curChar = await charReader.ReadNextAsync( stream );
            wasArrayEnd = curChar == ARRAY_END;
         }

         var hasMore = curArrayIndex >= 0;
         if ( hasMore )
         {
            // More arrays follow 
            // Read until we are at innermost array level again
            while ( curArrayIndex < innermostArrayIndex )
            {
               curChar = await charReader.ReadNextAsync( stream );
               if ( curChar == ARRAY_START )
               {
                  ++curArrayIndex;
               }
            }
         }

         stream.EraseReadBytesFromBuffer();

         return (lowestEncounteredArrayEnd, lengths, hasMore);
      }

      private static Int32 MoveNextMultiDimensionalIndex(
         Int32[] lengths,
         Int32[] indices,
         Int32[] loBos = null
         )
      {
         var i = indices.Length - 1;
         if ( loBos == null )
         {
            for ( ; i >= 0 && ++indices[i] == lengths[i]; --i )
            {
               indices[i] = 0;
            }
         }
         else
         {
            for ( ; i >= 0 && ++indices[i] == lengths[i] + loBos[i]; --i )
            {
               indices[i] = loBos[i];
            }
         }

         return lengths.Length - i - 1;
      }

      // We never encounter empty arrays when calling this, since inner empty arrays are not possible, and whole empty array string is handled separately
      private async ValueTask<(Object Value, ElementEndingWay Ending)> ReadArrayElementText(
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamReaderWithResizableBufferAndLimitedSize stream
         )
      {
         // Scan for element end
         stream.EraseReadBytesFromBuffer();

         var insideQuote = false;
         var delimLength = boundData.ArrayDelimiter.Length;
         var delim0 = boundData.ArrayDelimiter[0];
         var delim1 = delimLength > 1 ? boundData.ArrayDelimiter[1] : '\0';
         Int32 prevIdx;
         Char curChar;
         Char curChar2 = '\0';
         Boolean wasArrayDelimiter = false;
         var prevWasEscape = false;
         var asciiSize = helper.Encoding.BytesPerASCIICharacter;
         var charReader = helper.CharacterReader;
         Boolean continueReading;
         do
         {
            prevIdx = stream.ReadBytesCount;
            var charNullable = await charReader.TryReadNextAsync( stream );
            continueReading = charNullable.HasValue;
            curChar = charNullable.GetValueOrDefault();
            if ( continueReading )
            {
               if ( prevWasEscape )
               {
                  prevWasEscape = false;
               }
               else
               {
                  if ( delimLength > 1 && Char.IsHighSurrogate( curChar ) )
                  {
                     curChar2 = await charReader.TryReadNextAsync( stream ) ?? '\0';
                  }

                  switch ( curChar )
                  {
                     case ESCAPE:
                     case QUOTE:
                        if ( curChar == QUOTE )
                        {
                           insideQuote = !insideQuote;
                        }
                        else
                        {
                           prevWasEscape = true;
                        }
                        // "Shift" the array
                        stream.EraseReadBufferSegment( prevIdx, asciiSize );
                        break;
                     default:
                        wasArrayDelimiter = !insideQuote
                           && (
                              ( delimLength == 1 && curChar == delim0 )
                              || ( delimLength > 1 && curChar == delim0 && curChar2 == delim1 )
                              );
                        continueReading = !wasArrayDelimiter && curChar != ARRAY_END;
                        break;
                  }
               }
            }
         } while ( continueReading );

         var elementAndSeparatorSize = stream.ReadBytesCount;
         var ending = wasArrayDelimiter ? ElementEndingWay.ArrayDelimiter : ( curChar == ARRAY_END ? ElementEndingWay.ArrayEnd : ElementEndingWay.Abnormal );
         Object arrayElement;
         if ( ending == ElementEndingWay.Abnormal || IsNullArrayElement( helper.Encoding, stream.Buffer, prevIdx ) )
         {
            arrayElement = null;
         }
         else
         {
            stream.UnreadBytes();
            var elementTypeInfo = this._elementTypeInfo.Value;
            using ( var elementStream = stream.CreateWithLimitedSizeAndSharedBuffer( prevIdx ) )
            {
               arrayElement = await elementTypeInfo.Functionality.ReadBackendValueAsync(
                        DataFormat.Text,
                        elementTypeInfo.DatabaseData,
                        helper,
                        elementStream
                        );

            }

            // Re-read separator
            await stream.ReadMoreOrThrow( elementAndSeparatorSize - prevIdx );
         }

         // Erase all previous read data (element + separator)
         stream.EraseReadBytesFromBuffer();
         return (
            arrayElement,
            ending
            );
      }

      private const Int32 CHUNK_SIZE = 1024;




      private enum ElementEndingWay
      {
         ArrayDelimiter,
         ArrayEnd,
         Abnormal
      }

      private static Boolean IsNullArrayElement(
         IEncodingInfo encoding,
         Byte[] array,
         Int32 elementByteCount
         )
      {
         Int32 idx = 0;
         Byte lastASCIIByte;
         return elementByteCount == encoding.BytesPerASCIICharacter * NULL_CHAR_COUNT
            // Poor man's case insensitive matching
            && ( ( lastASCIIByte = encoding.ReadASCIIByte( array, ref idx ) ) == 'N' || lastASCIIByte == 'n' )
            && ( ( lastASCIIByte = encoding.ReadASCIIByte( array, ref idx ) ) == 'U' || lastASCIIByte == 'u' )
            && ( ( lastASCIIByte = encoding.ReadASCIIByte( array, ref idx ) ) == 'L' || lastASCIIByte == 'l' )
            && ( ( lastASCIIByte = encoding.ReadASCIIByte( array, ref idx ) ) == 'L' || lastASCIIByte == 'l' )
            ;
      }

      private async Task<Array> ReadArrayBinary(
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamReaderWithResizableBufferAndLimitedSize stream
         )
      {
         await stream.ReadOrThrow( sizeof( Int32 ) );
         var idx = 0;
         var rank = stream.Buffer.ReadPgInt32( ref idx );
         Array retVal;
         if ( rank < 0 )
         {
            throw new PgSQLException( "Array rank must be zero or more." );
         }
         else if ( rank == 0 )
         {
            // Empty array
            retVal = this._emptyArray;
         }
         else
         {
            // Read the rest of the header (null-map (Int32), element type id (Int32), and rank infos (2 integers per rank)
            await stream.ReadMoreOrThrow( ( 2 + 2 * rank ) * sizeof( Int32 ) );
            // Skip null-map and element type id.
            idx = sizeof( Int32 ) * 3;

            // Read lengths and lower bounds for dimensions
            var lengths = new Int32[rank];
            Int32[] loBos = null;
            for ( var i = 0; i < rank; ++i )
            {
               var curNumber = stream.Buffer.ReadPgInt32( ref idx );
               lengths[i] = curNumber;
               curNumber = stream.Buffer.ReadPgInt32( ref idx );
               if ( curNumber != 1 ) // In SQL, default min lo bo is 1. In C#, the default is 0.
               {
                  if ( loBos == null )
                  {
                     loBos = new Int32[rank];
                  }
                  loBos[i] = curNumber - 1;
               }
            }

            // Create & populate array instance
            stream.EraseReadBytesFromBuffer();
            var elemInfo = this._elementTypeInfo.Value;
            if ( rank == 1 && loBos == null )
            {
               // Can just use normal array
               var len = lengths[0];
               retVal = Array.CreateInstance( this._arrayElementType, len );
               for ( var i = 0; i < len; ++i )
               {
                  var curInfo = await elemInfo.Functionality.ReadBackendValueCheckNull( DataFormat.Binary, elemInfo.DatabaseData, helper, stream );
                  retVal.SetValue( curInfo.Value, i );
               }
            }
            else
            {
               // Have to create multi-dimensional array
               retVal = loBos == null ? Array.CreateInstance( this._arrayElementType, lengths ) : Array.CreateInstance( this._arrayElementType, lengths, loBos );
               var indices = new Int32[rank];
               if ( loBos != null )
               {
                  Array.Copy( loBos, indices, rank );
               }
               do
               {
                  var curInfo = await elemInfo.Functionality.ReadBackendValueCheckNull( DataFormat.Binary, elemInfo.DatabaseData, helper, stream );
                  retVal.SetValue( curInfo.Value, indices );
               } while ( MoveNextMultiDimensionalIndex( lengths, indices, loBos ) < rank );
            }
         }

         return retVal;
      }

      private async Task WriteArrayText(
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamWriterWithResizableBufferAndLimitedSize stream,
         Array value,
         BackendSizeInfo sizeInfo
         )
      {
         var encoding = helper.Encoding;
         var length = value.Length;
         if ( length <= 0 )
         {
            // Just write '{}'
            stream.AppendToBytes( 2 * encoding.BytesPerASCIICharacter, ( bArray, idx, count ) => encoding.WriteASCIIByte( bArray, ref idx, ARRAY_START ).WriteASCIIByte( bArray, ref idx, ARRAY_END ) );
         }
         else
         {
            var additionalSizeInfo = (TextSizeAdditionalInfo) sizeInfo.CustomInformation;
            var loboInfo = additionalSizeInfo.Item2;
            var rank = value.Rank;
            var lobos = loboInfo.Item1;
            var asciiSize = encoding.BytesPerASCIICharacter;
            if ( lobos != null )
            {
               // We must write the lower bound specification: '[lobo1:upbo1][lobo2:upbo2]...='
               var upbos = loboInfo.Item2;
               stream.AppendToBytes( loboInfo.Item3, ( array, idx, count ) =>
               {
                  for ( var i = 0; i < rank; ++i )
                  {
                     // Both lobos and upbos have correct values from GetBackendBinarySize method
                     encoding
                            .WriteASCIIByte( array, ref idx, ARRAY_LOBO_START )
                            .WriteIntegerTextual( array, ref idx, lobos[i] )
                            .WriteASCIIByte( array, ref idx, (Byte) DIM_SEPARATOR )
                            .WriteIntegerTextual( array, ref idx, upbos[i] )
                            .WriteASCIIByte( array, ref idx, ARRAY_LOBO_END );
                  }

                  // Write final '='
                  encoding.WriteASCIIByte( array, ref idx, ARRAY_LOBO_SPEC_SEPARATOR );
               } );

            }

            Int32[] indices = null;
            Int32[] lengths = null;
            if ( rank > 1 )
            {
               // We have to use indices and lengths in order to properly emit '}' and '{' in between array elements
               // N.B.! One-ranked array with lobo-specification still doesn't need to use indices and lengths, as it won't have '}' and '{' in between any elements.
               // We also don't need to initialize indices with lower bounds, as we only need to know when dimensions end.
               indices = new Int32[rank];
               lengths = value.GetLengths();
            }

            // Write '{'s
            var rankASCIISize = rank * asciiSize;
            stream.AppendToBytes( rankASCIISize, ( bArray, idx, count ) =>
            {
               for ( var i = 0; i < rank; ++i )
               {
                  encoding.WriteASCIIByte( bArray, ref idx, ARRAY_START );
               }
            } );

            // Send prefix, then send elements
            await stream.FlushAsync();

            var elementSizeInfos = additionalSizeInfo.Item1;
            var eIdx = 0;
            var elementTypeInfo = this._elementTypeInfo.Value;
            var delimByteCount = encoding.Encoding.GetByteCount( boundData.ArrayDelimiter );
            var curIdx = 0;
            foreach ( var element in value )
            {
               var elementSizeInfo = elementSizeInfos[eIdx++];
               if ( element == null )
               {
                  // Write 'NULL'
                  stream.AppendToBytes( asciiSize * NULL_CHAR_COUNT, ( bArray, idx, count ) => encoding.Encoding.GetBytes( NULL_STRING, 0, NULL_CHAR_COUNT, bArray, idx ) );
                  await stream.FlushAsync();
               }
               else
               {
                  using ( var elementStream = await stream.CreateWithLimitedSizeAndSharedBuffer( elementSizeInfo.ByteCount ) )
                  {
                     await elementTypeInfo.Functionality.WriteBackendValueAsync(
                        DataFormat.Text,
                        elementTypeInfo.DatabaseData,
                        helper,
                        elementStream,
                        element,
                        elementSizeInfo,
                        true
                        );
                     await elementStream.FlushAsync();
                  }
               }

               if ( curIdx++ < length - 1 )
               {
                  if ( indices == null )
                  {
                     stream.AppendToBytes( delimByteCount, ( bArray, idx, count ) => encoding.Encoding.GetBytes( boundData.ArrayDelimiter, 0, boundData.ArrayDelimiter.Length, bArray, idx ) );
                  }
                  else
                  {
                     // We might need to write '}'s
                     var amountOfDimensionsEnded = MoveNextMultiDimensionalIndex( lengths, indices );
                     // Have to write '}'s, followed by array separator, followed by equally many '{'s.
                     stream.AppendToBytes( amountOfDimensionsEnded * 2 * asciiSize + delimByteCount, ( bArray, idx, count ) =>
                     {
                        for ( var i = 0; i < amountOfDimensionsEnded; ++i )
                        {
                           encoding.WriteASCIIByte( bArray, ref idx, ARRAY_END );
                        }
                        idx += encoding.Encoding.GetBytes( boundData.ArrayDelimiter, 0, boundData.ArrayDelimiter.Length, bArray, idx );
                        for ( var i = 0; i < amountOfDimensionsEnded; ++i )
                        {
                           encoding.WriteASCIIByte( bArray, ref idx, ARRAY_START );
                        }
                     } );
                  }

                  await stream.FlushAsync();
               }
            }

            // Write final '}'s
            stream.AppendToBytes( rankASCIISize, ( bArray, idx, count ) =>
            {
               for ( var i = 0; i < rank; ++i )
               {
                  encoding.WriteASCIIByte( bArray, ref idx, ARRAY_END );
               }
            } );
         }
         await stream.FlushAsync();
      }

      private async Task WriteArrayBinary(
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper helper,
         StreamWriterWithResizableBufferAndLimitedSize stream,
         Array value,
         BackendSizeInfo sizeInfo
         )
      {
         // Write header (3 integers + 2 integers per rank)
         var elemInfo = this._elementTypeInfo.Value;
         var arrayLength = value.Length;
         var rank = arrayLength <= 0 ? 0 : value.Rank;
         stream.AppendToBytes( ( 3 + 2 * rank ) * sizeof( Int32 ), ( bArray, idx, count ) =>
           {
              bArray
                 .WritePgInt32( ref idx, rank )
                 .WritePgInt32( ref idx, 1 ) // null map, always zero in our case
                 .WritePgInt32( ref idx, elemInfo.DatabaseData.TypeID );
              for ( var i = 0; i < rank; ++i )
              {
                 bArray
                    .WritePgInt32( ref idx, value.GetLength( i ) )
                    .WritePgInt32( ref idx, value.GetLowerBound( i ) + 1 ); // SQL lower bounds for arrays are 1 by default
              }
           } );

         // Send header
         await stream.FlushAsync();

         if ( arrayLength > 0 )
         {
            // Send elements
            var additionalSizeInfo = (BackendSizeInfo[]) sizeInfo.CustomInformation;
            var j = 0;
            foreach ( var element in value )
            {
               var elementSizeInfo = additionalSizeInfo[j++];
               using ( var elementStream = await stream.CreateWithLimitedSizeAndSharedBuffer(
                  Math.Max( 0, elementSizeInfo.ByteCount ) + sizeof( Int32 )
                  ) )
               {
                  await elemInfo.Functionality.WriteBackendValueCheckNull(
                     DataFormat.Binary,
                     elemInfo.DatabaseData,
                     helper,
                     elementStream,
                     element,
                     elementSizeInfo,
                     true
                     );
               }
            }
         }
      }

   }
}