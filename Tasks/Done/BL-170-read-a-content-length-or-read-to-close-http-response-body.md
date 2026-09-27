---
id: BL-170
title: Read a Content-Length or read-to-close HTTP response body
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-169]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-170 — Read a Content-Length or read-to-close HTTP response body

## Goal

The Http library reads a response body framed by Content-Length or by connection close, writes it to the output stream, and returns curl's exits for a short body and a failed write.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: a body 7 bytes short of Content-Length gives exit 18 `curl: (18) end of response with 7 bytes missing` (curl 8.21.0).
- HEAD, 204 and 304 have no body even with Content-Length. `OutputWriteFailedException` (Abstractions) carries the accepted byte count of a failed write (BL-114); follow how the file and telnet handlers report it.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] A short body returns `CurlExitCode.PartialFile` (18) `end of response with N bytes missing` with N computed.
- [x] HEAD, 204 and 304 read no body; Content-Length 0 reads nothing; tests for each.
- [x] A failed output write returns `WriteError` (23) with the measured message and the bytes that reached the output.
- [x] Peer close mid-body under Content-Length returns 18; read-to-close ends cleanly at close.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered: `HttpResponseBodyReader` (internal) - `CopyAsync(head, isHeadRequest, output, token)` writes `BodyPrefix` (capped at the length) as one write, then each read of at most `ReadSize` = 16384 bytes as it arrives, never reading past a Content-Length; `BytesWritten` holds the whole writes the output accepted, for the future handler's `TransferResult`. `HasBody` is false for `-I` and for 204/304. `HttpContentLength.Find` parses the headers. Failures throw `HttpTransferException`; `HttpLineReader`'s receive-failure message choice moved to `HttpTransferMessages.ReceiveFailure` so head and body share it. Test fake `Fakes/FailingWriteStream`.
- Measured 2026-09-26 with `/mingw64/bin/curl` 8.21.0 (Schannel): `Record-CurlExchange.ps1 -Port 18170 -Response <bytes> -CurlArgs -sS,<url>`, and for resets and a gone stdout reader a Python loopback server (SO_LINGER 0 for reset) with `curl -sS URL | cmd /c "exit 0"`. Results (response -> outcome):
  - `Content-Length: 12` + `hello` then close -> stdout `hello`, exit 18 `end of response with 7 bytes missing`; `Content-Length: 7` and no body -> the same with nothing written; a 404 with `Content-Length: 9` + `hi` -> `hi`, exit 18, 7 missing.
  - No Content-Length (HTTP/1.1 and 1.0) + `hello` then close -> `hello`, exit 0. `Content-Length: 3` + `hello` -> `hel`, exit 0. `Content-Length: 0` + `hello` -> nothing, exit 0.
  - `-I` with `Content-Length: 5` -> no body read, exit 0. `-X HEAD` with `Content-Length: 5` + `hello` -> `hello` written (so only `-I` suppresses the body; hence the parameter is `isHeadRequest` for `-I`, not the method string). 204 and 304 with `Content-Length: 5` + `hello`, 204 without Content-Length + `hello`, and 204 with `Content-Length: abc` -> nothing, exit 0 (the length is not even parsed).
  - Reset after `hello` under `Content-Length: 12` and under read-to-close -> `hello`, exit 56 `Recv failure: Connection was reset`.
  - 20000-byte body sent 1.5 s after the head, stdout a pipe whose reader had exited -> exit 23 `Failure writing output to destination, passed 16384 returned 0`. A 5-byte body the same way -> `curl: Failed writing body` (the tool's stdio-flush path; `Curl.Console` owns it, not this library).
  - Content-Length values: `5, 5`, `5 , 5`, `	5	`, ` 5`, `5 `, `05`, two headers both 5, `content-LENGTH` -> accepted. `abc`, `-1`, `+5`, `0x5`, `5 x`, `5,6`, `5,5,6`, `5,`, `,5`, `5,,5`, empty, two headers 5 and 3 -> exit 8 `Invalid Content-Length: value`. `99999999999999999999` and `9223372036854775808` -> `hello`, exit 0 (read to close).
- Decision (Claude, under Stewart's delegation): `BytesWritten` after a failed write counts only the writes before it, not the `returned M` bytes of the failed one, matching how `FileProtocolHandler` reports a failed output write. Criterion 3's "bytes that reached the output" is ticked on that basis.
- Not pinned: `Content-Length: 9223372036854775807` + `hello` then close gave exit 0 with nothing written on curl 8.21.0; this reader would give exit 18 with 9223372036854775802 missing. Unexplained upstream edge, left unpinned. An overflowing item in a list alongside other numbers is not measured; the reader treats any overflow as unknown length.
- Verification: `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` clean; `dotnet format --verify-no-changes` clean for both projects; `dotnet test --filter "TestCategory!=Integration"` all green (Curl.Protocol.Http.UnitTests 211 passed, none Integration); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. Every connection-reading body test runs with 1-, 7- and 65536-byte reads.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. HttpResponseBodyReader copies a Content-Length or read-to-close body to the output with curl 8.21.0's measured exits 8, 18, 23 and 56; -I, 204 and 304 read no body
