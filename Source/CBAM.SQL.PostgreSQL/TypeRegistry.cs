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
using CBAM.SQL.PostgreSQL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
//using TextBackendSizeInfo = UtilPack.EitherOr<System.ValueTuple<System.Int32, System.Object>, System.String>;
//using BinaryBackendSizeInfo = System.ValueTuple<System.Int32, System.Object>;
using BackendSizeInfo = System.ValueTuple<System.Int32, System.Object>;


namespace CBAM.SQL.PostgreSQL
{
   public interface TypeRegistry
   {
      Task AddTypeFunctionalitiesAsync( params (String DBTypeName, Func<(TypeRegistry TypeRegistry, PgSQLTypeDatabaseData DBTypeInfo), (PgSQLTypeFunctionality UnboundFunctionality, Boolean IsDefaultForCLRType)> FunctionalityCreator)[] functionalities );
      (PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData) GetTypeInfo( Int32 typeID );
      (PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData) GetTypeInfo( Type clrType );
      Int32 TypeInfoCount { get; }
   }



   // Using those classes makes things easier when exceptions fly from Read/WriteBackendValue.

   public interface PgSQLTypeFunctionality
   {
      Type CLRType { get; }
      Boolean SupportsReadingBinaryFormat { get; }
      Boolean SupportsWritingBinaryFormat { get; }
      BackendSizeInfo GetBackendTextSize( PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value, Boolean isArrayElement );
      BackendSizeInfo GetBackendBinarySize( PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value );
      ValueTask<Object> ReadBackendValue( DataFormat dataFormat, PgSQLTypeDatabaseData boundData, BackendABIHelper helper, StreamReaderWithResizableBufferAndLimitedSize stream );
      Task WriteBackendValue( DataFormat dataFormat, PgSQLTypeDatabaseData boundData, BackendABIHelper helper, StreamWriterWithResizableBufferAndLimitedSize stream, Object value, BackendSizeInfo additionalInfoFromSize, Boolean isArrayElement );

      Object ChangeTypeFrameworkToPgSQL( Object obj );
      Object ChangeTypePgSQLToFramework( PgSQLTypeDatabaseData boundData, Object obj, Type typeTo );
   }

   public class BackendABIHelper
   {
      public BackendABIHelper(
         IEncodingInfo encoding
         )
      {
         this.CharacterReader = new StreamCharacterReader( encoding );
      }

      public IEncodingInfo Encoding
      {
         get
         {
            return this.CharacterReader.Encoding;
         }
      }

      public StreamCharacterReader CharacterReader { get; }

      public String GetString( Byte[] array, Int32 offset, Int32 count )
      {
         // TODO string pool
         return this.Encoding.Encoding.GetString( array, offset, count );
      }

   }

   public enum DataFormat : short
   {
      Text = 0,
      Binary = 1,
   }

   public abstract class AbstractPgSQLTypeFunctionality : PgSQLTypeFunctionality
   {
      static AbstractPgSQLTypeFunctionality()
      {
         var format = (System.Globalization.NumberFormatInfo) System.Globalization.CultureInfo.InvariantCulture.NumberFormat.Clone();
         format.NumberDecimalDigits = 15;
         NumberFormat = format;
      }

      public static System.Globalization.NumberFormatInfo NumberFormat { get; }

      public abstract Type CLRType { get; }

      public abstract Boolean SupportsReadingBinaryFormat { get; }

      public abstract Boolean SupportsWritingBinaryFormat { get; }

      public abstract Object ChangeTypeFrameworkToPgSQL( Object obj );

      public abstract Object ChangeTypePgSQLToFramework( PgSQLTypeDatabaseData boundData, Object obj, Type typeTo );

      public abstract BackendSizeInfo GetBackendBinarySize( PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value );

      public abstract BackendSizeInfo GetBackendTextSize( PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value, Boolean isArrayElement );

      public abstract ValueTask<Object> ReadBackendValue( DataFormat dataFormat, PgSQLTypeDatabaseData boundData, BackendABIHelper helper, StreamReaderWithResizableBufferAndLimitedSize stream );

      public abstract Task WriteBackendValue( DataFormat dataFormat, PgSQLTypeDatabaseData boundData, BackendABIHelper helper, StreamWriterWithResizableBufferAndLimitedSize stream, Object value, BackendSizeInfo additionalInfoFromSize, Boolean isArrayElement );

