using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using TypeInfo = System.ValueTuple<CBAM.SQL.PostgreSQL.PgSQLTypeFunctionality, CBAM.SQL.PostgreSQL.PgSQLTypeDatabaseData>;
using TypeInfoWithCLRType = System.ValueTuple<System.Type, CBAM.SQL.PostgreSQL.PgSQLTypeFunctionality, CBAM.SQL.PostgreSQL.PgSQLTypeDatabaseData>;
using TStaticTypeCacheValue = System.Collections.Generic.IDictionary<System.String, CBAM.SQL.PostgreSQL.PgSQLTypeDatabaseData>;
using UtilPack;
using CBAM.SQL.Implementation;

namespace CBAM.SQL.PostgreSQL.Implementation
{
   using SQLConnectionFunctionality = Abstractions.Connection<SQLStatementBuilder, SQLStatementBuilderInformation, String, SQLStatementExecutionResult, SQLConnectionVendorFunctionality>;

   internal class TypeRegistryImpl : TypeRegistry
   {
      private const Char ARRAY_PREFIX = '_';

      private readonly IDictionary<Int32, TypeFunctionalityInformation> _typeInfos;
      private readonly IDictionary<Type, TypeFunctionalityInformation> _typeInfosByCLRType;

      private readonly SQLConnectionVendorFunctionality _vendorFunctionality;
      private readonly SQLConnectionFunctionality _connectionFunctionality;

      public TypeRegistryImpl(
         SQLConnectionVendorFunctionality vendorFunctionality,
         SQLConnectionFunctionality connectionFunctionality
         )
      {
         this._vendorFunctionality = ArgumentValidator.ValidateNotNull( nameof( vendorFunctionality ), vendorFunctionality );
         this._connectionFunctionality = ArgumentValidator.ValidateNotNull( nameof( connectionFunctionality ), connectionFunctionality );

         this._typeInfos = new Dictionary<Int32, TypeFunctionalityInformation>();
         this._typeInfosByCLRType = new Dictionary<Type, TypeFunctionalityInformation>();
      }

      public async ValueTask<Int32> AddTypeFunctionalitiesAsync( params (String DBTypeName, Type CLRType, Func<PgSQLTypeDatabaseData, TypeFunctionalityCreationResult> FunctionalityCreator)[] functionalities )
      {
         var retVal = 0;
         if ( functionalities != null )
         {
            var dic = functionalities.ToDictionary_Overwrite( tuple => tuple.DBTypeName, tuple => tuple );
            retVal = dic.Count;
            this.AssignTypeData(
               await this.ReadTypeDataFromServer( dic.Keys ),
               typeName => dic[typeName].CLRType,
               tuple => dic[tuple.DBTypeName].FunctionalityCreator( tuple.BoundData )
               );
         }

         return retVal;
      }

      public TypeFunctionalityInformation TryGetTypeInfo( Int32 typeID )
      {
         this._typeInfos.TryGetValue( typeID, out var retVal );
         return retVal;
      }

      public TypeFunctionalityInformation TryGetTypeInfo( Type clrType )
      {
         TypeFunctionalityInformation retVal;
         if ( clrType != null )
         {
            KeyValuePair<Type, TypeFunctionalityInformation> kvp;
            if ( !this._typeInfosByCLRType.TryGetValue( clrType, out retVal ) )
            {
               if ( ( kvp = this.TryFindByParent( clrType ) ).Value != null )
               {
                  retVal = kvp.Value;
               }
               else
               {
                  retVal = default;
               }
            }
         }
         else
         {
            retVal = default;
         }
         return retVal;
      }

      private KeyValuePair<Type, TypeFunctionalityInformation> TryFindByParent( Type clrType )
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
         );
      }

      public async ValueTask<TStaticTypeCacheValue> ReadTypeDataFromServer(
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
         Func<String, Type> clrTypeExtractor,
         Func<(String DBTypeName, PgSQLTypeDatabaseData BoundData), TypeFunctionalityCreationResult> funcExtractor
         )
      {
         foreach ( var kvp in typeData )
         {
            var typeName = kvp.Key;
            var boundData = kvp.Value;
            PgSQLTypeFunctionality thisFunc;
            Boolean isDefaultForThisCLRType;
            Type clrType;
            if ( typeName[0] == ARRAY_PREFIX )
            {
               clrType = clrTypeExtractor( typeName.Substring( 1 ) );
               thisFunc = new PgSQLTypeFunctionalityForArrays( this, ref clrType, kvp.Value.ElementTypeID );
               clrType = clrType.MakeArrayType();
               isDefaultForThisCLRType = true;
            }
            else
            {
               clrType = clrTypeExtractor( typeName );
               var result = funcExtractor( (typeName, boundData) );
               (thisFunc, isDefaultForThisCLRType) = (result.TypeFunctionality, result.IsDefaultForCLRType);
            }
            if ( thisFunc != null )
            {
               var typeInfo = new TypeFunctionalityInformation( clrType, thisFunc, boundData );
               this._typeInfos[boundData.TypeID] = typeInfo;
               if ( isDefaultForThisCLRType || !this._typeInfosByCLRType.ContainsKey( clrType ) )
               {
                  this._typeInfosByCLRType[clrType] = typeInfo;
               }
            }
         }
      }
   }
}
