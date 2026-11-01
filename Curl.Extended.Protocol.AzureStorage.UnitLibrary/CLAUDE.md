# Curl.Extended.Protocol.AzureStorage.UnitLibrary

The Azure Storage extension (ADR-0999), for the `az://` and `abfs://` schemes. Namespace
`Curl.Extended.Protocol.AzureStorage`. It references the base class library and `Curl.Protocol.Abstractions`
(for `IConnection`) and `Curl.Extension.Abstractions` (for its description), never `Curl.Original.Protocol.Http`
or another extension. It is registered in `ExtensionCatalogue.cs` and `ExtensionDescriptionCatalogue.cs`.

- `Client/` is the handler (ADR-1105). `AzureStorageExtension` gives the description and creates
  `AzureStorageProtocolHandler`, which maps the URL, chooses the credential and hands `az` to
  `AzureBlobOperations` (Get Blob, Put Blob, Put Block and Put Block List from 8 MiB, List Blobs) and
  `abfs`/`abfss` to `AzureDataLakeOperations` (Path Read, Path Create, Path Update append and flush,
  Path List). `AzureStorageClient` sends one request per connection over HTTPS on 443 and turns a status
  of 300 or above into exit 22 naming the `Error` code; every other failure is an
  `AzureStorageTransferException` carrying its exit code.

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
- `Requests/AzureStorageUrlMapping` maps `az://account/container/blob` to the Blob endpoint and
  `abfs[s]://filesystem@account.dfs.core.windows.net/path` to the DFS endpoint (a bare account name gets
  `.blob.core.windows.net` or `.dfs.core.windows.net`). `AzureStorageCredential.Choose` takes
  `--oauth2-bearer`, then `AZURE_STORAGE_BEARER_TOKEN`, then `AZURE_STORAGE_SAS_TOKEN`, else anonymous.
  `AzureStorageRequestAuthorizer` appends a SAS token to the query or sends `Authorization: Bearer`, and
  always sends `Host`, `x-ms-version` (`2025-01-05`) and `x-ms-date` from its `TimeProvider`. Every failure
  is an `AzureStorageRequestException` naming an `AzureStorageRequestFailure`. Shared Key signing is not
  done (BL-4290's Notes).
