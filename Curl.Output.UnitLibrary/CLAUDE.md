# Curl.Output.UnitLibrary

Phase 1.

The 76 --write-out variables, progress meter, verbose and trace formatting.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
