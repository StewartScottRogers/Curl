---
id: BL-3867
title: CGR-062 part 2: a raw non-ASCII byte in an absolute Location is written back as that byte by %{redirect_url} and %{url_effective}
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Protocol.Http.UnitTests/Cgr062HeaderByteTests.cs, Curl.Output.UnitTests/Cgr062HeaderByteTests.cs, Curl.Protocol.Abstractions.UnitLibrary/HeaderByteText.cs, Curl.Protocol.Abstractions.UnitTests/HeaderByteTextTests.cs, Curl.Core.UnitTests/Cgr062RedirectFollowerTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3867 — CGR-062 part 2: a raw non-ASCII byte in an absolute Location is written back as that byte by %{redirect_url} and %{url_effective}

## Goal

A redirect whose absolute `Location` holds a raw byte from 0x80 to 0xFF (`Location: http://\xE9.invalid/b`) prints that byte unchanged, one byte, in `%{redirect_url}` (without `-L`) and `%{url_effective}` (with `-L`), as curl 8.22.0 Schannel does, not its UTF-8 encoding (`C3 A9`).

## Context

Split from BL-3591 (CGR-062, the HS-9 `loc-unicode-host-noL` repro of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Measured on 2026-10-26 with `Record-CurlExchange.ps1`, first response `HTTP/1.1 302 Found\r\nLocation: http://\xE9.invalid/b\r\nContent-Length: 0\r\n\r\n`, `-w '%{http_code} %{num_redirects} %{url_effective} %{redirect_url}\n'`:

- REF (curl 8.22.0 Schannel) without `-L`: exit 0, stdout `302 0 http://127.0.0.1:P/a/b http://<E9>.invalid/b`; with `-L`: exit 6, `302 1 http://<E9>.invalid/b`, stderr `curl: (6) Could not resolve host: xn--9ca.invalid`.
- PORT: the same, except each `<E9>` is written as `C3 A9`. The resolved host (`xn--9ca.invalid`) already matches.

Cause: `HttpRedirectLocation` reads the header as Latin-1 (one char per byte) and keeps an absolute value's bytes, so `TransferReport.RedirectUrl` holds U+00E9; `TransferWriteOutVariables` writes `redirect_url` and `url_effective` with `WriteOutValue.FromText`, which UTF-8 encodes it. Marking the whole string with `WriteOutTemplateRenderer.MarkHeaderBytes` (as `content_type` does) is wrong: a relative `Location` keeps the request URL's own authority, which is text from the command line (an IDN host such as `åäö.se`, upstream tests 2046 and 2047) and must stay UTF-8. So the report must carry which characters are header bytes (for example the redirect URL's header-byte span, or a marked string the follower unmarks before `EncodedForFollow`), and `RedirectFollower`'s effective URL must keep that provenance for the followed target. Check every other reader of `TransferReport.RedirectUrl`/`EffectiveUrl` (Libcurl `EasyInfoReaders.Strings.cs`, `Curl.Console/EasyHandleWriteOutInfo.cs`, `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs`) and keep their answers. Record the representation chosen in an ADR. Linux curl 8.18 fails this case with exit 3 instead; pin only the Windows answer unless Linux 8.22 is measured.

## Acceptance criteria

- [x] The repro above, run through real curl and through Curl on Windows, with and without `-L`, gives the same exit code, stdout bytes and stderr text; commands and results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] A relative `Location` after a request to an IDN host still writes the host as UTF-8 text in `%{url_effective}` and `%{redirect_url}`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: f3912fd8 f457ad6c d8a159f1 779790c0.
- 2026-10-26: Doing -> Done. An absolute Location's raw byte 0x80-0xFF is written back as that byte by %{redirect_url} and %{url_effective}, as curl 8.22.0 Schannel does