      //public static async ValueTask<(Char CharacterRead, Char[] AuxArray)> TryReadNextCharacter(
      //   BackendABIHelper helper,
      //   StreamReaderWithResizableBufferAndLimitedSize stream,
      //   Char[] auxArray,
      //   Func<Char, Boolean> charChecker = null // If false -> will read next character
      //)
      //{
      //   Boolean charReadSuccessful;
      //   do
      //   {

      //      var encoding = helper.Encoding.Encoding;
      //      var arrayIndex = stream.ReadBytesCount;
      //      charReadSuccessful = await stream.TryReadMoreAsync( 1 );
      //      if ( charReadSuccessful )
      //      {
      //         var charCount = 1;
      //         while ( charCount == 1 && await stream.TryReadMoreAsync( 1 ) ) // stream.BytesLeft > 0 && charCount == 1 )
      //         {
      //            charCount = encoding.GetCharCount( stream.Buffer, arrayIndex, stream.ReadBytesCount - arrayIndex );
      //         }

      //         if ( charCount > 1 )
      //         {
      //            // Unread peeked byte
      //            stream.UnreadBytes( 1 );
      //         }


      //         if ( auxArray == null )
      //         {
      //            auxArray = new Char[1];
      //         }

      //         encoding.GetChars( stream.Buffer, arrayIndex, stream.ReadBytesCount - arrayIndex, auxArray, 0 );
      //      }

      //   } while ( !( charChecker?.Invoke( auxArray[0] ) ?? true ) );

      //   return (auxArray[0], auxArray);
      //}
   }

   public class PgSQLTypeFunctionality<TValue> : AbstractPgSQLTypeFunctionality
   {


      private readonly ReadFromBackendText<TValue> _text2CLR;
      private readonly ReadFromBackendBinary<TValue> _binary2CLR;
      private readonly ChangePgSQLToSystem<TValue> _pg2System;
      private readonly ChangeSystemToPgSQL<TValue> _system2PG;
      private readonly GetBackendSizeBinary<TValue, BackendSizeInfo> _clr2BinarySize;
      private readonly WriteBackendBinary<TValue> _clr2Binary;
      private readonly GetBackendSizeText<TValue, BackendSizeInfo> _textSize;
      private readonly WriteBackendText<TValue> _clr2Text;

      internal PgSQLTypeFunctionality(
         ReadFromBackendText<TValue> text2CLR,
         ReadFromBackendBinary<TValue> binary2CLR,
         GetBackendSizeText<TValue, BackendSizeInfo> textSize,
         GetBackendSizeBinary<TValue, BackendSizeInfo> clr2BinarySize,
         WriteBackendText<TValue> clr2Text,
         WriteBackendBinary<TValue> clr2Binary,
         ChangePgSQLToSystem<TValue> pgSQL2System,
         ChangeSystemToPgSQL<TValue> system2PgSQL
         )
      {

         this._text2CLR = text2CLR;
         this._binary2CLR = binary2CLR;
         this._pg2System = pgSQL2System;
         this._system2PG = system2PgSQL;
         this._clr2BinarySize = clr2BinarySize;
         this._clr2Binary = clr2Binary;
         this._textSize = ArgumentValidator.ValidateNotNull( nameof( textSize ), textSize );
         this._clr2Text = ArgumentValidator.ValidateNotNull( nameof( clr2Text ), clr2Text );
      }

      public override Type CLRType
      {
         get
         {
            return typeof( TValue );
         }
      }

      public override Boolean SupportsReadingBinaryFormat
      {
         get
         {
            return this._binary2CLR != null;
         }
      }

      public override Boolean SupportsWritingBinaryFormat
      {
         get
         {
            return this._clr2BinarySize != null && this._clr2Binary != null;
         }
      }

      public override Object ChangeTypePgSQLToFramework( PgSQLTypeDatabaseData boundData, Object obj, Type typeTo )
      {
         return this._pg2System == null ?
            Convert.ChangeType( obj, typeTo, System.Globalization.CultureInfo.InvariantCulture ) :
            this._pg2System( (TValue) obj, typeTo );
      }

      public override Object ChangeTypeFrameworkToPgSQL( Object obj )
      {
         return this._system2PG == null ?
            Convert.ChangeType( obj, this.CLRType, System.Globalization.CultureInfo.InvariantCulture ) :
            this._system2PG( obj );
      }

