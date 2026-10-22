---
id: BL-2620
title: Make CURLOPT_MIMEPOST set to NULL send a zero-byte POST in upstream cases 584 and 589
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2620 — Make CURLOPT_MIMEPOST set to NULL send a zero-byte POST in upstream cases 584 and 589

## Goal

Upstream cases 584 and 589 (lib589) pass: setting `CURLOPT_MIMEPOST` to NULL leaves the handle making a zero-byte POST, as libcurl's setopt sets the POST method whatever the mime pointer is.

## Context

Found by BL-2533: both cases run, but `<verify><protocol>` differs: `expected "POST /589 HTTP/1.1\r\n", got "GET /589 HTTP/1.1\r\n"` (584 at line 12, its second request). `Lib589` matches lib589.c and is unit tested. Start at `Libcurl/Mime/Mime/EasyOptionSetters.MimePost.cs`: a null value clears the body but does not make the request a POST with an empty body (curl 8.22.0 `lib/setopt.c`, `CURLOPT_MIMEPOST` sets `data->set.method = HTTPREQ_POST_MIME`). If the engine also needs a change, add it to `touches` in the first pass.

## Acceptance criteria

- [x] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Fix 1 (Libcurl): `CURLOPT_MIMEPOST` NULL now leaves an empty `HttpPost` form (the path the engine already posts as a zero-byte body with no `Content-Type`, as for a freed MIME structure) and clears `CURLOPT_NOBODY`; a MIME handle clears `NOBODY` too, as curl 8.22.0's `setopt.c` sets `opt_no_body = FALSE`. That made case 589 pass.
- Fix 2 (Curl.Protocol.Http): case 584's second request then went nowhere: the first reply is `swsclose`, the reused connection was dead, and `CanSendAgainOnFreshConnection` refused to resend any `StreamBody`. A zero-length stream body has nothing to rewind, so it is now resent on a fresh connection like a bytes body (`StreamBody { Length: not 0 }` still refused, an unknown length too). Added `Curl.Protocol.Http.UnitLibrary` and `Curl.Protocol.Http.UnitTests` to `touches`: no task in Doing on origin/work/dark-factory named them.
- Tests: `EasyMimePostSetterTests` (NULL leaves an empty form and clears NoBody; a handle clears NoBody) and `HttpProtocolHandlerTests.ExecuteAsync_ReusedConnectionDiedAfterAnEmptyStreamBody_SendsTheRequestAgain`.
- Cases 584 and 589 pass through the runner and are on the ratchet; both are plain HTTP to the in-memory server with no path, file or certificate, so nothing depends on the platform.
- Measure-CodeQuality.ps1 -Library Libcurl,Curl.Protocol.Http.UnitLibrary: 0 failing members. Fast tests: all 42 test projects pass.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef 4963d26c 21e5ddeb 4272a6b9.
- 2026-10-18: Doing -> Done. CURLOPT_MIMEPOST NULL sends a zero-byte POST, resent on a fresh connection when the reused one died; upstream 584 and 589 on the ratchet
