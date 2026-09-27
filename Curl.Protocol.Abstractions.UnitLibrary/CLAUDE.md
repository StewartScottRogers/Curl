# Curl.Protocol.Abstractions.UnitLibrary

Phase 1.

Contracts every other project depends on. Depends on nothing itself. Besides the contracts it
holds `CurlDateParser`, the one port of libcurl's `parsedate`, shared by `-z` in `Curl.Cli` and
cookie `Expires` in `Curl.Cookies` (ADR-0074).

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

## `IFileSystem` implementations never throw on an open failure

An `IFileSystem` implementation returns `FileOpenResult.Failed` with a
`FileAccessStatus` for every reason an open can fail. No exception escapes
`OpenForReadAsync` or `OpenForWriteAsync` except an `OperationCanceledException`
raised by the `CancellationToken`. `FileProtocolHandler` promises that a transfer
failure comes back as a `TransferResult` and is never thrown; that promise holds
only while every `IFileSystem` keeps this one (ADR-0002).

The path handed to an open may be syntactically invalid for the platform, because
`FileUrlPath` forwards it as curl does rather than rejecting it: `c|/Windows` keeps
its bar instead of becoming a colon, a malformed escape such as `%2` or `%GG` stays
as a literal `%`, and outside `--path-as-is` the path is whatever dot-segment
resolution left behind. A `System.IO`-based implementation therefore has to absorb,
and map onto a `FileAccessStatus`, at least:

- `ArgumentException`
- `NotSupportedException`
- `PathTooLongException`
- `DirectoryNotFoundException`
- `FileNotFoundException`
- `UnauthorizedAccessException`
- `IOException`

In curl's terms the consequence is fixed whichever operating-system error occurred:
a failed read open is exit 37 (`CURLE_FILE_COULDNT_READ_FILE`) and a failed write
open is exit 23 (`CURLE_WRITE_ERROR`)
(<https://curl.se/libcurl/c/libcurl-errors.html>, checked against curl 8.21.0). An
exception that escapes instead turns that exit code into an unhandled crash.

## A body read fails the transfer only through `RequestBodyReadFailedException`

A request body's stream that throws a plain `IOException` has reached its end, as curl takes
a failed file read. Only `RequestBodyReadFailedException` fails the send: the handler stops
with exit 26 and the exception's message. `Curl.Core`'s `EncodedReadStream` throws it with
`read error getting mime data` at a byte a multipart encoder refuses (ADR-0093).
