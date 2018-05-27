# CBAM.HTTP.Implementation

This project provides API to create instances of types defined in [CBAM.HTTP](../CBAM.HTTP) project.
Unlike [CBAM.SQL.PostgreSQL.Implementation](../CBAM.SQL.PostgreSQL.Implementation), the `HTTPConnection`s are not oriented to be used by [resource pools](https://github.com/CometaSolutions/UtilPack/tree/develop/Source/UtilPack.ResourcePooling), instead their management is manual.
<!--The `HTTPConnection` itself will use the [network resource pool](https://github.com/CometaSolutions/UtilPack/tree/develop/Source/UtilPack.ResourcePooling.NetworkStream) when sending and receiving HTTP messages.
This is why the creation of `HTTPConnection` objects is provided via extension method on `ExplicitAsyncResourcePool<Stream>` type, and extension method is located in `CBAM.HTTP.HTTPExtensions` class of this project.

# Code example
Below is a small code sample illustrating the typical usecase of this library:

```csharp
using System.Collections.Concurrent;
using UtilPack; // For "CreateRepeater" extension method
using UtilPack.ResourcePooling.NetworkStream; // For NetworkStreamFactory
using CBAM.HTTP; // For HTTP-related

// Store all responses as strings in this simple example
ConcurrentBag<String> responseTexts;
using ( var pool = new NetworkStreamFactory()
  .BindCreationParameters(
    new HTTPConnectionEndPointConfigurationData()
    {
      Host = "www.google.com",
      IsSecure = true
    }.CreateNetworkStreamFactoryConfiguration()
  ).CreateTimeoutingAndLimitedResourcePool( 10 ) // Cache streams and their idle time, and limit maximum concurrent connections to 10
  )
{
  // Create CBAM HTTP connection
  var httpConnection = pool.CreateNewHTTPConnection();

  // Send 20 requests to "/" path in parallel and process each response
  // Note that only 10 connections will be opened, since the pool is limited to 10 concurrent connections
  responseTexts = await httpConnection
    .PrepareStatementForExecution( HTTPMessageFactory
      .CreateGETRequest( "/" ) // Fetch top-level resource
      .CreateRepeater( 20 ) // Repeat same request 20 times
    )
    // Read whole response content into byte array and get string from it (assume UTF-8 encoding for this simple example)
    .ToConcurrentBagAsync( async response => Encoding.UTF8.GetString( await response.Content.ReadAllContentIfKnownSizeAsync() ) );
}

// Now the responseTexts bag will contain 20 HTTP responses as text.
```-->

# Distribution
See [NuGet package](http://www.nuget.org/packages/CBAM.HTTP.Implementation) for binary distribution.