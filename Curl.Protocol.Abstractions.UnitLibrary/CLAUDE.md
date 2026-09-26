# Curl.Protocol.Abstractions.UnitLibrary

Phase 1.

Contracts every other project depends on. Depends on nothing itself.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
