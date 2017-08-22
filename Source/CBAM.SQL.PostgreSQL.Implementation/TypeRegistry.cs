using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using TypeInfo = System.ValueTuple<CBAM.SQL.PostgreSQL.PgSQLTypeFunctionality, CBAM.SQL.PostgreSQL.PgSQLTypeDatabaseData>;
using TStaticTypeCacheValue = System.Collections.Generic.IDictionary<System.String, CBAM.SQL.PostgreSQL.PgSQLTypeDatabaseData>;
using UtilPack;
using CBAM.SQL.Implementation;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   using SQLConnectionFunctionality = Abstractions.Connection<SQLStatementBuilder, SQLStatementBuilderInformation, String, SQLStatementExecutionResult, SQLConnectionVendorFunctionality>;

   internal class TypeRegistryImpl : TypeRegistry
   {
      private const Char ARRAY_PREFIX = '_';

      private readonly IDictionary<Int32, TypeInfo> _typeInfos;
      private readonly IDictionary<Type, TypeInfo> _typeInfosByCLRType;

      private readonly SQLConnectionVendorFunctionality _vendorFunctionality;
      private readonly SQLConnectionFunctionality _connectionFunctionality;

      public TypeRegistryImpl(
         SQLConnectionVendorFunctionality vendorFunctionality,
         SQLConnectionFunctionality connectionFunctionality
         )
      {
         this._vendorFunctionality = ArgumentValidator.ValidateNotNull( nameof( vendorFunctionality ), vendorFunctionality );
         this._connectionFunctionality = ArgumentValidator.ValidateNotNull( nameof( connectionFunctionality ), connectionFunctionality );

         this._typeInfos = new Dictionary<Int32, TypeInfo>();
         this._typeInfosByCLRType = new Dictionary<Type, TypeInfo>();
      }

      public async Task AddTypeFunctionalitiesAsync( params (String DBTypeName, Func<(TypeRegistry TypeRegistry, PgSQLTypeDatabaseData DBTypeInfo), (PgSQLTypeFunctionality UnboundFunctionality, Boolean IsDefaultForCLRType)> FunctionalityCreator)[] functionalities )
      {
         if ( functionalities != null )
         {
            var dic = functionalities.ToDictionary_Overwrite( tuple => tuple.DBTypeName, tuple => tuple.FunctionalityCreator );
            this.AssignTypeData(
               await this.ReadTypeDataFromServer( dic.Keys ),
               tuple => dic[tuple.DBTypeName]( (this, tuple.BoundData) )
               );
         }
      }

      public (PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData) GetTypeInfo( Int32 typeID )
      {
         this._typeInfos.TryGetValue( typeID, out TypeInfo retVal );
         return retVal;
      }

      public (PgSQLTypeFunctionality UnboundInfo, PgSQLTypeDatabaseData BoundData) GetTypeInfo( Type clrType )
      {
         return clrType != null && (
            this._typeInfosByCLRType.TryGetValue( clrType, out var retVal )
            || ( retVal = this.TryFindByParent( clrType ) ).Item1 != null
            ) ? retVal : default( TypeInfo );
      }

      private TypeInfo TryFindByParent( Type clrType )
      {
         var child = clrType
#if !NET40 && !NET45
         .GetTypeInfo()
#endif
         ;
         return this._typeInfosByCLRType.FirstOrDefault( kvp => kvp.Key
#if !NET40 && !NET45
         .GetTypeInfo()
#endif
         .IsAssignableFrom( child )
         ).Value;
      }

      public Int32 TypeInfoCount => this._typeInfos.Count;

      public async Task<TStaticTypeCacheValue> ReadTypeDataFromServer(
         IEnumerable<String> typeNames
         )
      {
         var types = new Dictionary<String, PgSQLTypeDatabaseData>();
         await this._connectionFunctionality.PrepareStatementForExecution(
               "SELECT typname, oid, typdelim, typelem\n" +
               "FROM pg_type\n" +
               "WHERE typname IN (" + String.Join( ", ", typeNames.Select( typename =>
               {
                  typename = this._vendorFunctionality.EscapeLiteral( typename );
                  return "'" + typename + "', '" + ARRAY_PREFIX + typename + "'";
               } ) ) + ")\n"
            ).EnumerateSQLRowsAsync( async row =>
              {
                 // We need to get all values as strings, since we might not have type mapping yet (we might be building it right here)
                 var typeName = await row.GetValueAsync<String>( 0 );
                 var typeID = Int32.Parse( await row.GetValueAsync<String>( 1 ) );
                 var delimiter = ( await row.GetValueAsync<String>( 2 ) );
                 var elementTypeID = Int32.Parse( await row.GetValueAsync<String>( 3 ) );
                 types.Add( typeName, new PgSQLTypeDatabaseData( typeName, typeID, delimiter, elementTypeID ) );
              } );
         return types;
      }

      public void AssignTypeData(
         TStaticTypeCacheValue typeData,
         Func<(String DBTypeName, PgSQLTypeDatabaseData BoundData), (PgSQLTypeFunctionality UnboundFunctionality, Boolean IsDefaultForCLRType)> funcExtractor
         )
      {
         foreach ( var kvp in typeData )
         {
            var typeName = kvp.Key;
            var boundData = kvp.Value;
            PgSQLTypeFunctionality thisFunc;
            Boolean isDefaultForThisCLRType;
            if ( typeName[0] == ARRAY_PREFIX )
            {
               thisFunc = new PgSQLTypeFunctionalityForArrays( this, funcExtractor( (typeName.Substring( 1 ), boundData) ).UnboundFunctionality, kvp.Value.ElementTypeID );
               isDefaultForThisCLRType = true;
            }
            else
            {
               (thisFunc, isDefaultForThisCLRType) = funcExtractor( (typeName, boundData) );
            }
            var thisTypeInfo = (thisFunc, boundData);
            this._typeInfos[boundData.TypeID] = thisTypeInfo;

            var clrType = thisFunc.CLRType;
            if ( isDefaultForThisCLRType || !this._typeInfosByCLRType.ContainsKey( clrType ) )
            {
               this._typeInfosByCLRType[clrType] = thisTypeInfo;
            }
         }
      }
   }
}
