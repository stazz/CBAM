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
using CBAM.SQL.Implementation;
using CBAM.SQL.PostgreSQL;
using CBAM.SQL.PostgreSQL.Implementation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UtilPack;
using BackendSizeInfo = System.ValueTuple<System.Int32, System.Object, System.Object>;
using MessageIOArgs = System.ValueTuple<CBAM.SQL.PostgreSQL.BackendABIHelper, System.IO.Stream, System.Threading.CancellationToken, UtilPack.ResizableArray<System.Byte>>;
using FormatCodeInfo = System.ValueTuple<CBAM.SQL.PostgreSQL.DataFormat[], System.Int32>;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   internal abstract class FrontEndMessage
   {
      private const Int32 MAX_MESSAGE_SIZE = 0x3fffffff;

      public async Task SendMessageAsync(
         MessageIOArgs args,
         Boolean skipFlush = false
         )
      {
         var size = this.CalculateSize( args.Item1, args.Item4 ) + 4;
         if ( size > MAX_MESSAGE_SIZE )
         {
            // Backend's maxalloc size is largest message size that it can allocate
            throw new PgSQLException( "Too big message to send to backend (max is " + MAX_MESSAGE_SIZE + ", and " + this + " size was " + size + "." );
         }

         try
         {
            await this.DoSendMessageAsync( args.Item1, args.Item2, size, args.Item3, args.Item4 );
            if ( !skipFlush )
            {
               await args.Item2.FlushAsync( args.Item3 );
            }
         }
         catch ( Exception exc )
         {
            throw new PgSQLException( "Error when writing message to backend.", exc );
         }
      }

      protected abstract Int32 CalculateSize( BackendABIHelper args, ResizableArray<Byte> array );

      protected abstract Task DoSendMessageAsync( BackendABIHelper args, Stream stream, Int32 size, CancellationToken token, ResizableArray<Byte> array );


   }
   internal abstract class FrontEndMessageWithCode : FrontEndMessage
   {
      private const Int32 PREFIX_SIZE = sizeof( Byte ) + sizeof( Int32 );

      private readonly FrontEndMessageCode _code;

      internal FrontEndMessageWithCode( FrontEndMessageCode code )
      {
         this._code = code;
      }

      protected override async Task DoSendMessageAsync(
         BackendABIHelper args,
         Stream stream,
         Int32 size,
         CancellationToken token,
         ResizableArray<Byte> array
         )
      {
         array.CurrentMaxCapacity = PREFIX_SIZE;
         var idx = 0;

         array.Array
            .WriteByteToBytes( ref idx, (Byte) this._code )
            .WritePgInt32( ref idx, size );
         await stream.WriteAsync( array.Array, 0, PREFIX_SIZE, token );
         await this.PerformSendAfterWriteAsync( args, stream, size, token, array );
      }

      protected abstract Task PerformSendAfterWriteAsync( BackendABIHelper args, Stream stream, Int32 size, CancellationToken token, ResizableArray<Byte> array );

      internal FrontEndMessageCode Code
      {
         get
         {
            return this._code;
         }
      }
   }

   internal sealed class FrontEndMessageWithNoContent : FrontEndMessageWithCode
   {
      internal static readonly FrontEndMessageWithNoContent TERMINATION = new FrontEndMessageWithNoContent( FrontEndMessageCode.Termination );
      internal static readonly FrontEndMessageWithNoContent SYNC = new FrontEndMessageWithNoContent( FrontEndMessageCode.Sync );
      internal static readonly FrontEndMessageWithNoContent FLUSH = new FrontEndMessageWithNoContent( FrontEndMessageCode.Flush );
      internal static readonly FrontEndMessageWithNoContent COPY_DONE = new FrontEndMessageWithNoContent( FrontEndMessageCode.CopyDone );

      private FrontEndMessageWithNoContent( FrontEndMessageCode code )
         : base( code )
      {
      }

      protected override Int32 CalculateSize( BackendABIHelper args, ResizableArray<Byte> array )
      {
         return 0;
      }

      protected override Task PerformSendAfterWriteAsync( BackendABIHelper args, Stream stream, Int32 size, CancellationToken token, ResizableArray<Byte> array )
      {
         return TaskUtils.CompletedTask;
      }
   }

   internal sealed class PasswordMessage : FrontEndMessageWithSingleBody
   {
      private readonly Byte[] _pw;

      internal PasswordMessage( Byte[] pw )
         : base( FrontEndMessageCode.PasswordMessage )
      {
         ArgumentValidator.ValidateNotNull( "Password", pw );

         this._pw = pw;
      }

      protected override Int32 CalculateBufferSize( BackendABIHelper args, ResizableArray<Byte> array )
      {
         return this._pw.Length + 1;
      }

      protected override void WriteMessageToBuffer( BackendABIHelper args, ResizableArray<Byte> array )
      {
         this._pw.CopyTo( array.Array, 0 );
         array.Array[this._pw.Length] = 0;
      }
   }

   internal sealed class StartupMessage : FrontEndMessage
   {
      private const Int32 PREFIX = 8;

      private readonly Int32 _protocolVersion;
      private readonly IDictionary<String, String> _parameters;

      internal StartupMessage(
         Int32 protocolVersion,
         IDictionary<String, String> parameters
         )
      {
         this._protocolVersion = protocolVersion;
         this._parameters = parameters ?? new Dictionary<String, String>();
      }

      protected override Int32 CalculateSize( BackendABIHelper args, ResizableArray<Byte> array )
      {
         return 5 // int32 (protocol version) + end-byte (after message, zero)
            + this._parameters.Sum( kvp => args.GetStringSize( kvp.Key, array ) + args.GetStringSize( kvp.Value, array ) );
      }

      protected override async Task DoSendMessageAsync(
         BackendABIHelper args,
         Stream stream,
         Int32 size,
         CancellationToken token,
         ResizableArray<Byte> array
         )
      {
         // As documentation states, "For historical reasons, the very first message sent by the client (the startup message) has no initial message-type byte."
         // Hence we don't inherit the FrontEndMessageObjectWithCode class
         array.CurrentMaxCapacity = PREFIX;
         var idx = 0;
         array.Array
            .WritePgInt32( ref idx, size )
            .WritePgInt32( ref idx, this._protocolVersion );
         await stream.WriteAsync( array.Array, 0, PREFIX, token );

         foreach ( var kvp in this._parameters )
         {
            await args.WriteString( stream, kvp.Key, token, array );
            await args.WriteString( stream, kvp.Value, token, array );
         }
         array.Array[0] = 0;
         await stream.WriteAsync( array.Array, 0, 1, token );
      }
   }

   internal abstract class FrontEndMessageWithSingleBody : FrontEndMessageWithCode
   {
      public FrontEndMessageWithSingleBody( FrontEndMessageCode code )
         : base( code )
      {
      }

      protected sealed override Int32 CalculateSize( BackendABIHelper args, ResizableArray<Byte> array )
      {
         return this.CalculateBufferSize( args, array );
      }

      protected sealed override async Task PerformSendAfterWriteAsync(
         BackendABIHelper args,
         Stream stream,
         Int32 size,
         CancellationToken token,
         ResizableArray<Byte> array
         )
      {
         // Given size includes the integer that already has been written
         size -= sizeof( Int32 );
         array.CurrentMaxCapacity = size;
         this.WriteMessageToBuffer( args, array );
         await stream.WriteAsync( array.Array, 0, size, token );
      }

      protected abstract Int32 CalculateBufferSize( BackendABIHelper args, ResizableArray<Byte> array );

      protected abstract void WriteMessageToBuffer( BackendABIHelper args, ResizableArray<Byte> array );
   }

   internal sealed class QueryMessage : FrontEndMessageWithSingleBody
   {
      private readonly String _query;

      internal QueryMessage( String query )
         : base( FrontEndMessageCode.Query )
      {
         this._query = query;
      }

      protected override Int32 CalculateBufferSize( BackendABIHelper args, ResizableArray<Byte> array )
      {
         return args.GetStringSize( this._query, array );
      }

      protected override void WriteMessageToBuffer( BackendABIHelper args, ResizableArray<Byte> array )
      {
         var idx = 0;
         array.Array.WritePgString( ref idx, args.Encoding, this._query );
      }
   }

   internal sealed class ParseMessage : FrontEndMessageWithCode
   {
      private readonly String _statementName;
      private readonly String _sql;
      private readonly Int32[] _typeIDs;

      internal ParseMessage( String sql, Int32[] paramIndices, Int32[] typeIDs, String statementName = null )
         : base( FrontEndMessageCode.Parse )
      {
         ArgumentValidator.ValidateNotNull( "SQL", sql );

         this._statementName = statementName;
         // Replace "blaa ? blaa2 ? blaa3" with "blaa $1 blaa2 $2 blaa3"
         var sb = new StringBuilder();
         var prev = 0;
         if ( paramIndices != null )
         {
            var curParam = 1;
            foreach ( var i in paramIndices )
            {
               sb.Append( sql.Substring( prev, i - prev ) )
               .Append( '$' ).Append( curParam++ );
               prev = i + 1;
            }
         }

         sb.Append( sql.Substring( prev ) );

         this._sql = sb.ToString();
         this._typeIDs = typeIDs;
      }

      protected override Int32 CalculateSize( BackendABIHelper args, ResizableArray<Byte> array )
      {
         return args.GetStringSize( this._statementName, array )
            + args.GetStringSize( this._sql, array )
            + sizeof( Int16 )
            + this._typeIDs.GetLengthOrDefault() * sizeof( Int32 );
      }

      protected override async Task PerformSendAfterWriteAsync(
         BackendABIHelper args,
         Stream stream,
         Int32 size,
         CancellationToken token,
         ResizableArray<Byte> buffer
         )
      {
         await args.WriteString( stream, this._statementName, token, buffer );
         await args.WriteString( stream, this._sql, token, buffer );

         var typesCount = this._typeIDs.GetLengthOrDefault();
         buffer.CurrentMaxCapacity = sizeof( Int16 ) + 4 * typesCount;
         var array = buffer.Array;
         var idx = 0;
         array.WritePgInt16( ref idx, typesCount );
         if ( typesCount > 0 )
         {
            foreach ( var typeID in this._typeIDs )
            {
               array.WritePgInt32( ref idx, typeID );
            }
         }
         await stream.WriteAsync( array, 0, idx, token );
      }

      // Too many task allocations to make this feasible, at least at the moment (maybe when ValueTask is visible in .NETStandard?)
      // Or make this use new WritePgString method for arrays...
      //private async Task SendSQL(
      //   MessageSendingArgs args,
      //   Stream stream
      //   )
      //{
      //   // Replace "blaa ? blaa2 ? blaa3" with "blaa $1 blaa2 $2 blaa3"
      //   var paramIndices = this._paramIndices;
      //   // Send first chunk
      //   var sql = this._sql;
      //   await args.WriteStringPart( stream, sql, 0, paramIndices.Length > 0 ? paramIndices[0] : sql.Length );

      //   for (var i = 0; i < paramIndices.Length; ++i )
      //   {
      //      // Send $<number>
      //      var idx = 0;
      //      args.Buffer.Array[0] = 0x24; // '$'
      //      args.Buffer.Array.WritePgIntTextual( ref idx, i );
      //      await stream.WriteAsync( args.Buffer.Array, 0, idx );
      //      // Send next chunk
      //      var nextStartIndex = paramIndices[i] + 1;
      //      var nextEndIndex = i < paramIndices.Length - 1 ? paramIndices[i + 1] : sql.Length;
      //      await args.WriteStringPart( stream, sql, nextStartIndex, nextEndIndex - nextStartIndex );
      //   }

      //   // Send terminating zero
      //   await args.WriteString( stream, null );
      // }
   }

   internal sealed class CloseMessage : FrontEndMessageWithSingleBody
   {
      internal static readonly CloseMessage UNNAMED_STATEMENT = new CloseMessage( true );
      internal static readonly CloseMessage UNNAMED_PORTAL = new CloseMessage( false );

      private readonly Boolean _isStatement;
      private readonly String _name;

      internal CloseMessage( Boolean isStatement, String name = null )
         : base( FrontEndMessageCode.Close )
      {
         this._isStatement = isStatement;
         this._name = name;
      }

      protected override Int32 CalculateBufferSize( BackendABIHelper args, ResizableArray<Byte> array )
      {
         return 1 + args.GetStringSize( this._name, array );
      }

      protected override void WriteMessageToBuffer( BackendABIHelper args, ResizableArray<Byte> array )
      {
         var idx = 0;
         array.Array
            .WriteByteToBytes( ref idx, (Byte) ( this._isStatement ? 'S' : 'P' ) )
            .WritePgString( ref idx, args.Encoding, this._name );
      }
   }


   internal sealed class BindMessage : FrontEndMessageWithCode
   {
      private readonly String _portalName;
      private readonly String _statementName;
      private readonly IEnumerable<StatementParameter> _params;
      private readonly (PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData)[] _types;
      private readonly BackendSizeInfo[] _preCreatedParamSizes;
      private readonly Boolean _disableBinarySend;
      private readonly Boolean _disableBinaryReceive;
      private readonly FormatCodeInfo _sendCodes;
      private readonly FormatCodeInfo _receiveCodes;

      internal BindMessage(
         IEnumerable<StatementParameter> paramz,
         Int32 paramCount,
         (PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData)[] types,
         Boolean disableBinarySend,
         Boolean disableBinaryReceive,
         String portalName = null,
         String statementName = null
         )
         : base( FrontEndMessageCode.Bind )
      {
         this._portalName = portalName;
         this._statementName = statementName;
         this._disableBinarySend = disableBinarySend;
         this._disableBinaryReceive = disableBinaryReceive;
         this._params = paramz;
         this._types = types;
         this._preCreatedParamSizes = new BackendSizeInfo[paramCount];
         this._sendCodes = this.GetFormatCodes( true );
         this._receiveCodes = this.GetFormatCodes( false );
      }

      protected override Int32 CalculateSize( BackendABIHelper args, ResizableArray<Byte> array )
      {
         var retVal = args.GetStringSize( this._portalName, array )
            + args.GetStringSize( this._statementName, array )
            + 6 // paramFormatCount, paramCount, columnFormatCount
            + this._sendCodes.Item2 * sizeof( Int16 )
            + this._receiveCodes.Item2 * sizeof( Int16 )
            + this.CalculateParamSizes( args );
         return retVal;
      }

      private Int32 CalculateParamSizes( BackendABIHelper args )
      {
         var paramCount = this._preCreatedParamSizes.Length;
         var size = 4 * paramCount; // Each parameter takes at least 4 bytes
         var i = 0;
         var formatCodes = this._sendCodes.Item1;
         foreach ( var param in this._params )
         {
            var val = param.ParameterValue;
            if ( val != null )
            {
               var thisType = this._types[i];
               // Change type if needed
               if ( !thisType.UnboundInfo.CLRType.Equals( val.GetType() ) )
               {
                  val = thisType.UnboundInfo.ChangeTypeFrameworkToPgSQL( val );
               }

               var thisFormat = formatCodes == null ? DataFormat.Text : formatCodes[i >= formatCodes.Length ? 0 : i];
               var thisSizeInfo = DataFormat.Text == thisFormat ?
                  thisType.UnboundInfo.GetBackendTextSize( thisType.BoundData, args, val, false ) :
                  thisType.UnboundInfo.GetBackendBinarySize( thisType.BoundData, args, val );

               this._preCreatedParamSizes[i] = (thisSizeInfo.Item1, thisSizeInfo.Item2, val);


               size += thisSizeInfo.Item1;
            }

            ++i;
         }
         return size;
      }

      protected override async Task PerformSendAfterWriteAsync(
         BackendABIHelper args,
         Stream stream,
         Int32 size,
         CancellationToken token,
         ResizableArray<Byte> array
         )
      {

         // Start building message
         await args.WriteString( stream, this._portalName, token, array );
         await args.WriteString( stream, this._statementName, token, array );

         // Write format info about parameters
         var formatCodes = await this.WriteFormatInfo( args, stream, token, true, array );

         // Write parameters
         var idx = 0;
         array.Array.WritePgInt16( ref idx, this._preCreatedParamSizes.Length );
         await stream.WriteAsync( array.Array, 0, idx, token );
         for ( var i = 0; i < this._preCreatedParamSizes.Length; ++i )
         {
            var thisSizeTuple = this._preCreatedParamSizes[i];
            var thisType = this._types[i];
            var thisStream = StreamFactory.CreateLimitedWriter(
               stream,
               thisSizeTuple.Item1 + sizeof( Int32 ),
               token,
               array
               );
            try
            {
               await thisType.UnboundInfo.WriteBackendValueCheckNull(
                  formatCodes == null ? DataFormat.Text : formatCodes[i >= formatCodes.Length ? 0 : i],
                  thisType.BoundData,
                  args,
                  thisStream,
                  thisSizeTuple.Item3,
                  (thisSizeTuple.Item1, thisSizeTuple.Item2),
                  false
                  );


            }
            finally
            {
               await thisStream.FlushAsync();
            }
         }

         // Write format info for columns
         await this.WriteFormatInfo( args, stream, token, false, array );
      }

      private FormatCodeInfo GetFormatCodes( Boolean isForWriting )
      {
         //formatCodesNumber = 0;
         //return null;
         DataFormat[] formatCodes;
         Int32 formatCodesNumber;
         // Prepare format information
         if (
            ( isForWriting && this._disableBinarySend )
            || ( !isForWriting && this._disableBinaryReceive )
            || this._types.All( t => !( isForWriting ? t.UnboundInfo.SupportsWritingBinaryFormat : t.UnboundInfo.SupportsReadingBinaryFormat ) ) )
         {
            // All parameters use text format
            formatCodesNumber = 0;
            formatCodes = null;
         }
         else if ( this._types.All( t => isForWriting ? t.UnboundInfo.SupportsWritingBinaryFormat : t.UnboundInfo.SupportsReadingBinaryFormat ) )
         {
            // All parameters use binary format
            formatCodesNumber = 1;
            formatCodes = new[] { DataFormat.Binary };
         }
         else
         {
            // Each parameter will use the most optimal format
            formatCodesNumber = this._types.Length;
            formatCodes = new DataFormat[formatCodesNumber];
            for ( var i = 0; i < formatCodes.Length; ++i )
            {
               var thisType = this._types[i];
               formatCodes[i] = ( isForWriting ? thisType.UnboundInfo.SupportsWritingBinaryFormat : thisType.UnboundInfo.SupportsReadingBinaryFormat ) ? DataFormat.Binary : DataFormat.Text;
            }
         }
         return (formatCodes, formatCodesNumber);
      }

      private async Task<DataFormat[]> WriteFormatInfo(
         BackendABIHelper args,
         Stream stream,
         CancellationToken token,
         Boolean isForWriting,
         ResizableArray<Byte> buffer
         )
      {
         var formatCodesTuple = isForWriting ? this._sendCodes : this._receiveCodes;
         var formatCodes = formatCodesTuple.Item1;

         buffer.CurrentMaxCapacity = sizeof( Int16 ) + sizeof( Int16 ) * ( formatCodes?.Length ?? 0 );
         var idx = 0;
         var array = buffer.Array;
         array.WritePgInt16( ref idx, formatCodesTuple.Item2 );

         if ( formatCodes != null )
         {
            // Format codes, if necessary
            for ( var i = 0; i < formatCodes.Length; ++i )
            {
               array.WritePgInt16( ref idx, (Int16) formatCodes[i] );
            }
         }

         await stream.WriteAsync( array, 0, idx, token );

         return formatCodes;
      }
   }

   internal sealed class DescribeMessage : FrontEndMessageWithSingleBody
   {
      internal static readonly DescribeMessage UNNAMED_STATEMENT = new DescribeMessage( true );
      internal static readonly DescribeMessage UNNAMED_PORTAL = new DescribeMessage( false );

      private readonly Boolean _isStatement;
      private readonly String _name;

      internal DescribeMessage( Boolean isStatement, String name = null )
         : base( FrontEndMessageCode.Describe )
      {
         this._isStatement = isStatement;
         this._name = name;
      }

      protected override Int32 CalculateBufferSize( BackendABIHelper args, ResizableArray<Byte> array )
      {
         return 1 + args.GetStringSize( this._name, array );
      }

      protected override void WriteMessageToBuffer( BackendABIHelper args, ResizableArray<Byte> array )
      {
         var idx = 0;
         array.Array
            .WriteByteToBytes( ref idx, (Byte) ( this._isStatement ? 'S' : 'P' ) )
            .WritePgString( ref idx, args.Encoding, this._name );
      }
   }

   internal sealed class ExecuteMessage : FrontEndMessageWithSingleBody
   {
      internal static readonly ExecuteMessage UNNAMED_EXEC_ALL = new ExecuteMessage();

      private readonly String _portalName;
      private readonly Int32 _maxRows;

      internal ExecuteMessage( String portalName = null, Int32 maxRows = 0 )
         : base( FrontEndMessageCode.Execute )
      {
         this._portalName = portalName;
         this._maxRows = maxRows;
      }

      protected override Int32 CalculateBufferSize( BackendABIHelper args, ResizableArray<Byte> array )
      {
         return args.GetStringSize( this._portalName, array ) + sizeof( Int32 );
      }

      protected override void WriteMessageToBuffer( BackendABIHelper args, ResizableArray<Byte> array )
      {
         var idx = 0;
         array.Array
            .WritePgString( ref idx, args.Encoding, this._portalName )
            .WritePgInt32( ref idx, this._maxRows );
      }
   }

   internal sealed class SSLRequestMessage : FrontEndMessage
   {
      internal static readonly SSLRequestMessage INSTANCE = new SSLRequestMessage();

      private SSLRequestMessage()
      {

      }

      protected override Int32 CalculateSize( BackendABIHelper args, ResizableArray<Byte> array )
      {
         return sizeof( Int32 );
      }

      protected override async Task DoSendMessageAsync(
         BackendABIHelper args,
         Stream stream,
         Int32 size,
         CancellationToken token,
         ResizableArray<Byte> array
         )
      {
         var idx = 0;
         // We don't have front end message code, so we need to write size ourselves.
         array.Array
            .WritePgInt32( ref idx, size )
            .WritePgInt32( ref idx, 80877103 );// As per spec
         await stream.WriteAsync( array.Array, 0, idx, token );
      }
   }

   internal sealed class CopyDataFrontEndMessage : FrontEndMessageWithCode
   {
      private readonly Byte[] _data;
      private readonly Int32 _offset;
      private readonly Int32 _count;

      public CopyDataFrontEndMessage( Byte[] data, Int32 offset, Int32 count )
         : base( FrontEndMessageCode.CopyData )
      {
         this._data = data;
         this._offset = offset;
         this._count = count;
      }

      protected override Int32 CalculateSize( BackendABIHelper args, ResizableArray<Byte> array )
      {
         return this._count - this._offset;
      }

      protected override async Task PerformSendAfterWriteAsync(
         BackendABIHelper args,
         Stream stream,
         Int32 size,
         CancellationToken token,
         ResizableArray<Byte> array
         )
      {
         await stream.WriteAsync( this._data, this._offset, this._count, token );
      }

   }


   internal enum FrontEndMessageCode : byte
   {
      Bind = (Byte) 'B',
      Close = (Byte) 'C',
      Describe = (Byte) 'D',
      Execute = (Byte) 'E',
      //FunctionCall = (Byte) 'F', // Deprecated
      Flush = (Byte) 'H',
      Parse = (Byte) 'P',
      Query = (Byte) 'Q',
      Sync = (Byte) 'S',
      Termination = (Byte) 'X',
      PasswordMessage = (Byte) 'p',
      CopyDone = (Byte) 'c',
      CopyData = (Byte) 'd',
   }
}

