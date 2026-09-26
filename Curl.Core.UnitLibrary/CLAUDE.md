# Curl.Core.UnitLibrary

Phase 1.

Transfer engine: URL parsing, scheme dispatch, redirects, resume, retries, rate limiting, IPFS gateway rewriting.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

`FileSystem\PhysicalFileSystem` is the real-disk `IFileSystem` behind `file://`
(ADR-0002). Every failed open comes back as a `FileAccessStatus`; only cancellation
throws. Its disk tests in `Curl.Core.UnitTests` are `[TestCategory("Integration")]`.
