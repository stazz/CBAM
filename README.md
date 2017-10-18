# CBAM
Connection-Based Asynchronous Messaging (CBAM) framework provides asynchronous task-based API and implementation for workflow which communicates with remote resources, e.g. SQL, HTTP, LDAP servers, etc.

The [CBAM.SQL.PostgreSQL.Implementation](#cbamsqlpostgresqlimplementation) project allows one to do this:
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
  .CreateTimeoutingResourcePool()) 
{
  // Quick example on using connection pool to execute "SELECT 1" statement, and print the result (number "1") to console
  integers = await pool.UseResourceAsync( async pgConnection =>
  {
     return await pgConnection
        .PrepareStatementForExecution( "SELECT 1" )
        .IncludeDataRowsOnly()
        .Select( async row => await row.GetValueAsync<Int32>( 0 ) )
        .ToArray();
  } );
}

// Elsewhere, e.g. maybe in a separate background thread/loop:
// This will close all connections that has been idle in a pool for over one minute
await pool.CleanUpAsync( TimeSpan.FromMinutes( 1 ) );
```

While the [CBAM.HTTP.Implementation](#cbamhttpimplementation) project allows one to do this:
```csharp
using System.Collections.Concurrent;
using UtilPack; // For "CreateRepeater" extension method
using UtilPack.ResourcePooling.NetworkStream; // For NetworkStreamFactory
using CBAM.HTTP; // For HTTP-related

// Store all responses as strings in this simple example
ConcurrentBag<String> responseTexts;
using ( var pool = new NetworkStreamFactory().BindCreationParameters(
    new HTTPConnectionEndPointConfigurationData()
    {
      Host = "www.google.com",
      IsSecure = true
    }.CreateNetworkStreamFactoryConfiguration()
  ).CreateTimeoutingAndLimitedResourcePool( 10 ) ) // Cache streams and their idle time, and limit maximum concurrent connections to 10
{
  // Create CBAM HTTP connection
  var httpConnection = pool.CreateNewHTTPConnection();

  // Send 20 requests to "/" in parallel and process each response
  // Note that only 10 connections will be opened, since the pool is limited to 10 concurrent connections
  responseTexts = await httpConnection.PrepareStatementForExecution( 
    HTTPMessageFactory.CreateGETRequest( "/" ).CreateRepeater( 20 ) // Repeat same request 20 times
    ).ToConcurrentBagAsync( async response =>
    {
      // Read whole response content into byte array and get string from it (assume UTF-8 encoding for this simple example)
      return Encoding.UTF8.GetString( await response.Content.ReadAllContentIfKnownSizeAsync() );
    } );
}

// Now the responseTexts bag will contain 20 response texts.
```

## Projects
Currently there are 14 projects, and only SQL version of CBAM.Abstractions project exists.
The most interesting projects, from end-user point of view, are most likely CBAM.SQL.PostgreSQL.Implementation, CBAM.SQL.MSBuild, and CBAM.HTTP.Implementation.

### CBAM.SQL.PostgreSQL.Implementation

This project contains concrete implementation of various CBAM APIs and abstractions, so that the user can establish connections to PostgreSQL backend and execute SQL statements.

[Read more ->](./Source/CBAM.SQL.PostgreSQL.Implementation)

### CBAM.SQL.MSBuild

This project contains a task which can be used to execute SQL statements from files against the database.

[Read more ->](./Source/CBAM.SQL.MSBuild)

### CBAM.SQL.PostgreSQL

This project contains PostgreSQL-specific API, such as notifications and DB type system, by specializing and extending the interfaces of CBAM.SQL.

[Read more ->](./Source/CBAM.SQL.PostgreSQL)

### CBAM.SQL.PostgreSQL.JSON

This project provides natural support for ```json``` and ```jsonb``` PostgreSQL types.

[Read more ->](./Source/CBAM.SQL.PostgreSQL.JSON)

### CBAM.SQL

This project contains SQL-related API that is common for all RDBMS vendors, by specializing and augmenting the interfaces of CBAM.Abstractions.

[Read more ->](./Source/CBAM.SQL)

### CBAM.Abstractions
This project provides interfaces and abstractions which are common for all connection-based async messaging, such as interacting with SQL database or LDAP server.

[Read more ->](./Source/CBAM.Abstractions)

### CBAM.Abstractions.Implementation
This project has skeleton implementations for interfaces in CBAM.Abstractions project.

[Read more ->](./Source/CBAM.Abstractions.Implementation)

### CBAM.Abstractions.Implementation.Tabular

This project extends the data column class in [UtilPack.TabularData](https://github.com/CometaSolutions/UtilPack/tree/develop/Source/UtilPack.TabularData) to use the connection implementation class in CBAM.Abstractions.Implementation project.

[Read more ->](./Source/CBAM.Abstractions.Implementation)

### CBAM.SQL.Implementation
This project provides skeleton implementations for interfaces in CBAM.SQL and uses the skeleton implementations provided in CBAM.Tabular.Implementation.

[Read more ->](./Source/CBAM.SQL.Implementation)

### CBAM.HTTP
This project provides API for HTTP-oriented Connection from CBAM.Abstractions, along with minimalistic HTTP API.

[Read more ->](./Source/CBAM.HTTP)

### CBAM.HTTP.Implementation
This project provides implementation for CBAM types in CBAM.HTTP, along with factory extension method to create HTTP CBAM connections.

[Read more ->](./Source/CBAM.HTTP.Implementation)

## Portability
CBAM projects aim to be as portable as possible.
The CBAM.SQL.MSBuild project targets .NET Standard 1.3, while the other projects target .NET Standard 1.0 and .NET 4.0 as their most portable target frameworks.

## Tests

### CBAM.SQL.PostgreSQL.Tests
This test suite project for CBAM.SQL.PostgreSQL.* projects contains various unit tests using MSTest v2.

[Read more ->](./Source/CBAM.SQL.PostgreSQL.Tests)

# TODO
Once PostgreSQL stuff works good and solid, add CBAM.LDAP.* projects in order to provide async API for LDAP connections.
