[![Build status](https://ci.appveyor.com/api/projects/status/e9yafu9qvuup6kg6/branch/develop?svg=true)](https://ci.appveyor.com/project/stazz/cbam/branch/develop)
[![Code coverage](https://codecov.io/gh/stazz/CBAM/branch/develop/graph/badge.svg)](https://codecov.io/gh/stazz/CBAM)

# CBAM
Connection-Based Asynchronous Messaging (CBAM) framework provides asynchronous task-based API and implementation for workflow which communicates with remote resources, e.g. SQL, HTTP, LDAP servers, etc.

# Main Principles

The [CBAM.Abstractions](#cbamabstractions) project defines an interface `Connection` which represents stateful or stateless connection to some kind of remote endpoint, e.g. SQL/HTTP server.
The `Connection` interface allows to prepare a statement for execution: e.g. string containing SQL, an object which contains HTTP request, etc.
This prepared statement is exposed as [`IAsyncEnumerable<T>`](https://github.com/CometaSolutions/UtilPack/tree/develop/Source/UtilPack.AsyncEnumeration), which can be asynchronously enumerated using e.g. `EnumerateSequentiallyAsync` extension method.
Each item encountered during enumeration may be e.g. SQL statement execution result (statement execution information, or data row), or HTTP response (with all headers read, but content not read).

# Examples
With CBAM as common framework, it is possible to interact with remote in various protocols.

[HTTP](./Source/CBAM.HTTP.Implementation) example:
```csharp
var response = await new SimpleHTTPConfiguration()
{
  Host = "www.google.com",
  Port = 443,
  IsSecure = true
}.CreatePoolAndReceiveTextualResponseAsync( HTTPFactory.CreateGETRequest( "/" ) );
// Access content as string
var stringContents = response.TextualContent
// Access headers
var headerDictionary = response.Headers
```

[PostgreSQL](./Source/CBAM.SQL.PostgreSQL.Implementation) example:
```csharp
// SQL example
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
  .CreateTimeoutingResourcePool()) 
{
  // Quick example on using connection pool to execute "SELECT 1" statement, and print the result (number "1") to console
  // The prepared statements are also fully supported, but out of scope from this example
  // The code below only requires CBAM.SQL.PostgreSQL project, the CBAM.SQL.PostgreSQL.Implementation is only for access of PgSQLConnectionPoolProvider.Factory
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

[NATS](./Source/CBAM.NATS.Implementation) example:
```csharp
var pool = NATSConnectionPoolProvider.Factory.BindCreationParameters( new NATSConnectionCreationInfo( new NATSConnectionCreationInfoData()
{
  Connection = new NATSConnectionConfiguration()
  {
    Host = "localhost",
    Port = 4222,
    ConnectionSSLMode = ConnectionSSLMode.NotRequired
  }
} ) ).CreateOneTimeUseResourcePool().WithoutExplicitAPI();

var messageContentsByteArray = await pool.UseResourceAsync( async natsConnection =>
{
  return await natsConnection.SubscribeAsync( "MySubject" )
    .Select( message => message.CreateDataArray() )
    .FirstAsync();
} );
```

## Projects
The projects are contained in [Source/Code](./Source/Code) folder, while the tests are in [Source/Tests](./Source/Tests) folder.

The most interesting projects, from end-user point of view, are most likely [CBAM.SQL.PostgreSQL.Implementation](./Source/Code/CBAM.SQL.PostgreSQL.Implementation), [CBAM.SQL.ExecuteStatements.Application](./Source/Code/CBAM.SQL.ExecuteStatements.Application), [CBAM.HTTP.Implementation](./Source/Code/CBAM.HTTP.Implementation), and [CBAM.NATS.Implementation](./Source/Code/CBAM.NATS.Implementation).

## Portability
CBAM projects aim to be as portable as possible.
The CBAM.SQL.MSBuild project targets .NET Standard 1.3, while the other projects target .NET Standard 1.0 and .NET 4.0 as their most portable target frameworks.
