# Curl.Extension.ResumableUpload.UnitLibrary

The resumable upload extension (ADR-0999), for `--resumable-upload`. Namespace
`Curl.Extension.ResumableUpload`. It references the base class library and `Curl.Protocol.Abstractions`
(for `IConnection`), never `Curl.Protocol.Http` or another extension. The client flow (BL-4307) and the
option, its registration in `ExtensionCatalogue.cs` and its description (BL-4129) arrive in later tasks.

- `Http/ResumableUploadHttpClientConnection` sends one HTTP/1.1 request at a time over an `IConnection`
  (RFC 9112), its body optionally from `BodyOffset`, and returns a `ResumableUploadHttpResponse` whose
  body is framed by `Content-Length`, chunked or the close, with the interim 1xx responses before it.
  Every failure is a `ResumableUploadHttpException` naming a `ResumableUploadHttpFailure`. It is a copy
  of the Azure Storage extension's client (ADR-1095).
- `Fields/` holds one type per header field: `Ietf*Field` for draft-ietf-httpbis-resumable-upload's
  Structured Fields and `Tus*Field` for tus 1.0's, each with `Name`, `Format` and `TryParse`.
