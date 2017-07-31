using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading;
using UtilPack;
using System.Threading.Tasks;
using CBAM.SQL.Implementation;
using CBAM.Abstractions.Implementation;
using CBAM.Tabular;
using CBAM.Tabular.Implementation;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   internal sealed class PgSQLDataRowStream : DataRowColumnSUKSWithConnectionFunctionality<PostgreSQLProtocol, StatementBuilder, StatementBuilderInformation, SQLStatementExecutionResult>
   {
      private readonly DataFormat _dataFormat;
      private DataRowObject _backendMessage;

      public PgSQLDataRowStream(
         PgSQLDataColumnMetaDataImpl metadata,
         Int32 thisStreamIndex,
         PgSQLDataRowStream[] allDataRowStreams,
         PostgreSQLProtocol protocol,
         ReservedForStatement reservedForStatement,
         RowDescription rowDescription
         ) : base( metadata, thisStreamIndex, protocol.Buffer, allDataRowStreams, protocol, reservedForStatement )
      {
         var fieldInfo = rowDescription.Fields[thisStreamIndex];
         this._dataFormat = fieldInfo.DataFormat;
      }

      public override async Task<Object> ConvertFromBytesAsync( Stream stream, Int32 byteCount )
      {
         return await this.ConnectionFunctionality.ConvertFromBytes( ( (PgSQLDataColumnMetaDataImpl) this.MetaData ).SQLTypeID, this._dataFormat, stream, byteCount );
      }

      protected override async Task<Object> ReadValueAsync( Int32 byteCount )
      {
         return await this.ConnectionFunctionality.ConvertFromBytes( ( (PgSQLDataColumnMetaDataImpl) this.MetaData ).SQLTypeID, this._dataFormat, this.ConnectionFunctionality.Stream, byteCount );
      }

      protected override async Task<Int32> ReadByteCountAsync()
      {
         return await this._backendMessage.ReadColumnByteCount( this.ConnectionFunctionality.MessageIOArgs, this.ConnectionFunctionality.Stream, this.ConnectionFunctionality.CurrentCancellationToken, this.ColumnIndex, this.ByteArray );
      }

      protected override async Task<Int32> PerformReadFromStreamAsync( Byte[] array, Int32 offset, Int32 count )
      {
         return await this.ConnectionFunctionality.Stream.ReadAsync( array, offset, count, this.ConnectionFunctionality.CurrentCancellationToken );
      }

      public void Reset( DataRowObject nextRow )
      {
         this.Reset();
         Interlocked.Exchange( ref this._backendMessage, nextRow );
      }
   }

   internal sealed class PgSQLDataRowMetaDataImpl : DataRowMetaDataImpl //, PgSQLDataRowMetaData
   {

      public PgSQLDataRowMetaDataImpl(
         PgSQLDataColumnMetaDataImpl[] columnMetaDatas
         )
         : base( columnMetaDatas )
      {
      }
   }

   internal sealed class PgSQLDataColumnMetaDataImpl : AbstractDataColumnMetaData, PgSQLDataColumnMetaData
   {

      public PgSQLDataColumnMetaDataImpl(
         Int32 typeID,
         (PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData) typeInfo,
         String label
         ) : base( typeInfo.UnboundInfo?.CLRType, label )
      {
         this.SQLTypeID = typeID;
         this.TypeInfo = typeInfo;
      }

      public override Object ChangeType( Object value, Type targetType )
      {
         var typeInfo = this.TypeInfo;
         return typeInfo.UnboundInfo.ChangeTypePgSQLToFramework( typeInfo.BoundData, value, targetType );
      }

      public Int32 SQLTypeID { get; }

      // TODO consider that this should be exposed as-it, since both PgSQLTypeFunctionality and PgSQLTypeDatabaseData are in CBAM.SQL.PostgreSQL project.
      private (PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData) TypeInfo { get; }
   }
}