      public override BackendSizeInfo GetBackendBinarySize( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Object value )
      {
         return this._clr2BinarySize( boundData, args, (TValue) value );
      }

      public override Task WriteBackendValue( DataFormat dataFormat, PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamWriterWithResizableBufferAndLimitedSize stream, Object value, BackendSizeInfo additionalInfoFromSize, Boolean isArrayElement )
      {
         switch ( dataFormat )
         {
            case DataFormat.Text:
               return CheckDelegate( this._clr2Text, dataFormat )( boundData, args, stream, (TValue) value, additionalInfoFromSize, isArrayElement );
            case DataFormat.Binary:
               return CheckDelegate( this._clr2Binary, dataFormat )( boundData, args, stream, (TValue) value, additionalInfoFromSize );
            default:
               throw new NotSupportedException( $"Data format {dataFormat} is not recognized." );
         }
      }

      public override BackendSizeInfo GetBackendTextSize( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Object value, Boolean isArrayElement )
      {
         return value == null ? (-1, null) : this._textSize( boundData, args.Encoding, (TValue) value, isArrayElement );
      }

      public override async ValueTask<Object> ReadBackendValue(
         DataFormat dataFormat,
         PgSQLTypeDatabaseData boundData,
         BackendABIHelper args,
         StreamReaderWithResizableBufferAndLimitedSize stream
         )
      {
         switch ( dataFormat )
         {
            case DataFormat.Binary:
               return await CheckDelegate( this._binary2CLR, dataFormat )( boundData, args, stream );
            case DataFormat.Text:
               return await CheckDelegate( this._text2CLR, dataFormat )( boundData, args, stream );
            default:
               throw new NotSupportedException( $"Data format {dataFormat} is not recognized." );
         }
      }

      private static T CheckDelegate<T>( T del, DataFormat dataFormat )
         where T : class
      {
         if ( del == null )
         {
            throw new NotSupportedException( $"The data format {dataFormat} is not supported." );
         }
         return del;
      }

      public static PgSQLTypeFunctionality<TValue> CreateSingleBodyUnboundInfo(
         ReadFromBackendTextSync<TValue> text2CLR,
         ReadFromBackendBinarySync<TValue> binary2CLR,
         GetBackendSizeText<TValue, EitherOr<Int32, String>> textSize,
         GetBackendSizeBinary<TValue, Int32> clr2BinarySize,
         WriteBackendTextSync<TValue> clr2Text,
         WriteBackendBinarySync<TValue> clr2Binary,
         ChangePgSQLToSystem<TValue> pgSQL2System,
         ChangeSystemToPgSQL<TValue> system2PgSQL
         )
      {
         ArgumentValidator.ValidateNotNull( nameof( text2CLR ), text2CLR );

         GetBackendSizeText<TValue, BackendSizeInfo> textSizeActual;
         if ( textSize == null )
         {
            if ( typeof( IFormattable ).GetTypeInfo().IsAssignableFrom( typeof( TValue ).GetTypeInfo() ) )
            {
               textSizeActual = ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, TValue value, Boolean isArrayElement ) =>
               {
                  var str = ( (IFormattable) value ).ToString( null, NumberFormat );
                  return (encoding.Encoding.GetByteCount( str ), str);
               };
            }
            else
            {
               textSizeActual = ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, TValue value, Boolean isArrayElement ) =>
               {
                  var str = value.ToString();
                  return (encoding.Encoding.GetByteCount( str ), str);
               };
            }
         }
         else
         {
            textSizeActual = ( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, TValue value, Boolean isArrayElement ) =>
            {
               var thisTextSize = textSize( boundData, encoding, value, isArrayElement );
               return thisTextSize.IsFirst ? (thisTextSize.First, null) : (encoding.Encoding.GetByteCount( thisTextSize.Second ), thisTextSize.Second);
            };
         }

