# Curl.Core.UnitLibrary

Phase 1.

Transfer engine: URL parsing, scheme dispatch, redirects, resume, retries, rate limiting, IPFS gateway rewriting.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

`FileSystem\PhysicalFileSystem` is the real-disk `IFileSystem` behind `file://`
(ADR-0002). Every failed open comes back as a `FileAccessStatus`; only cancellation
throws. Its disk tests in `Curl.Core.UnitTests` are `[TestCategory("Integration")]`.

`ProtocolDispatcher` hands a transfer to the `IProtocolHandler` registered for its URL's
scheme (case-insensitive) and returns exit 1, `Protocol "<scheme>" not supported`, when
none is; two handlers claiming one scheme make its constructor throw. It is not yet wired
into `Curl.Console`.

`ByteRangeParser` is the one place `-r`/`--range` text becomes the `ByteRange` a handler
receives on `ITransferContext.Range`, read as libcurl 8.21.0's `Curl_range` reads it; text
that names no range is `NotDeliveredFailure`, exit 33. Handlers never parse range text.
