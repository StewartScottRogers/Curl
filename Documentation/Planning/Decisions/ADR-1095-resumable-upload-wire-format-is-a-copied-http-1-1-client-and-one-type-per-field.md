# ADR-1095: The resumable upload wire format is a copied HTTP/1.1 client and one type per header field

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4306 (split from BL-4129)

Decided by Claude under Stewart's delegation.

## Context

The `--resumable-upload` extension (BL-4129) speaks two protocols over HTTP/1.1: the IETF
draft-ietf-httpbis-resumable-upload and tus 1.0. An extension never references a `Curl.Protocol.*`
library or another extension (ADR-0999), so it needs its own HTTP/1.1 exchange over `IConnection`.

## Decision

- `Curl.Extension.ResumableUpload.UnitLibrary/Http` copies the Azure Storage extension's client
  (itself a copy of the S3 one, ADR-1018) under this library's names, and adds two things the
  protocols need: `ResumableUploadHttpRequest.BodyOffset`, which seeks the body stream so an append
  sends the bytes from the server's offset, and interim 1xx responses other than 101, which are read
  and kept in `ResumableUploadHttpResponse.InterimResponses` so the draft's
  `104 Upload Resumption Supported` and its `Location` reach the client flow (BL-4307).
- Every header field is its own static type with `Name`, `Format` and `TryParse`, prefixed `Ietf` or
  `Tus` because both protocols name fields `Upload-Offset` and `Upload-Length` with different syntax:
  the draft's are Structured Fields (RFC 9651: sf-integer at most fifteen digits, sf-boolean `?0`/`?1`,
  `Upload-Limit` an sf-dictionary), tus's are plain decimal digits and comma-separated lists.
- A Structured Field item's parameters are skipped without being checked, since the draft defines
  none; `Upload-Limit` ignores unknown keys, as the draft requires, and is split on commas, so an
  unknown key whose string value holds a comma is refused. Refusing is the safe side for a client.

## Consequences

The client flow (BL-4307) builds on these types; nothing on the wire depends on upstream curl,
which has no such option, so the extension is tested against the two specifications.
