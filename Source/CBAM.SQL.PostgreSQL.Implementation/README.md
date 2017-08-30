# CBAM.SQL.PostgreSQL.Implementation

This is the project containing implementation of [CBAM.SQL.PostgreSQL](../CBAM.SQL.PostgreSQL) project, for .NET Desktop and .NET Standard frameworks.
The `PgSQLConnectionPoolProvider` class contains methods related to obtaining the connection pools, and `PgSQLConnectionCreationInfo` class has all required API to initialize connection to PostgreSQL backend.

The typical usecase scenario for obtaining the connection pool is the following:
```csharp
using CBAM.SQL.PostgreSQL;

// ...

var configData = new ConfigurationBuilder() // This line requires reference to Microsoft.Extensions.Configuration NuGet package
  .AddJsonFile( System.IO.Path.GetFullPath( "path/to/config/jsonfile" ) ) // This line requires reference to Microsoft.Extensions.Configuration.Json NuGet package
  .Build()
  .Get<PgSQLConnectionCreationInfoData>(); // This line requires reference to Microsoft.Extensions.Configuration.Binder NuGet package

// Create connection pool
var pool = PgSQLConnectionPoolProvider.Instance.CreateTimeoutingResourcePool( new PgSQLConnectionCreationInfo( configData ) );

// Quick example on using connection pool to execute "SELECT 1" statement, and print the result (number "1") to console
pool.UseResourceAsync( async pgConnection => pgConnection
  .PrepareStatementForExecution( "SELECT 1" )
  .EnumerateSQLRowsAsync( async row => Console.WriteLine( await row.GetValueAsync<Int32>( 0 ) ) )
  );
```

The configuration file in the example above should mimic the structure of `PgSQLConnectionCreationInfoData` class.
For example, it could be something like this:
```json
{
   "Connection": {
      "Host": "127.0.0.1",
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