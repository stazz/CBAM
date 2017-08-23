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
using CBAM.SQL.PostgreSQL.Implementation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using MessageIOArgs = System.ValueTuple<CBAM.SQL.PostgreSQL.BackendABIHelper, System.IO.Stream, System.Threading.CancellationToken, UtilPack.ResizableArray<System.Byte>>;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   internal abstract class BackendMessageObject
   {

      internal BackendMessageObject( BackendMessageCode code )
      {
         this.Code = code;
      }

      internal BackendMessageCode Code { get; }

      public static async ValueTask<(BackendMessageObject msg, Int32 msgSize)> ReadBackendMessageAsync(
         MessageIOArgs ioArgs,
         ResizableArray<ResettableTransformable<Int32?, Int32>> columnSizes
         )
      {
         var args = ioArgs.Item1;
         var stream = ioArgs.Item2;
         var token = ioArgs.Item3;
         var buffer = ioArgs.Item4;
         await stream.ReadSpecificAmountAsync( buffer.Array, 0, 5, token );
         var code = (BackendMessageCode) buffer.Array[0];
         var length = buffer.Array.ReadInt32BEFromBytesNoRef( 1 );
         var remaining = length - sizeof( Int32 );
         if ( code != BackendMessageCode.DataRow && code != BackendMessageCode.CopyData )
         {
            // Just read the whole message at once for everything else except DataRow and CopyData messages
            buffer.CurrentMaxCapacity = remaining;
            await stream.ReadSpecificAmountAsync( buffer.Array, 0, remaining, token );
            remaining = 0;
         }
         var array = buffer.Array;
         var encoding = args.Encoding.Encoding;

         BackendMessageObject result;
         switch ( code )
         {
            case BackendMessageCode.AuthenticationRequest:
               result = new AuthenticationResponse( array, length );
               break;
            case BackendMessageCode.ErrorResponse:
               result = new PgSQLErrorObject( array, encoding, true );
               break;
            case BackendMessageCode.NoticeResponse:
               result = new PgSQLErrorObject( array, encoding, false );
               break;
            case BackendMessageCode.RowDescription:
               result = new RowDescription( array, encoding );
               break;
            case BackendMessageCode.DataRow:
               (result, remaining) = await DataRowObject.ReadDataRow( stream, token, array, columnSizes, remaining );
               break;
            case BackendMessageCode.ParameterDescription:
               result = new ParameterDescription( array );
               break;
            case BackendMessageCode.ParameterStatus:
               result = new ParameterStatus( array, encoding );
               break;
            case BackendMessageCode.ReadyForQuery:
               result = new ReadyForQuery( array );
               break;
            case BackendMessageCode.BackendKeyData:
               result = new BackendKeyData( array );
               break;
            case BackendMessageCode.CommandComplete:
               result = new CommandComplete( array, encoding );
               break;
            case BackendMessageCode.NotificationResponse:
               result = new NotificationMessage( array, encoding );
               break;
            case BackendMessageCode.CopyInResponse:
               result = new CopyInOrOutMessage( array, true );
               break;
            case BackendMessageCode.CopyOutResponse:
               result = new CopyInOrOutMessage( array, false );
               break;
            case BackendMessageCode.CopyData:
               result = new CopyDataMessage( length );
               break;
            case BackendMessageCode.ParseComplete:
               result = MessageWithNoContents.PARSE_COMPLETE;
               break;
            case BackendMessageCode.BindComplete:
               result = MessageWithNoContents.BIND_COMPLETE;
               break;
            case BackendMessageCode.EmptyQueryResponse:
               result = MessageWithNoContents.EMPTY_QUERY;
               break;
            case BackendMessageCode.NoData:
               result = MessageWithNoContents.NO_DATA;
               break;
            case BackendMessageCode.CopyDone:
               result = MessageWithNoContents.COPY_DONE;
               break;
            case BackendMessageCode.CloseComplete:
               result = MessageWithNoContents.CLOSE_COMPLETE;
               break;
            default:
               throw new NotSupportedException( "Not supported backend response: " + code );
         }

         return (result, remaining);
      }
   }

   internal sealed class PgSQLErrorObject : BackendMessageObject
   {
      private readonly PgSQLError _error;

      public PgSQLErrorObject( Byte[] array, Encoding encoding, Boolean isError )
         : base( isError ? BackendMessageCode.ErrorResponse : BackendMessageCode.NoticeResponse )
      {
         String severity = null,
            code = null,
            message = null,
            detail = null,
            hint = null,
            position = null,
            internalPosition = null,
            internalQuery = null,
            where = null,
            file = null,
            line = null,
            routine = null,
            schemaName = null,
            tableName = null,
            columnName = null,
            datatypeName = null,
            constraintName = null;

         Byte fieldType;
         var offset = 0;
         do
         {
            fieldType = array.ReadByteFromBytes( ref offset );
            if ( fieldType != 0 )
            {
               var str = array.ReadZeroTerminatedStringFromBytes( ref offset, encoding );
               switch ( fieldType )
               {
                  case (Byte) 'S':
                     severity = str;
                     break;
                  case (Byte) 'C':
                     code = str;
                     break;
                  case (Byte) 'M':
                     message = str;
                     break;
                  case (Byte) 'D':
                     detail = str;
                     break;
                  case (Byte) 'H':
                     hint = str;
                     break;
                  case (Byte) 'P':
                     position = str;
                     break;
                  case (Byte) 'p':
                     internalPosition = str;
                     break;
                  case (Byte) 'q':
                     internalQuery = str;
                     break;
                  case (Byte) 'W':
                     where = str;
                     break;
                  case (Byte) 'F':
                     file = str;
                     break;
                  case (Byte) 'L':
                     line = str;
                     break;
                  case (Byte) 'R':
                     routine = str;
                     break;
                  case (Byte) 's':
                     schemaName = str;
                     break;
                  case (Byte) 't':
                     tableName = str;
                     break;
                  case (Byte) 'c':
                     columnName = str;
                     break;
                  case (Byte) 'd':
                     datatypeName = str;
                     break;
                  case (Byte) 'n':
                     constraintName = str;
                     break;
                  default:
                     // Unknown error field, just continue
                     break;
               }
            }
         } while ( fieldType != 0 );

         this._error = new PgSQLError(
            severity,
            code,
            message,
            detail,
            hint,
            position,
            internalPosition,
            internalQuery,
            where,
            file,
            line,
            routine,
            schemaName,
            tableName,
            columnName,
            datatypeName,
            constraintName
            );
      }

      internal PgSQLError Error
      {
         get
         {
            return this._error;
         }
      }
   }

   internal sealed class AuthenticationResponse : BackendMessageObject
   {
      internal enum AuthenticationRequestType
      {
         AuthenticationOk = 0,
         AuthenticationKerberosV4 = 1,
         AuthenticationKerberosV5 = 2,
         AuthenticationClearTextPassword = 3,
         AuthenticationCryptPassword = 4,
         AuthenticationMD5Password = 5,
         AuthenticationSCMCredential = 6,
         AuthenticationGSS = 7,
         AuthenticationGSSContinue = 8,
         AuthenticationSSPI = 9
      }

      public AuthenticationResponse( Byte[] array, Int32 messageLength )
         : base( BackendMessageCode.AuthenticationRequest )
      {
         var offset = 0;
         this.RequestType = (AuthenticationRequestType) array.ReadPgInt32( ref offset );
         // Don't count in message length int32 + auth request type int32
         this.AdditionalDataInfo = (offset, messageLength - offset - sizeof( Int32 ));
      }

      internal AuthenticationRequestType RequestType { get; }

      internal (Int32 offset, Int32 count) AdditionalDataInfo { get; }
   }

   internal sealed class RowDescription : BackendMessageObject
   {
      internal sealed class FieldData
      {
         internal readonly String name;
         internal readonly Int32 tableID;
         internal readonly Int16 colAttr;
         internal readonly Int32 dataTypeID;
         internal readonly Int16 dataTypeSize;
         internal readonly Int32 dataTypeModifier;

         internal FieldData( Byte[] array, Encoding encoding, ref Int32 offset )
         {
            this.name = array.ReadZeroTerminatedStringFromBytes( ref offset, encoding );
            this.tableID = array.ReadPgInt32( ref offset );
            this.colAttr = array.ReadPgInt16( ref offset );
            this.dataTypeID = array.ReadPgInt32( ref offset );
            this.dataTypeSize = array.ReadPgInt16( ref offset );
            this.dataTypeModifier = array.ReadPgInt32( ref offset );
            this.DataFormat = (DataFormat) array.ReadPgInt16( ref offset );
         }

         internal DataFormat DataFormat { get; }
      }

      public RowDescription( Byte[] array, Encoding encoding )
         : base( BackendMessageCode.RowDescription )
      {
         var offset = 0;
         var fieldCount = array.ReadPgInt16Count( ref offset );
         var fields = new FieldData[Math.Max( 0, fieldCount )];
         for ( var i = 0; i < fieldCount; ++i )
         {
            fields[i] = new FieldData( array, encoding, ref offset );
         }
         this.Fields = fields;
      }

      internal FieldData[] Fields { get; }
   }

   internal sealed class ParameterDescription : BackendMessageObject
   {

      public ParameterDescription( Byte[] array )
         : base( BackendMessageCode.ParameterDescription )
      {
         var offset = 0;
         var idCount = array.ReadPgInt16Count( ref offset );
         var ids = new Int32[Math.Max( 0, idCount )];
         for ( var i = 0; i < idCount; ++i )
         {
            ids[i] = array.ReadPgInt32( ref offset );
         }
         this.ObjectIDs = ids;

      }

      internal Int32[] ObjectIDs { get; }
   }

   internal sealed class ParameterStatus : BackendMessageObject
   {

      public ParameterStatus( Byte[] array, Encoding encoding )
         : base( BackendMessageCode.ParameterStatus )
      {
         var offset = 0;
         this.Name = array.ReadZeroTerminatedStringFromBytes( ref offset, encoding );
         this.Value = array.ReadZeroTerminatedStringFromBytes( ref offset, encoding );
      }

      internal String Name { get; }

      internal String Value { get; }
   }

   internal sealed class DataRowObject : BackendMessageObject
   {
      private readonly Int32 _columnCount;
      private readonly Transformable<Int32?, Int32>[] _columnSizes;

      private DataRowObject(
         Int32 columnCount,
         ResizableArray<ResettableTransformable<Int32?, Int32>> columnSizes
         )
         : base( BackendMessageCode.DataRow )
      {
         this._columnCount = columnCount;
         columnSizes.CurrentMaxCapacity = columnCount;
         var array = columnSizes.Array;
         this._columnSizes = array;
         for ( var i = 0; i < columnCount; ++i )
         {
            var cur = array[i];
            if ( cur == null )
            {
               cur = new ResettableTransformable<Int32?, Int32>( null );
               array[i] = cur;
            }
            cur.TryReset();
         }
      }

      public async Task<Int32> ReadColumnByteCount(
         BackendABIHelper args,
         Stream stream,
         CancellationToken token,
         Int32 columnIndex,
         ResizableArray<Byte> array
         )
      {
         var columnSizeHolder = this._columnSizes[columnIndex];
         await columnSizeHolder.TryTransitionOrWaitAsync( async unused =>
         {
            await stream.ReadSpecificAmountAsync( array.Array, 0, sizeof( Int32 ), token );
            var idx = 0;
            return array.Array.ReadPgInt32( ref idx );
         } );
         return columnSizeHolder.Transformed;
      }

      public static async ValueTask<(DataRowObject, Int32)> ReadDataRow(
         Stream stream,
         CancellationToken token,
         Byte[] array,
         ResizableArray<ResettableTransformable<Int32?, Int32>> columnSizes,
         Int32 msgSize
         )
      {
         await stream.ReadSpecificAmountAsync( array, 0, sizeof( Int16 ), token );
         msgSize -= sizeof( Int16 );
         var idx = 0;
         var colCount = array.ReadPgInt16Count( ref idx );
         return (new DataRowObject( colCount, columnSizes ), msgSize);
      }
   }

   internal sealed class ReadyForQuery : BackendMessageObject
   {

      public ReadyForQuery( Byte[] array )
         : base( BackendMessageCode.ReadyForQuery )
      {
         this.Status = (TransactionStatus) array[0];
      }

      public TransactionStatus Status { get; }
   }

   internal sealed class BackendKeyData : BackendMessageObject
   {

      public BackendKeyData( Byte[] array )
         : base( BackendMessageCode.BackendKeyData )
      {
         var idx = 0;
         this.ProcessID = array.ReadPgInt32( ref idx );
         this.Key = array.ReadPgInt32( ref idx );
      }

      internal Int32 ProcessID { get; }

      internal Int32 Key { get; }
   }

   internal sealed class CommandComplete : BackendMessageObject
   {

      public CommandComplete( Byte[] array, Encoding encoding )
         : base( BackendMessageCode.CommandComplete )
      {
         const String INSERT = "INSERT";

         var idx = 0;
         var tag = array.ReadZeroTerminatedStringFromBytes( ref idx, encoding );
         this.FullCommandTag = tag;
         idx = 0;
         while ( Char.IsWhiteSpace( tag[idx] ) && ++idx < tag.Length ) ;
         String actualTag = null;
         if ( idx < tag.Length - 1 )
         {
            var start = idx;
            var max = idx;
            while ( !Char.IsWhiteSpace( tag[max] ) && ++max < tag.Length ) ;
            var isInsert = max - idx == INSERT.Length && tag.IndexOf( INSERT, StringComparison.OrdinalIgnoreCase ) == idx;
            if ( isInsert )
            {
               // Next word is inserted row id
               idx = max + 1;
               while ( Char.IsWhiteSpace( tag[idx] ) && ++idx < tag.Length ) ;
               max = idx;
               while ( !Char.IsWhiteSpace( tag[max] ) && ++max < tag.Length ) ;
               if ( max - idx > 0 && Int64.TryParse( tag.Substring( idx, max - idx ), out Int64 insertedID ) )
               {
                  this.LastInsertedID = insertedID;
               }
            }

            // Last word is affected row count
            max = tag.Length - 1;
            while ( Char.IsWhiteSpace( tag[max] ) && --max >= 0 ) ;
            if ( max > 0 )
            {
               idx = max;
               ++max;
               while ( !Char.IsWhiteSpace( tag[idx] ) && --idx >= 0 ) ;
               ++idx;
               if ( max - idx > 0 && Int32.TryParse( tag.Substring( idx, max - idx ), out Int32 affectedRows ) )
               {
                  this.AffectedRows = affectedRows;
               }

               // First integer word marks actual command id
               if ( this.AffectedRows.HasValue )
               {
                  // See if previous word is number (happens only in insert)
                  --idx;
                  Char c;
                  while ( ( Char.IsDigit( ( c = tag[idx] ) ) || c == '-' || c == '+' || Char.IsWhiteSpace( c ) ) && --idx >= 0 ) ;
                  if ( idx >= 0 )
                  {
                     actualTag = tag.Substring( 0, idx + 1 );
                  }
               }
            }

         }

         this.CommandTag = actualTag ?? tag;

      }

      internal String CommandTag { get; }

      internal String FullCommandTag { get; }

      internal Int32? AffectedRows { get; }

      internal Int64? LastInsertedID { get; }
   }

   internal sealed class NotificationMessage : BackendMessageObject
   {
      private readonly NotificationEventArgs _args;

      public NotificationMessage( Byte[] array, Encoding encoding )
         : base( BackendMessageCode.NotificationResponse )
      {
         var idx = 0;
         this._args = new NotificationEventArgs(
            array.ReadPgInt32( ref idx ),
            array.ReadZeroTerminatedStringFromBytes( ref idx, encoding ),
            array.ReadZeroTerminatedStringFromBytes( ref idx, encoding )
            );
      }

      internal NotificationEventArgs Args
      {
         get
         {
            return this._args;
         }
      }
   }

   internal sealed class CopyInOrOutMessage : BackendMessageObject
   {

      public CopyInOrOutMessage( Byte[] array, Boolean isIn )
         : base( isIn ? BackendMessageCode.CopyInResponse : BackendMessageCode.CopyOutResponse )
      {
         var idx = 0;
         this.CopyFormat = (DataFormat) array.ReadByteFromBytes( ref idx );
         var arraySize = array.ReadPgInt16Count( ref idx );
         var formats = new Int16[Math.Max( 0, arraySize )];
         for ( var i = 0; i < arraySize; ++i )
         {
            formats[i] = array.ReadPgInt16( ref idx );
         }
         this.FieldFormats = formats;
      }

      internal DataFormat CopyFormat { get; }

      internal Int16[] FieldFormats { get; }
   }

   internal sealed class CopyDataMessage : BackendMessageObject
   {
      public CopyDataMessage( Int32 messageLength )
         : base( BackendMessageCode.CopyData )
      {
         this.DataSize = messageLength - 4;
      }

      public Int32 DataSize { get; }
   }

   internal sealed class MessageWithNoContents : BackendMessageObject
   {
      public static MessageWithNoContents PARSE_COMPLETE = new MessageWithNoContents( BackendMessageCode.ParseComplete );
      public static MessageWithNoContents BIND_COMPLETE = new MessageWithNoContents( BackendMessageCode.BindComplete );
      public static MessageWithNoContents CLOSE_COMPLETE = new MessageWithNoContents( BackendMessageCode.CloseComplete );
      public static MessageWithNoContents EMPTY_QUERY = new MessageWithNoContents( BackendMessageCode.EmptyQueryResponse );
      public static MessageWithNoContents NO_DATA = new MessageWithNoContents( BackendMessageCode.NoData );
      public static MessageWithNoContents COPY_DONE = new MessageWithNoContents( BackendMessageCode.CopyDone );

      private MessageWithNoContents( BackendMessageCode code )
         : base( code )
      {

      }
   }


   internal enum BackendMessageCode : byte
   {
      ParseComplete = (Byte) '1',
      BindComplete = (Byte) '2',
      CloseComplete = (Byte) '3',

      NotificationResponse = (Byte) 'A',
      CommandComplete = (Byte) 'C',
      DataRow = (Byte) 'D',
      ErrorResponse = (Byte) 'E',
      CopyInResponse = (Byte) 'G',
      CopyOutResponse = (Byte) 'H',
      EmptyQueryResponse = (Byte) 'I',
      BackendKeyData = (Byte) 'K',
      NoticeResponse = (Byte) 'N',
      AuthenticationRequest = (Byte) 'R',
      ParameterStatus = (Byte) 'S',
      RowDescription = (Byte) 'T',
      FunctionCallResponse = (Byte) 'V',
      ReadyForQuery = (Byte) 'Z',

      CopyDone = (Byte) 'c',
      CopyData = (Byte) 'd',
      NoData = (Byte) 'n',
      PortalSuspended = (Byte) 's', // We should never get this message, as we always specify to fetch all rows in Execute message.
      ParameterDescription = (Byte) 't',
   }
}

public static partial class E_CBAM
{
   internal static async ValueTask<Byte> ReadByte( this Stream stream, ResizableArray<Byte> array, CancellationToken token )
   {
      //array.CurrentMaxCapacity = sizeof( Byte );
      await stream.ReadSpecificAmountAsync( array.Array, 0, 1, token );
      return array.Array[0];
   }
}