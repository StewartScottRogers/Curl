# Curl.Extension.AzureStorage.UnitLibrary

The Azure Storage extension (ADR-0999), for the `az://` and `abfs://` schemes. Namespace
`Curl.Extension.AzureStorage`. It references the base class library and `Curl.Protocol.Abstractions`
(for `IConnection`), never `Curl.Protocol.Http` or another extension. The handler, the options and
its registration in `ExtensionCatalogue.cs` arrive in later tasks (BL-4126).

- `Http/AzureStorageHttpClientConnection` sends one HTTP/1.1 request at a time over an `IConnection`
  (RFC 9112) and returns an `AzureStorageHttpResponse` whose body is a stream framed by
  `Content-Length`, chunked or the close; `ConnectionPersists` says whether the caller may mark the
  connection reusable once the body is read. Every failure is an `AzureStorageHttpException` naming an
  `AzureStorageHttpFailure`. `Expect: 100-continue` is not sent. It follows the S3 extension's client
  (ADR-1018) under this library's names; extensions never reference each other, so it is a copy, not a
  reference.
- `Bodies/` reads and writes the bodies the handler exchanges: `AzureBlobListingParser` (List Blobs
  `EnumerationResults`, with `NextMarker`), `AzureStorageErrorParser` (the `Error` body),
  `AzureBlockListWriter` (Put Block List `BlockList`) and `AzureStoragePathListParser` (ADLS Gen2
  Path List JSON; the `x-ms-continuation` header is the caller's). A parser never throws on a bad
  body: it returns a failed `AzureStorageBodyParseResult` saying why. `System.Xml.Linq` and
  `JsonDocument` only, so nothing needs reflection under Native AOT.