         WriteBackendTextSync<TValue> clr2TextActual;
         if ( clr2Text == null )
         {
            clr2TextActual = ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, TValue value, BackendSizeInfo additionalInfoFromSize, Boolean isArrayElement ) =>
            {
               var str = (String) additionalInfoFromSize.Item2;
               args.Encoding.Encoding.GetBytes( str, 0, str.Length, array, offset );
            };
         }
         else
         {
            clr2TextActual = clr2Text;
         }

         return new PgSQLTypeFunctionality<TValue>(
            async ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamReaderWithResizableBufferAndLimitedSize stream ) =>
            {
               if ( stream != null )
               {
                  await stream.ReadAllBytesToBuffer();
               }
               return text2CLR( boundData, args, stream.Buffer, 0, (Int32) stream.TotalByteCount );
            },
            binary2CLR == null ? (ReadFromBackendBinary<TValue>) null : async ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamReaderWithResizableBufferAndLimitedSize stream ) =>
            {
               if ( stream != null )
               {
                  await stream.ReadAllBytesToBuffer();
               }

               return binary2CLR( boundData, args, stream.Buffer, 0, (Int32) stream.TotalByteCount );
            },
            textSizeActual,
            clr2BinarySize == null ? (GetBackendSizeBinary<TValue, BackendSizeInfo>) null : ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, TValue value ) => (clr2BinarySize( boundData, args, value ), null),
            async ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamWriterWithResizableBufferAndLimitedSize stream, TValue value, BackendSizeInfo additionalInfoFromSize, Boolean isArrayElement ) =>
            {
               stream.AppendToBytes( additionalInfoFromSize.Item1, ( array, actualOffset, actualCount ) => clr2TextActual( boundData, args, array, actualOffset, value, additionalInfoFromSize, isArrayElement ) );
               await stream.FlushAsync();
            },
            clr2Binary == null ? (WriteBackendBinary<TValue>) null : async ( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamWriterWithResizableBufferAndLimitedSize stream, TValue value, BackendSizeInfo additionalInfoFromSize ) =>
            {
               stream.AppendToBytes( additionalInfoFromSize.Item1, ( array, actualOffset, actualCount ) => clr2Binary( boundData, args, array, actualOffset, value, additionalInfoFromSize ) );
               await stream.FlushAsync();
            },
            pgSQL2System,
            system2PgSQL
            );
      }
   }

   public delegate ValueTask<TValue> ReadFromBackendText<TValue>( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamReaderWithResizableBufferAndLimitedSize stream );
   public delegate ValueTask<TValue> ReadFromBackendBinary<TValue>( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamReaderWithResizableBufferAndLimitedSize stream );
   public delegate TResult GetBackendSizeBinary<TValue, TResult>( PgSQLTypeDatabaseData boundData, BackendABIHelper args, TValue value );
   public delegate Task WriteBackendBinary<TValue>( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamWriterWithResizableBufferAndLimitedSize stream, TValue value, BackendSizeInfo additionalInfoFromSize );
   public delegate Object ChangePgSQLToSystem<TValue>( TValue pgSQLObject, Type targetType );
   public delegate TValue ChangeSystemToPgSQL<TValue>( Object systemObject );
   public delegate TResult GetBackendSizeText<TValue, TResult>( PgSQLTypeDatabaseData boundData, IEncodingInfo encoding, TValue value, Boolean isArrayElement );
   public delegate Task WriteBackendText<TValue>( PgSQLTypeDatabaseData boundData, BackendABIHelper args, StreamWriterWithResizableBufferAndLimitedSize stream, TValue value, BackendSizeInfo additionalInfoFromSize, Boolean isArrayElement );

   public delegate TValue ReadFromBackendTextSync<TValue>( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count );
   public delegate TValue ReadFromBackendBinarySync<TValue>( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, Int32 count );
   public delegate void WriteBackendTextSync<TValue>( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, TValue value, BackendSizeInfo additionalInfoFromSize, Boolean isArrayElement );
   public delegate void WriteBackendBinarySync<TValue>( PgSQLTypeDatabaseData boundData, BackendABIHelper args, Byte[] array, Int32 offset, TValue value, BackendSizeInfo additionalInfoFromSize );

   public sealed class PgSQLTypeDatabaseData
   {
      public PgSQLTypeDatabaseData(
         String typeName,
         Int32 typeID,
         String arrayDelimiter,
         Int32 elementTypeID
         )
      {
         this.TypeName = typeName;
         this.TypeID = typeID;
         this.ArrayDelimiter = arrayDelimiter;
         this.ElementTypeID = elementTypeID;
      }

      public String TypeName { get; }
      public Int32 TypeID { get; }
      public Int32 ElementTypeID { get; }
      public String ArrayDelimiter { get; } // String because we might get surrogate pairs here...
   }
}

public static partial class E_PgSQL
{
   private const Int32 NULL_BYTE_COUNT = -1;

