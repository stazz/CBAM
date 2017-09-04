# CBAM
Connection-Based Asynchronous Messaging (CBAM) framework provides asynchronous task-based API and implementation for workflow which communicates with e.g. SQL or LDAP backend processes.

## Projects
Currently there are 11 projects, and only SQL version of CBAM.Abstractions project exists.
The most interesting projects, from end-user point of view, are most likely CBAM.SQL.PostgreSQL.Implementation and CBAM.SQL.MSBuild.

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

## Portability
CBAM projects aim to be as portable as possible.
The CBAM.SQL.MSBuild project targets .NET Standard 1.3, while the other projects target .NET Standard 1.0 and .NET 4.0 as their most portable target frameworks.

## Tests

### CBAM.SQL.PostgreSQL.Tests
This test suite project for CBAM.SQL.PostgreSQL.* projects contains various unit tests using MSTest v2.

[Read more ->](./Source/CBAM.SQL.PostgreSQL.Tests)

# TODO
Once PostgreSQL stuff works good and solid, add CBAM.LDAP.* projects in order to provide async API for LDAP connections.
