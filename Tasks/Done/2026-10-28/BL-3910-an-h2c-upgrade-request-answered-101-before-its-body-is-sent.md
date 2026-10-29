---
id: BL-3910
title: An h2c Upgrade request answered 101 before its body is sent keeps sending the HTTP/1.1 body as curl does not
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.H2cUpgrade.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3910 — An h2c Upgrade request answered 101 before its body is sent keeps sending the HTTP/1.1 body as curl does not

## Goal

When a server answers an h2c Upgrade request with `101` before reading its body, Curl sends the same bytes on the wire as curl 8.18.0 (REF-LINUX) and ends with the same exit code and `-w` values.

## Context

Found by BL-3672 (CGR-143). With a scripted h2c server that answers `101`, an empty SETTINGS, a SETTINGS ACK and HEADERS `:status 200` END_STREAM on stream 1 as soon as it has the request head, `curl --http2 --data-binary @2m.bin http://H:P/echo` (2 MiB of zeros) through REF-LINUX 8.18.0 wrote only 589,898 bytes in all before closing: part of the HTTP/1.1 body, then HTTP/2 frames. Curl wrote the whole 2,097,152-byte body and then its HTTP/2 frames (2,097,505 bytes). Both exited 0 with `[2 200 1]`. When the server reads the whole body before it answers, the two send identical bytes (BL-3672 Notes). The server was a PowerShell TcpListener script (BL-3672 Notes describe it); rebuild it, and measure what curl does with the rest of the body once it has the `101` before pinning anything.

## Acceptance criteria

- [x] Against a server that answers the Upgrade before reading the body, Curl's bytes on the wire, exit code and `-w "[%{http_version} %{http_code} %{num_connects}]"` equal REF-LINUX's, recorded in Notes.
- [x] A unit test in `Curl.Protocol.Http.UnitTests` pins the behaviour measured.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Server: a throwaway PowerShell TcpListener script that reads only the request head, then writes `HTTP/1.1 101 Switching Protocols` (Connection: Upgrade, Upgrade: h2c), an empty SETTINGS, a SETTINGS ACK and HEADERS `:status 200` END_STREAM on stream 1, and logs every byte until the close. Command: `curl -sS --http2 --data-binary @2m.bin -w "[%{http_version} %{http_code} %{num_connects}]" http://H:P/echo` (2 MiB of zeros).
- REF-LINUX (WSL curl 8.18.0, nghttp2 1.68.0, against the Windows host), twice: the head (253 bytes), then body bytes up to offset 1,835,008 (28 x 64 KiB), then the preface and frames, 1,835,133 bytes in all; stdout `[2 200 1]`, exit 0. curl stops sending the HTTP/1.1 body once it reads the 101 and drops the rest. How much went before is timing (how soon the 101 is read): BL-3672's run of the same command saw 589,898 bytes in all, this one 1,835,133.
- Curl before the fix: the whole 2,097,152-byte body, then the frames. After it, twice (Windows loopback): preface at offset 524,288 (8 x 64 KiB), 524,413 bytes in all; stdout `[2 200 1]`, exit 0. The 125 bytes from the preface on (SETTINGS, WINDOW_UPDATEs, SETTINGS ACK, GOAWAY) are byte-for-byte REF's. The criterion's "bytes equal" is read as: the same head, a body cut at an upload-buffer boundary once the 101 is read, then the same HTTP/2 bytes. The cut point is timing in curl itself (two REF runs gave two counts), so no implementation can equal it byte for byte.
- Fix (`HttpH2cUpgradeConnection`): once the request head is written the connection starts its watch for the `101` in the background, writes the rest of the request in `HttpRequestBodyWriter.UploadBufferSize` (64 KiB) pieces, and drops the pieces left once a whole `101` head has arrived; the HTTP/2 session then goes on as before. A non-101 early answer changes nothing. Piece size: curl's upload buffer, since curl looks for the response between sends of that buffer (default taken, rule 1). No ADR: it matches measured curl and takes no design choice beyond that.
- Unit tests in `HttpProtocolHandlerTests.H2cUpgrade.cs`: `ExecuteAsync_Http2UpgradeAnswered101BeforeTheBodyIsSent_StopsTheBodyAndGoesOnInHttp2` (the whole body went before the preface without the fix) and `ReadAsync_BeforeAnyWrite_WatchesForThe101AndHandsOutAnHttp11Response` (a read that starts the watch).
- Build clean; fast tests 46 projects green, no failures (Curl.Protocol.Http.UnitTests 2592 passed, 19 skipped). `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members. `dotnet format --verify-no-changes` on both files clean. Platform-neutral (no paths, OS text or certificates), which covers CI on Linux and macOS.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: a52ab161 266903ce 779790c0.
- 2026-10-28: Doing -> Done. An h2c Upgrade answered 101 before the body is sent stops the HTTP/1.1 body and goes on in HTTP/2, as curl 8.18.0 does
