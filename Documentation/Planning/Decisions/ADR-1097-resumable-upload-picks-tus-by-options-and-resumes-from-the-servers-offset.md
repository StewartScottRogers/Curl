# ADR-1097: The resumable upload picks tus by OPTIONS and resumes from the server's offset

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4307 (split from BL-4129)

Decided by Claude under Stewart's delegation.

## Context

`--resumable-upload` (BL-4129) uploads `-T`'s body to a server that speaks either tus 1.0 or the IETF
draft-ietf-httpbis-resumable-upload, and resumes after a broken connection. The transfer hook
`ResumableUploadTransferHook` (in `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/`) runs the upload in
place of curl's handler through the connector `TransferHookServices` lends it (ADR-1039), over ADR-1095's
HTTP/1.1 client. Upstream curl has no such option, so there is no curl output to match; the decisions are
about the protocols.

## Decision

- **Only an http or https transfer with an upload** is taken over; every other transfer goes to `next`
  unchanged. Without services the hook fails such a transfer with exit 2, as the OAuth 2.0 hook does.
- **Protocol choice.** The hook sends `OPTIONS` to the URL. A 2xx response whose `Tus-Version` names
  `1.0.0` and whose `Tus-Extension` names `creation` selects tus; anything else selects the IETF draft.
  `Tus-Resumable` is not required on that response, since tus 1.0 exempts `OPTIONS` from it.
- **tus 1.0.** "Creation": `POST` with `Tus-Resumable` and `Upload-Length`, no body. "Core Protocol":
  `PATCH` with `Upload-Offset` and `Content-Type: application/offset+octet-stream`; `HEAD` for the offset.
- **IETF draft, interop version 6.** Upload creation with curl's method (`PUT` for `-T`, or `-X`'s),
  `Upload-Complete: ?0` and `Upload-Length`, no body; upload append with `PATCH`,
  `Content-Type: application/partial-upload`, `Upload-Offset` and `Upload-Complete: ?1`; offset retrieval
  with `HEAD`. The upload resource is the creation's 2xx `Location` (resolved against the URL); a 104
  interim response's `Location` is not used.
- **Resumption.** A step broken by its connection (a send or receive failure, or the connection closing
  before the response) is tried again, at most `DefaultMaxResumptions` (3) times in all, the constructor's
  argument for BL-4129 to set. A broken append retrieves the offset with `HEAD` first and appends from
  there; a broken creation is sent again. A connect failure or a malformed response is not resumed.
- **Seekable body.** A non-seekable upload, or one already read part way, is read whole into memory
  first: both protocols announce `Upload-Length` on creation, and a resumption sends again from any offset
  the server gives.
- **Outcome.** The last response (the append's, or a creation or offset retrieval answered without 2xx or
  without `Location`) is the transfer's: body to `Output`, head to `-i`'s and `-D`'s outputs, status and
  headers in the `TransferReport`. `-f` with a status of 400 or more ends with 22 and writes nothing;
  `--fail-with-body` writes it and ends with 22. Failures end with curl's code: the connector's (7 and the
  rest), 55 for sending, 56 for receiving, 8 for a malformed response or an offset outside the upload.
- **Requests** carry `Host`, the flow's fields, then `-H`'s lines, except one naming a field already set or
  `Content-Length`, or one without a value.

## Consequences

The upload needs no help from curl's HTTP handler and runs over any connection the connector gives,
including TLS. Through an HTTP forward proxy the requests use origin-form targets, so only a tunnelling
proxy works, as for the WebSocket REPL hook. Buffering a non-seekable upload holds it in memory.
