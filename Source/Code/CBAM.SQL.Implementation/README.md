# CBAM.SQL.Implementation

This project provides skeleton implementations for types defined in [CBAM.SQL](../CBAM.SQL) project.
The implementation is done in such way that common code for all SQL vendors would be in this project, and vendor-specific implementation would only need to customize smaller subset of API.
This project is only intented to be used directly by projects which provide the concrete CBAM implementation for some specific SQL vendor, e.g. [PostgreSQL implementation](../CBAM.SQL.PostgreSQL.Implementation).
The XML documentation of types will explain in more detail about each type and method provided in this project.

# Distribution
See [NuGet package](http://www.nuget.org/packages/CBAM.SQL.Implementation) for binary distribution.