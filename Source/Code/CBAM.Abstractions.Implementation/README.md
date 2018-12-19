# CBAM.Abstractions.Implementation

This project contains types implementing abstractions in [CBAM.Abstractions](../CBAM.Abstractions) project.
The implementation is done in such way that it implements the common concerns for all CBAM projects, but leaves enough room for domain-specific implementation (e.g. [CBAM.SQL](../CBAM.SQL)).

The `ConnectionImpl` class implements `Connection` interface by delegating the functionality to a new interface, `ConnectionFunctionality`.
This enables to use connection-related functionality by private components of `Connection`.
The `DefaultConnectionVendorFunctionality` class implements the `ConnectionVendorFunctionality` interface, and is used by connection pool when they need to create a new instance of connection.

The `DefaultConnectionFactory` class implements the `ResourceFactory` interface of [UtilPack.ResourcePooling](https://github.com/CometaSolutions/UtilPack/tree/develop/Source/UtilPack.ResourcePooling) package to provide some common functionality when creating instances of classes deriving from `ConnectionImpl`.
The `ConnectionFactorySU` class further specializes the `DefaultConnectionFactory` class with functionality common for connections operating over unseekable stream (e.g. network stream).
Typical CBAM implementations will also want to provide implementation for abstract class `ConnectionAcquireInfoImpl` which is used by resource pools.


# Distribution
See [NuGet package](http://www.nuget.org/packages/CBAM.Abstractions.Implementation) for binary distribution.