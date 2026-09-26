# Curl.Cli.UnitLibrary

Phase 1.

The 274-option table, argument parsing, .curlrc and -K, --variable, usage text, exit-code mapping.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
