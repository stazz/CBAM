# CBAM
Connection-Based Asynchronous Messaging (CBAM) framework provides asynchronous task-based API and implementation for workflow which communicates with e.g. SQL or LDAP processes.

## Portability
CBAM projects aim to be as portable as possible.
Projects aimed to be used as a normal dependency are targeting .NET Standard 1.0, .NET 4.0 and .NET 4.5.
Projects aimed to be used as part of build process are targeting .NET Standard 1.3.

# CBAM.Abstractions
This project provides interfaces and abstractions which are common for all connection-based async messaging, such as interacting with SQL database or LDAP server.

NuGet link: https://www.nuget.org/packages/CBAM.Abstractions .

# CBAM.Abstractions.Implementation
This project has skeleton implementations for interfaces in CBAM.Abstractions.

NuGet link: https://www.nuget.org/packages/CBAM.Abstractions.Implementation .

# CBAM.Tabular
This project has provides interfaces which are suitable for retrieving data in tabular format (async-gettable rows with async-gettable column values).

NuGet link: https://www.nuget.org/packages/CBAM.Tabular .

# CBAM.Tabular.Implementation
This project has skeleton implementations for interfaces in CBAM.Tabular, and also uses CBAM.Abstractions.Implementation skeleton implementations in order to tie CBAM.Tabular with CBAM.Abstractions.

NuGet link: https://www.nuget.org/packages/CBAM.Tabular.Implementation .

# CBAM.SQL
This project specializes and augments the interfaces of CBAM.Abstractions in such way that interacting with SQL databases would be easy and natural.
The CBAM.Tabular project is used to provide access to data rows returned by SQL database.

NuGet link: https://www.nuget.org/packages/CBAM.SQL .

# CBAM.SQL.Implementation
This project provides skeleton implementations for interfaces in CBAM.SQL and uses the skeleton implementations provided in CBAM.Tabular.Implementation.

NuGet link: https://www.nuget.org/packages/CBAM.SQL.Implementation .

# CBAM.SQL.PostgreSQL
This project specializes and augments the interfaces of CBAM.SQL with PostgreSQL-specific features, such as notifications and type system.

NuGet link: https://www.nuget.org/packages/CBAM.PostgreSQL .

# CBAM.SQL.PostgreSQL.Implementation
This project provides PostgreSQL-specific implementation for CBAM.SQL and CBAM.SQL.PostgreSQL projects, exposing API to create connection pools which result in PostgreSQL-specific connections.
PgSQLConnectionPool is a good class to start exploring this project.

NuGet link: https://www.nuget.org/packages/CBAM.PostgreSQL.Implementation .

Since implementation requires to communicate with backend over Sockets, this project targets .NET Standard 1.3, and also .NET Core App 1.1.
The .NET Core App dependency is in order to provide SSL stream functionality right in this project, however it is still possible to customize SSL stream creation via callbacks.

This project should only be used by the code actually performing initialization of SQL connections - all the PostgreSQL-specific API is availabe in CBAM.SQL.PostgreSQL project, which targets .NET Standard 1.0.

# CBAM.SQL.PostgreSQL.JSON
This project provides extension methods to enable support for ```json``` and ```jsonb``` PostgreSQL types.
These extension methods are available for connection pools and connections.
The ```json``` and ```jsonb``` types will be directly deserialized to Newtonsoft.JSON ```JToken``` objects.

NuGet link: https://www.nuget.org/packages/CBAM.SQL.PostgreSQL.JSON .

# CBAM.MSBuild.Abstractions
This project provides abstract class AbstractCBAMConnectionUsingTask which all of the MSBuild tasks that intend to use CBAM connections should derive from.
It allows specification of NuGet package containing actual implementation of ConnectionPoolProvider interface in CBAM.Abstractions, which is then used to create connection pool, which is then used to create connection and pass it on to abstract method.

# CBAM.SQL.MSBuild
This projects provides abstract class AbstractSQLConnectionUsingTask which constraints the connection type to SQLConnection, and also out-of-the-box ready-to-be-used task ExecuteSQLStatementsTask.
The ExecuteSQLStatementsTask takes a path to file containing SQL statements, and then executes them.
The ExecuteSQLStatementsTask task should be run using https://www.nuget.org/packages/UtilPack.NuGet.MSBuild/ .

# CBAM.SQL.PostgreSQL.Tests
The test suite project for CBAM.SQL.PostgreSQL.* projects.
In order to run the tests, add ```test_config.json``` and ```test_config_ssl.json``` configuration files to the project directory.
These files will be used to create ```PgSQLConnectionCreationInfoData``` object required to connect to database.

# TODO
Adding documentation is top priority now.
Once PostgreSQL stuff works good and solid, add CBAM.LDAP.* projects in order to provide async API for LDAP connections.
