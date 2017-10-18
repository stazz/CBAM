# CBAM.SQL.PostgreSQL.Implementation

This is the project containing implementation of [CBAM.SQL.PostgreSQL](../CBAM.SQL.PostgreSQL) project, for .NET Desktop and .NET Standard frameworks.
The `PgSQLConnectionPoolProvider` class contains methods related to obtaining the connection pools, and `PgSQLConnectionCreationInfo` class has all required API to initialize connection to PostgreSQL backend.

The typical usecase scenario for obtaining the connection pool is the following:
```csharp
using Microsoft.Extensions.Configuration; // For configuration
using CBAM.SQL.PostgreSQL; // For CBAM PostgreSQL types

var configData = new ConfigurationBuilder() // This line requires reference to Microsoft.Extensions.Configuration NuGet package
  .AddJsonFile( System.IO.Path.GetFullPath( "path/to/config/jsonfile" ) ) // This line requires reference to Microsoft.Extensions.Configuration.Json NuGet package
  .Build()
  .Get<PgSQLConnectionCreationInfoData>(); // This line requires reference to Microsoft.Extensions.Configuration.Binder NuGet package

// Create connection pool
Int32[] integers;
using ( var pool = PgSQLConnectionPoolProvider.Factory
  .BindCreationParameters( new PgSQLConnectionCreationInfo( configData ) )
  .CreateTimeoutingResourcePool()
  )
{

  // Quick example on using connection pool to execute "SELECT 1" statement, and print the result (number "1") to console
  integers = await pool.UseResourceAsync( async pgConnection =>
  {
     return await pgConnection
        .PrepareStatementForExecution( "SELECT 1" )
        .IncludeDataRowsOnly()
        .Select( async row => await row.GetValueAsync<Int32>( 0 ) )
        .ToArrayAsync();
  } );

}

// Elsewhere, e.g. maybe in a separate background thread/loop:
// This will close all connections that has been idle in a pool for over one minute
await pool.CleanUpAsync( TimeSpan.FromMinutes( 1 ) );
```

The configuration file in the example above should mimic the structure of `PgSQLConnectionCreationInfoData` class.
Here is one example of structurally valid configuration file:
```json
{
   "Connection": {
      "Host": "localhost",
      "Port": 5432
   },
   "Initialization": {
      "Database": {
         "Database": "my_database",
         "Username": "my_user",
         "Password": "my_password"
      }
   }
}
```
For a complete list of possible values and structure, see `PgSQLConnectionCreationInfoData` class and all of the classes it declares through properties.

# Distribution

See [NuGet package](http://www.nuget.org/packages/CBAM.SQL.PostgreSQL.Implementation) for binary distribution.