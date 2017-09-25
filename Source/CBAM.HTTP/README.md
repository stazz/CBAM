# CBAM.HTTP

The CBAM.HTTP project extends the abstractions in [CBAM.Abstractions](../CBAM.Abstractions) project to provide `HTTPConnection` which allows executing `HTTPStatement`s on it.
The `HTTPStatement` is built so that it allows either sending one `HTTPRequest` or it has callback to generate `HTTPRequest`s that will be sent until the callback returns `null`.

The HTTP API itself is also within this project (this may change in the future).
The `CBAM.HTTP.HTTPMessageFactory` class contains factory methods to obtain instances of HTTP messages.
This API is extremely minimalistic, which also allows maximum versatility.

# Why some existing HTTP API was not chosen to be used in this project?
The API in `System.Net.Http` namspace was considered, but creating `System.Net.Http.HttpResponseMessage`s is annoyingly difficult to do efficiently, and that namespace is also extremely bound to `System.Net.Http.HttpClient`, which is not suitable for this project.
Furthermore, the `System.Net.Http` namespace is outdated and overly complex for this purpose.

The alternative in .NET Core world would be `Microsoft.AspNetCore.Http` namspace, but it represents HTTP protocol from server point of view, and thus the API it exposes it not suitable for this project.

# Distribution
See [NuGet package](http://www.nuget.org/packages/CBAM.HTTP) for binary distribution.