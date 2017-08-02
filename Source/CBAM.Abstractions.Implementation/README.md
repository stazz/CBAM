# CBAM.Abstractions.Implementation

This project contains types implementing abstractions in [CBAM.Abstractions](../CBAM.Abstractions) project.
The implementation is done in such way that it implements the common concerns for all CBAM projects, but leaves enough room for domain-specific implementation (e.g. [CBAM.SQL](../CBAM.SQL)).

The implementations for `ConnectionPool` interface of [CBAM.Abstractions](../CBAM.Abstractions) project are classes `OneTimeUseConnectionPool`, `CachingConnectionPool`, and `CachingConnectionPoolWithTimeout`.
The `OneTimeUseConnectionPool` will close all connections after executing user callback in `UseConnectionAsync` method, while `CachingConnectionPool` will use generic pool to cache the connections between invocations of `UseConnectionAsync` method.
The `CachingConnectionPoolWithTimeout` extends `CachingConnectionPool` and implements the `CleanUpAsync` method of `ConnectionPool` interface with two generic parameters - the method implementation will close all overdue connections that it finds from the underlying connection instance pool.

The `ConnectionImpl` class implements `Connection` interface by delegating the functionality to a new interface, `ConnectionFunctionality`.
This enables to use connection-related functionality by private components of `Connection`.
The `DefaultConnectionVendorFunctionality` class implements the `ConnectionVendorFunctionality` interface, and is used by connection pool when they need to create a new instance of connection.

# Distribution
See [NuGet package](http://www.nuget.org/packages/CBAM.Abstractions.Implementation) for binary distribution.