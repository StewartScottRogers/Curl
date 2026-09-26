# Curl.Cli.UnitLibrary

Phase 1.

Phase 1 will hold the 274-option table, argument parsing, .curlrc and -K, --variable,
usage text and exit-code mapping. Today it holds only `UploadUrl`, which nothing calls
yet.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

`UploadUrl` is pure string work: it never reads the file system, and it never parses,
validates or normalises the URL. That is the URL layer's job, run where `UploadUrl` is
called (ADR-0004).
