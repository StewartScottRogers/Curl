# Curl.Extension.ResumableUpload.UnitLibrary

The resumable upload extension (ADR-0999), for `--resumable-upload`. Namespace
`Curl.Extension.ResumableUpload`. It references the base class library, `Curl.Protocol.Abstractions`
(for `IConnection`) and `Curl.Extension.Abstractions` (for `ITransferHook`), never `Curl.Protocol.Http` or
another extension.

- `ResumableUploadExtension` holds the `--resumable-upload` flag, the description `curl --extensions`
  lists and `HookFor`, which gives the transfer hook only when the flag was given (ADR-1151).
  `Curl.Extension.Composition` registers all three in its option, description and transfer hook catalogues.

- `Transfer/ResumableUploadTransferHook` uploads an http or https transfer's `-T` body in place of curl's
  handler (ADR-1103): `OPTIONS` picks `TusUploadProtocol` or `IetfUploadProtocol`, `ResumableUploadFlow`
  creates the upload, appends, and after a broken connection retrieves the offset and appends from it,
  `ResumableUploadSession` sends the requests through curl's connector, and `ResumableUploadOutcome` makes
  the last response the transfer's. Its constructor takes how many resumptions to allow.

- `Http/ResumableUploadHttpClientConnection` sends one HTTP/1.1 request at a time over an `IConnection`
  (RFC 9112), its body optionally from `BodyOffset`, and returns a `ResumableUploadHttpResponse` whose
  body is framed by `Content-Length`, chunked or the close, with the interim 1xx responses before it.
  Every failure is a `ResumableUploadHttpException` naming a `ResumableUploadHttpFailure`. It is a copy
  of the Azure Storage extension's client (ADR-1095).
- `Fields/` holds one type per header field: `Ietf*Field` for draft-ietf-httpbis-resumable-upload's
  Structured Fields and `Tus*Field` for tus 1.0's, each with `Name`, `Format` and `TryParse`.
