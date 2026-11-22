---
id: BL-5766
title: End an extension URL's read-callback POST with exit 42 on CURL_READFUNC_ABORT instead of sending the partial body
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.ExtensionReadCallbackPost.cs, Documentation/Planning/Decisions/ADR-1337-a-curl-api-stream-body-to-an-extension-scheme-is-read-up-front-as-curl-d-data.md]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5766 — End an extension URL's read-callback POST with exit 42 on CURL_READFUNC_ABORT instead of sending the partial body

## Goal

A read-callback POST to an extension URL fails the way libcurl and the engine's own HTTP path do, and no extension handler is given part of a body as if it were complete:

- A callback that returns `CURL_READFUNC_ABORT` ends the perform with `CurlCode.AbortedByCallback` (42) and `operation aborted by callback`.
- A callback that returns `CURL_READFUNC_PAUSE`, or more bytes than it was asked for, ends it with `CurlCode.ReadError` (26).

## Context

The shift-end review of 2026-11-22 (ADR-0710) found this in the merge `dfb9516dda...9e2c6390ba`. The silent-failure, type-design and code reviewers each reported it on their own.

- **The bug:** `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:292` `ReadAllOf` (BL-5754, ADR-1337) loops on the read callback and `break`s when `count <= 0 || count > wanted`. That covers:
  - `CURL_READFUNC_ABORT` (0x10000000);
  - `CURL_READFUNC_PAUSE`;
  - a "funny value", more bytes than were asked for.
- **What happens:** in each case the loop returns the bytes read so far. `ReadCallbackPostDataOf` puts them on `PostData`, and the extension's handler (MCP `tools/call`, WebTransport, SignalR, NATS publish and the like) sends a truncated message. The perform can end 0.
- **How the HTTP path handles the same callback:** `CallbackReadStream` marks `Aborted` (exit 42, `AbortedText`, ADR-0573), and uses `FunnyValueText` and `PauseNotSupportedText` for the other two answers.
- **The `Curl.Api` stream body:** this body's read callback turns a stream exception into an abort. So a failing `Stream` given to `CurlContent.FromStream` is also published truncated.
- **The ADR:** ADR-1337 describes the `break` but not that the transfer fails, so update its Decision and Consequences.
- **Overlap:** BL-5765 touches the same file. Run it after or before this task, not alongside.

## Acceptance criteria

- [x] A test posts through a read callback that gives some bytes and then `CURL_READFUNC_ABORT` to an extension URL. It asserts `CurlCode.AbortedByCallback`, the error text `operation aborted by callback`, and that the handler received no `PostData`, or was never run.
- [x] A test with a callback that returns more than it was asked for asserts `CurlCode.ReadError` with the `read function returned funny value` text that `CallbackReadStream` uses.
- [x] A test with a callback that returns `CURL_READFUNC_PAUSE` asserts `CurlCode.ReadError` with `Read callback asked for PAUSE when not supported`.
- [x] A `Curl.Api` test posts a stream body that throws an `IOException` to an extension scheme. It asserts the request fails and is not delivered truncated.
- [x] ADR-1337 says what each of these callback answers does.
- [x] `dotnet build` is clean, the fast tests pass, and the quality gates hold.

## Notes

Engine behaviour (exit 42, exit 26 for PAUSE and funny value, with engine tests) already landed in BL-5764 (75073d4c5d, ADR-1339). This task added the Curl.Api stream-throws test and the ADR-1337 text. No production code changed, so no quality run.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: b22b9dcb 46569a08 4a1054fd 5457bf5f af89187f e58cc7bf.
- 2026-11-22: Doing -> Done. Engine fix already in BL-5764; added Curl.Api stream-throws test and ADR-1337 text
