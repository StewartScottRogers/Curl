---
id: BL-3832
title: Send escaped header-file bytes unchanged in multipart part headers and CONNECT proxy headers (CGR-020 follow-up)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary/Multipart/MultipartBodySegments.cs, Curl.Core.UnitTests/Cgr020MultipartTests.cs, Curl.Networking.UnitLibrary/HttpProxyTunnel.cs, Curl.Networking.UnitTests/Cgr020TunnelTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3832 — Send escaped header-file bytes unchanged in multipart part headers and CONNECT proxy headers (CGR-020 follow-up)

## Goal

On Linux and macOS, a `-F "a=b;headers=@file"` part header and a `--proxy-header @file` sent in an HTTPS-proxy CONNECT carry the header file's bytes unchanged, as curl 8.18.0 (OpenSSL) does, even when the bytes are not valid UTF-8.

## Context

BL-3549 (CGR-020) reads header files with `Curl.Cli.HeaderFileText.Read`: off Windows each byte UTF-8 cannot read becomes the lone surrogate U+DC00 plus the byte (U+DC80 to U+DCFF). `HttpRequestHeadFormatter.WireText` gives each such character back as its own byte, so `-H @file` and a forward-proxy `--proxy-header @file` now match curl. Two other encoders still turn those characters into EF BF BD (U+FFFD), as before BL-3549:
- `Curl.Core.UnitLibrary/Multipart/MultipartBodySegments.cs:117` (`textEncoding.GetBytes`) for multipart part headers.
- `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:112` (CONNECT request proxy headers).

Repro (Linux): `printf 'X-C: \xff\x80\xc3(\n' > h.txt; curl -s -F "a=b;headers=@h.txt" http://127.0.0.1:PORT/` sends `X-C: FF 80 C3 28` (CGR-020, ENV-02). Use the same escape rule as `HttpRequestHeadFormatter.WireText` (a U+DC80-U+DCFF char not preceded by a high surrogate is one byte).

## Acceptance criteria

- [ ] A multipart part header holding U+DC80-U+DCFF escapes is written as those bytes, with a unit test in `Curl.Core.UnitTests/Cgr020MultipartTests.cs`.
- [ ] A CONNECT request's proxy header holding such escapes is written as those bytes, with a unit test in `Curl.Networking.UnitTests/Cgr020TunnelTests.cs`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3549, whose `touches` held only the Cli and the HTTP head formatter.

## Log

- 2026-10-26: Created by BL-3549.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 12b0f17a 32e4c64d 779790c0.
