---
id: BL-3871
title: Libcurl and Curl.Api hand an absolute Location's header bytes out as libcurl byte text, not HeaderByteText marks
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-3867]
touches: [Libcurl/Info/Strings/EasyInfoReaders.Strings.cs, Libcurl.UnitTests/Info/RedirectUrlHeaderByteTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3871 — Libcurl and Curl.Api hand an absolute Location's header bytes out as libcurl byte text, not HeaderByteText marks

## Goal

`CURLINFO_REDIRECT_URL` and `CURLINFO_EFFECTIVE_URL` give an absolute `Location`'s raw byte 0xE9 as libcurl 8.22.0 does (as `UrlByteText` byte text, U+DCE9), not the `HeaderByteText` mark U+F7E9 the transfer report carries since BL-3867.

## Context

ADR-0923 (BL-3867) carries an absolute `Location`'s bytes from 0x80 marked as U+F700 + byte in `TransferReport.RedirectUrl` and `EffectiveUrl`, and they reach `TransferInfo` and the libcurl string readers (`Libcurl/Info/Strings/EasyInfoReaders.Strings.cs`, `ReadRedirectUrl` and `ReadEffectiveUrl`) unchanged. Converting them there with `HeaderByteText.Unmark`, Latin-1 bytes and `UrlByteText.ToText` gives libcurl's byte answer; `Curl.Console/EasyHandleWriteOutInfo.cs` reads these back for `-w`, and `WriteOutTemplateRenderer` writes a lone U+DC80 to U+DCFF as its byte too (ADR-0822), so `%{redirect_url}` keeps writing E9 - re-run BL-3867's repro (Notes there) to confirm. Check `Curl.Api`'s `CurlHttpMessageHandler.Behaviour.cs` use of `EffectiveUrl` as well.

## Acceptance criteria

- [x] A Libcurl unit test reads `CURLINFO_REDIRECT_URL` after a transfer whose report holds a marked byte and gets U+DCE9 (byte text for E9).
- [x] BL-3867's repro still writes E9 in `%{redirect_url}` and `%{url_effective}` through the console.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Libcurl readers now convert marked header bytes to UrlByteText (U+DCxx); Console reads via these readers and the renderer writes U+DC80..DCFF as its byte (ADR-0822), so -w keeps E9. Curl.Api is outside touches and unchanged. Libcurl quality run: 0 failing members.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 52c00312 4b1d76f6 779790c0.
- 2026-10-28: Doing -> Done. Libcurl REDIRECT_URL and EFFECTIVE_URL give marked header bytes as UrlByteText
