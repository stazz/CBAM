# CBAM.SQL

This project expands and augments the API of [CBAM.Abstractions](../CBAM.Abstractions) project with SQL-specific API, but in such way that it is neutral to which exact SQL vendor underneath implements the API.
People familiar with JDBC will find familiar concepts in here.

The key types of this project are:
* `SQLConnection`: This interface binds all generic type parameters of `Connection` interface of [CBAM.Abstractions](../CBAM.Abstractions) project, and adds ability to get database metadata (via `DatabaseMetadata` interface) and query and set readonly and default transaction isolation of the connection.
* `DatabaseMetadata`: This interface provides direct API and indirect extension methods to query data about database: schemas, tables, columns, primary key constraints, and foreign key constraints.
* `SQLStatementBuilderInformation`: This interface has read-only API for building (prepared) SQL statements.
* `SQLStatementBuilder`: Obtaineable from vendor of `SQLConnection`, this provides read-write API for building (prepared) SQL statements. Note that just like in JDBC, the parameters for prepared statement SQL should be question mark (`?`) characters.

To find out more, examine the XML documentation in-place in the code files or via IDE.
The [CBAM.SQL.PostgreSQL](../CBAM.SQL.PostgreSQL) project provides PostgreSQL-specific extensions to the API defined in this project.
The [CBAM.SQL.PostgreSQL.Implementation](../CBAM.SQL.PostgreSQL.Implementation) project contains the concrete implementation of `SQLConnection`, along with `PgSQLConnectionPoolProvider` class which can be used to create connection pools that will expose connections that will be connected to PostgreSQL backend.

# Distribution
See [NuGet package](http://www.nuget.org/packages/CBAM.SQL) for binary distribution.