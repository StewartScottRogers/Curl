# Curl.Extension.ResumableUpload.UnitLibrary

The resumable upload extension (ADR-0999), for `--resumable-upload`. Namespace
`Curl.Extension.ResumableUpload`. It references the base class library, `Curl.Protocol.Abstractions`
(for `ITransferContext`) and `Curl.Extension.Abstractions` (for `ITransferHook`), never `Curl.Original.Protocol.Http` or
another extension.

- `ResumableUploadExtension` holds the `--resumable-upload` flag, the description `curl --extended-help`
  lists and `HookFor`, which gives the transfer hook only when the flag was given (ADR-1151).
  `Curl.Extension.Composition` registers all three in its option, description and transfer hook catalogues.

- `Transfer/ResumableUploadTransferHook` uploads an http or https transfer's `-T` body in place of curl's
  handler (ADR-1103): `OPTIONS` picks `TusUploadProtocol` or `IetfUploadProtocol`, `ResumableUploadFlow`
  creates the upload, appends, and after a broken connection retrieves the offset and appends from it,
  `ResumableUploadSession` sends each request through the rest of curl's transfer (`next`, with a
  `ResumableUploadRequestContext`), so `-v`, `--trace`, `--har` and `--record` see it (ADR-1379); a request's content is
  an `UploadRemainderStream`, a read-only window over the upload from its offset, so nothing of it is copied into memory.
  `ResumableUploadOutcome` makes
  the last response the transfer's. `RereadableUpload` gives the upload as a stream that can be sent again: a window over
  a seekable one, otherwise a copy in memory up to a limit and in a delete-on-close temporary file past it, ending with
  exit 26 when the upload cannot be read and 23 when the file cannot be written (ADR-1400). The hook's constructor takes
  how many resumptions to allow, the memory limit and what creates the spool file.

- `Fields/` holds one type per header field: `Ietf*Field` for draft-ietf-httpbis-resumable-upload's
  Structured Fields and `Tus*Field` for tus 1.0's, each with `Name`, `Format` and `TryParse`.
