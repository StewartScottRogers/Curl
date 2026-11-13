---
id: BL-5107
title: Map an HTTP/2 framing failure of --ws-extended-connect h2 to exit 16
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebSocketExtendedConnect.UnitLibrary/Http2, Curl.Extension.WebSocketExtendedConnect.UnitLibrary/Option/Http2ExtendedConnectChannel.cs, Curl.Extension.WebSocketExtendedConnect.UnitTests/Http2]
requirement: none
created: 2026-11-13
completed:
---
# BL-5107 — Map an HTTP/2 framing failure of --ws-extended-connect h2 to exit 16

## Goal

`--ws-extended-connect h2` against a server that does not speak HTTP/2 exits 16 with curl's SETTINGS-frame text, not exit 2 with a raw .NET frame error.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-058 (Low, Confirmed). Part B: judged against the port's own docs and specs, not curl.

**Repro.** With an HTTP/1.1-only server on 127.0.0.1:28000: `curl -sS -m 3 --ws-extended-connect h2 ws://127.0.0.1:28000/ws`

**Expected.** ADR-1111: a refused or failed extended CONNECT over HTTP/2 fails with exit 16. The port's own `--http2-prior-knowledge http://127.0.0.1:28000/plain` against the same server prints `curl: (16) Remote peer returned unexpected data while we expected SETTINGS frame.  Perhaps, peer does not support HTTP/2 properly.`

**Actual.** `curl: (2) HTTP/2 FrameSizeError: a frame of 3940676 bytes exceeds the maximum frame size of 16384.`, exit 2.

**Where to start.** `Curl.Extension.WebSocketExtendedConnect.UnitLibrary` (`Http2/Http2WebSocketClient.cs`, `Http2/Http2WebSocketException.cs`, `Option/Http2ExtendedConnectChannel.cs`) does not map the HTTP/2 framing exception to 16 as the HTTP handler does. Find how `Curl.Original.Protocol.Http.UnitLibrary` produces the `Remote peer returned unexpected data while we expected SETTINGS frame` message; the extension may not reference that library, so reproduce the mapping inside the extension (or use an allowed shared layer).

## Acceptance criteria

- [ ] A new test file in `Curl.Extension.WebSocketExtendedConnect.UnitTests/Http2` (for example `NonHttp2ServerTests.cs`) proves that when the server's first bytes are not an HTTP/2 SETTINGS frame (an HTTP/1.1 response whose first bytes read as an oversized frame length), the transfer fails with `CurlExitCode` 16 and the message `Remote peer returned unexpected data while we expected SETTINGS frame.  Perhaps, peer does not support HTTP/2 properly.`
- [ ] A regression test proves a genuine HTTP/2 protocol error after the SETTINGS exchange keeps its existing exit code, and a successful extended CONNECT is unchanged.
- [ ] Tests are platform-neutral and use in-memory streams.
- [ ] `dotnet build Curl.Extension.WebSocketExtendedConnect.UnitLibrary -warnaserror` is clean, and `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app (`dotnet run tool.cs`) or `Record-CurlExchange.ps1`, never Python. The unit tests need no server.
- Extension isolation holds: the extension never references an Original protocol library or another extension.
- Review acceptance check, as a hand check: the command exits 16 with the same SETTINGS-frame text as `--http2-prior-knowledge`.

## Log

- 2026-11-13: Created.
