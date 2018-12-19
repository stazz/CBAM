# CBAM.SQL.PostgreSQL

This project contains API for communicating with PostgreSQL backend process, augmenting types of [CBAM.SQL](../CBAM.SQL) project with PostgreSQL-specific functionality.
The main point of interest is `PgSQLConnection` interface, which extends general-purpose `SQLConnection` interface of [CBAM.SQL](../CBAM.SQL) project.
The `PgSQLConnection` interface provides such things as event for receiving PostgreSQL notifications, and a `TypeRegistry` interface to interact how PostgreSQL types get interpreted into CLR types, and vice versa.
Also, structs are provided for each default temporal type of PostgreSQL databases: `PgSQLDate`, `PgSQLTime`, `PgSQLTimestamp`, `PgSQLTimeTZ`, `PgSQLTimestampTZ`, `PgSQLTimeZone`, and `PgSQLInterval`.

This project does not contain the actual implementation of `PgSQLConnection`, only the API of it and related functionality.
In order to get complete and working implementation of `PgSQLConnection`, use the `PgSQLConnectionPoolProvider` class in [CBAM.SQL.PostgreSQL.Implementation](../CBAM.SQL.PostgreSQL.Implementation) project to create new connection pool, which will expose `PgSQLConnection` to be used.

# Distribution

See [NuGet package](http://www.nuget.org/packages/CBAM.SQL.PostgreSQL) for binary distribution.