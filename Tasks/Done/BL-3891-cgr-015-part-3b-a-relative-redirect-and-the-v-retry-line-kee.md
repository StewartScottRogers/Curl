---
id: BL-3891
title: CGR-015 part 3b: a relative redirect and the -v retry line keep the command-line URL's path in the command-line text encoding
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3840]
touches: [Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs, Curl.Protocol.Http.UnitLibrary/HttpConnectionInfoLines.cs, Curl.Protocol.Http.UnitTests/Cgr015RedirectTests.cs, Curl.Protocol.Http.UnitLibrary/HttpUrlText.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3891 — CGR-015 part 3b: a relative redirect and the -v retry line keep the command-line URL's path in the command-line text encoding

## Goal

On Windows (cp1252), `-L` to a relative `Location` from `http://h/é/x` keeps the path as `/%E9/` (`GET /%E9/y` for `Location: y`), and `-v`'s "Issue another request to this URL" line prints the path the same way, as curl 8.22.0 does.

## Context

Follow-up from BL-3840 (CGR-015 part 3). BL-3840 made the request line encode a URL's path, and send its query, from `HttpRequestOptions.CommandLineTextEncoding`. Two places still read the command-line URL's path as UTF-8 through `HttpUrlText.Path(CurlUrl)` (`CurlUrl.RequestPath`): `HttpRedirectLocation.Resolve` (`TargetPath(HttpUrlText.Path(requestUrl), path)`, line ~74), which has no options today, and `HttpConnectionInfoLines` (line ~247). Real curl percent-encodes the command-line URL's path from its ANSI `argv` bytes when it parses the URL, so a relative redirect inherits `%E9`. Use `HttpUrlText.Path(CurlUrl, Encoding)` from BL-3840 and pass the encoding in. Measure first with `Record-CurlExchange.ps1 -Connections 2 -Response '<301 with Location: y and Connection: close>','<200>' -CurlArgs '-s','-L','--max-time','10','http://127.0.0.1:<port>/é/x'` (the second connection is needed, or the run hangs). BL-3840 measured `Location: /\xC3\xA9?q=\xC3\xA9` (a relative reference holding UTF-8 bytes) going out as `GET /%C3%A9?q=%C3%A9`, which is unchanged: `HttpRedirectLocation.Encode` makes such a reference ASCII.

## Acceptance criteria

- [x] With cp1252 injected, a relative redirect from `http://127.0.0.1:<port>/é/x` with `Location: y` sends the request line real curl on Windows sends (command and result in Notes), pinned in `Curl.Protocol.Http.UnitTests/Cgr015RedirectTests.cs`.
- [x] With UTF-8 the same redirect sends the same bytes as before.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` shows no member outside a gate.

## Notes

- Measured with Record-CurlExchange.ps1 -Connections 2 (301 `Location: y`, then 200; `-s -L http://127.0.0.1:21893/é/x`): real curl on Windows sent `GET /%E9/x` then `GET /%E9/y`. Pinned in Cgr015RedirectTests (cp1252 -> `/%E9/y`, UTF-8 -> `/%C3%A9/y` as before).
- `HttpRedirectLocation.Find/Resolve` and `HttpConnectionInfoLines.IssueAnotherRequest` take the encoding; HttpProtocolHandler passes `CommandLineTextEncoding`. `HttpUrlText.Path(CurlUrl)` became unused and was removed (touches widened to HttpUrlText.cs and HttpProtocolHandler.cs).
- Build clean, fast tests pass, Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: 100%/100%, 0 failing members. Linux/macOS: encoding is injected, tests platform-neutral.

## Log

- 2026-10-26: Created by BL-3840's run.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 6539f7d0 c664fab7 4979ce11.
- 2026-10-28: Doing -> Done. A relative redirect and the -v retry line keep the command-line URL's path in the command-line text encoding