   public static async ValueTask<(Object Value, Int32 BytesReadFromStream)> ReadBackendValueCheckNull(
      this PgSQLTypeFunctionality typeFunctionality,
      DataFormat dataFormat,
      PgSQLTypeDatabaseData boundData,
      BackendABIHelper helper,
      StreamReaderWithResizableBufferAndLimitedSize stream
      )
   {
      await stream.ReadOrThrow( sizeof( Int32 ) );
      var byteCount = 0;
      var length = stream.Buffer.ReadPgInt32( ref byteCount );
      Object retVal;
      if ( length >= 0 )
      {
         byteCount += length;
         using ( var limitedStream = stream.CreateWithLimitedSizeAndSharedBuffer( length ) )
         {
            try
            {
               retVal = await typeFunctionality.ReadBackendValue(
                  dataFormat,
                  boundData,
                  helper,
                  limitedStream
                  );
            }
            finally
            {
               try
               {
                  await limitedStream.SkipThroughRemainingBytes();
               }
               catch
               {
                  // Ignore
               }
            }
         }
      }
      else
      {
         retVal = null;
      }

      return (retVal, byteCount);
   }

   public static BackendSizeInfo GetBackendTextSizeCheckNull( this PgSQLTypeFunctionality typeFunctionality, PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value, Boolean isArrayElement )
   {
      return value == null ? (NULL_BYTE_COUNT, null) : typeFunctionality.GetBackendTextSize( boundData, helper, value, isArrayElement );
   }

   public static BackendSizeInfo GetBackendBinarySizeCheckNull( this PgSQLTypeFunctionality typeFunctionality, PgSQLTypeDatabaseData boundData, BackendABIHelper helper, Object value )
   {
      return value == null ? (NULL_BYTE_COUNT, null) : typeFunctionality.GetBackendBinarySize( boundData, helper, value );
   }

   public static async Task WriteBackendValueCheckNull(
      this PgSQLTypeFunctionality typeFunctionality,
      DataFormat dataFormat,
      PgSQLTypeDatabaseData boundData,
      BackendABIHelper helper,
      StreamWriterWithResizableBufferAndLimitedSize stream,
      Object value,
      BackendSizeInfo additionalInfoFromSize,
      Boolean isArrayElement
      )
   {
      stream.AppendToBytes( sizeof( Int32 ), ( array, actualOffset, actualCount ) => array.WritePgInt32( ref actualOffset, value == null ? NULL_BYTE_COUNT : additionalInfoFromSize.Item1 ) );
      if ( value != null )
      {
         await typeFunctionality.WriteBackendValue( dataFormat, boundData, helper, stream, value, additionalInfoFromSize, isArrayElement );
      }
      await stream.FlushAsync();
   }

   public static Boolean TryGetTypeInfo( this TypeRegistry typeRegistry, Int32 typeID, out (PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData) value )
   {
      value = typeRegistry.GetTypeInfo( typeID );
      return value.UnboundInfo != null && value.BoundData != null;
   }

   public static Boolean TryGetTypeInfo( this TypeRegistry typeRegistry, Type clrType, out (PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData) value )
   {
      value = typeRegistry.GetTypeInfo( clrType );
      return value.UnboundInfo != null && value.BoundData != null;
   }

   public static Byte[] WritePgInt32( this Byte[] array, ref Int32 idx, Int32 value )
   {
      array.WriteInt32BEToBytes( ref idx, value );
      return array;
   }

   public static Int32 ReadPgInt32( this Byte[] array, ref Int32 idx )
   {
      return array.ReadInt32BEFromBytes( ref idx );
   }

   public static Int16 ReadPgInt16( this Byte[] array, ref Int32 idx )
   {
      return array.ReadInt16BEFromBytes( ref idx );
   }

   public static Int32 ReadPgInt16Count( this Byte[] array, ref Int32 idx )
   {
      return array.ReadUInt16BEFromBytes( ref idx );
   }

   // TODO move to utilpack
   public static Int32 CountOccurrances( this String str, String substring, StringComparison comparison )
   {
      var count = 0;

      if ( substring != null && substring.Length > 0 )
      {
         var idx = 0;
         while ( ( idx = str.IndexOf( substring, idx, comparison ) ) != -1 )
         {
            idx += substring.Length;
            ++count;
         }
      }

      return count;
   }


}