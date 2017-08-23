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
using UtilPack.TabularData;
using CBAM.Abstractions.Implementation.Tabular;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   internal sealed class PgSQLDataRowColumn : DataColumnSUKSWithConnectionFunctionality<PostgreSQLProtocol, SQLStatementBuilder, SQLStatementBuilderInformation, String, SQLStatementExecutionResult, PgSQLConnectionVendorFunctionality>
   {
      private readonly DataFormat _dataFormat;
      private DataRowObject _backendMessage;

      public PgSQLDataRowColumn(
         PgSQLDataColumnMetaDataImpl metadata,
         Int32 thisStreamIndex,
         PgSQLDataRowColumn previousColumn,
         PostgreSQLProtocol protocol,
         ReservedForStatement reservedForStatement,
         RowDescription.FieldData fieldData
         ) : base( metadata, thisStreamIndex, previousColumn, protocol, reservedForStatement )
      {
         this._dataFormat = ArgumentValidator.ValidateNotNull( nameof( fieldData ), fieldData ).DataFormat;
      }

      protected override async ValueTask<Object> ReadValueAsync( Int32 byteCount )
      {
         return await this.ConnectionFunctionality.ConvertFromBytes( ((PgSQLDataColumnMetaDataImpl)this.MetaData).SQLTypeID, this._dataFormat, null, byteCount );
      }

      protected override async ValueTask<Int32> ReadByteCountAsync()
      {
         return await this._backendMessage.ReadColumnByteCount( this.ConnectionFunctionality.MessageIOArgs, this.ConnectionFunctionality.Stream, this.ConnectionFunctionality.CurrentCancellationToken, this.ColumnIndex, this.ConnectionFunctionality.Buffer );
      }

      protected override async ValueTask<Int32> ReadFromStreamWhileReservedAsync( Byte[] array, Int32 offset, Int32 count )
      {
         return await this.ConnectionFunctionality.Stream.ReadAsync( array, offset, count, this.ConnectionFunctionality.CurrentCancellationToken );
      }

      public void Reset( DataRowObject nextRow )
      {
         this.Reset();
         Interlocked.Exchange( ref this._backendMessage, nextRow );
      }
   }

   internal sealed class PgSQLDataRowMetaDataImpl : DataRowMetaDataImpl<AsyncDataColumnMetaData> //, PgSQLDataRowMetaData
   {

      public PgSQLDataRowMetaDataImpl(
         PgSQLDataColumnMetaDataImpl[] columnMetaDatas
         )
         : base( columnMetaDatas )
      {
      }
   }

   internal sealed class PgSQLDataColumnMetaDataImpl : AbstractAsyncDataColumnMetaData, PgSQLDataColumnMetaData
   {

      private readonly PostgreSQLProtocol _connectionFunctionality;
      private readonly DataFormat _dataFormat;

      public PgSQLDataColumnMetaDataImpl(
         PostgreSQLProtocol connectionFunctionality,
         DataFormat dataFormat,
         Int32 typeID,
         (Type CLRType, PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData) typeInfo,
         String label
         ) : base( typeInfo.CLRType ?? typeof( String ), label )
      {
         this._connectionFunctionality = ArgumentValidator.ValidateNotNull( nameof( connectionFunctionality ), connectionFunctionality );
         this._dataFormat = dataFormat;
         this.SQLTypeID = typeID;
         this.TypeInfo = typeInfo;
      }

      public override Object ChangeType( Object value, Type targetType )
      {
         var typeInfo = this.TypeInfo;
         return typeInfo.UnboundInfo.ChangeTypePgSQLToFramework( typeInfo.BoundData, value, targetType );
      }

      public override ValueTask<Object> ConvertFromBytesAsync( Stream stream, Int32 byteCount )
      {
         return this._connectionFunctionality.ConvertFromBytes( this.SQLTypeID, this._dataFormat, stream, byteCount );
      }

      public Int32 SQLTypeID { get; }

      private (Type CLRType, PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData) TypeInfo { get; }
   }
}
