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
completed:
---
# BL-3910 — An h2c Upgrade request answered 101 before its body is sent keeps sending the HTTP/1.1 body as curl does not

## Goal

When a server answers an h2c Upgrade request with `101` before reading its body, Curl sends the same bytes on the wire as curl 8.18.0 (REF-LINUX) and ends with the same exit code and `-w` values.

## Context

Found by BL-3672 (CGR-143). With a scripted h2c server that answers `101`, an empty SETTINGS, a SETTINGS ACK and HEADERS `:status 200` END_STREAM on stream 1 as soon as it has the request head, `curl --http2 --data-binary @2m.bin http://H:P/echo` (2 MiB of zeros) through REF-LINUX 8.18.0 wrote only 589,898 bytes in all before closing: part of the HTTP/1.1 body, then HTTP/2 frames. Curl wrote the whole 2,097,152-byte body and then its HTTP/2 frames (2,097,505 bytes). Both exited 0 with `[2 200 1]`. When the server reads the whole body before it answers, the two send identical bytes (BL-3672 Notes). The server was a PowerShell TcpListener script (BL-3672 Notes describe it); rebuild it, and measure what curl does with the rest of the body once it has the `101` before pinning anything.

## Acceptance criteria

- [ ] Against a server that answers the Upgrade before reading the body, Curl's bytes on the wire, exit code and `-w "[%{http_version} %{http_code} %{num_connects}]"` equal REF-LINUX's, recorded in Notes.
- [ ] A unit test in `Curl.Protocol.Http.UnitTests` pins the behaviour measured.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: a52ab161 266903ce 779790c0.
