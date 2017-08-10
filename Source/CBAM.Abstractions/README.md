# CBAM.Abstractions

This project contains types common for other CBAM (Connection Based Asynchronous Messaging) projects.
The `Connection` interface represents a connection to potentially remote resource (e.g. SQL server), which can be manipulated or queried via objects of parametrized `TStatement` type.
The `TStatement` objects are created by the `ConnectionVendorFunctionality` available from `Connection` itself or by having static instances (e.g. `VendorFunctionality` static property of `PgSQLConnectionPoolProvider` type in [CBAM.SQL.PostgreSQL.Implementation](../CBAM.SQL.PostgreSQL.Implementation) project).

# Distribution
See [NuGet package](http://www.nuget.org/packages/CBAM.Abstractions) for binary distribution.