public static partial class E_PgSQL
{
   public static Byte[] WritePgInt16( this Byte[] array, ref Int32 idx, Int32 value )
   {
      array.WriteInt16BEToBytes( ref idx, (Int16) value );
      return array;
   }

   public static Byte[] WritePgString( this Byte[] array, ref Int32 idx, IEncodingInfo encoding, String value )
   {
      if ( !String.IsNullOrEmpty( value ) )
      {
         idx += encoding.Encoding.GetBytes( value, 0, value.Length, array, idx );
      }
      array[idx++] = 0;
      return array;
   }

   public static Int32 GetStringSize( this BackendABIHelper args, String str, ResizableArray<Byte> array )
   {
      var retVal = String.IsNullOrEmpty( str ) ? 1 : ( args.Encoding.Encoding.GetByteCount( str ) + 1 );
      array.CurrentMaxCapacity = retVal;
      return retVal;
   }

   public static async Task WriteString( this BackendABIHelper args, Stream stream, String str, CancellationToken token, ResizableArray<Byte> array )
   {
      //if ( !String.IsNullOrEmpty( str ) )
      //{
      //   await args.WriteStringPart( stream, str, 0, str.Length );
      //}
      //args.Buffer.Array[0] = 0;
      //// Send terminating zero
      //await stream.WriteAsync( args.Buffer.Array, 0, 1 );
      var idx = 0;
      array.Array.WritePgString( ref idx, args.Encoding, str );
      await stream.WriteAsync( array.Array, 0, idx, token );
   }

   //public static async Task WriteStringPart( this BackendABIHelper args, Stream stream, String str, Int32 stringStart, Int32 stringLength )
   //{
   //   if ( !String.IsNullOrEmpty( str ) && stringLength > 0 )
   //   {
   //      var size = args.Encoding.Encoding.GetBytes( str, stringStart, stringLength, args.Buffer.Array, 0 );
   //      await stream.WriteAsync( args.Buffer.Array, 0, size );
   //   }
   //}


}