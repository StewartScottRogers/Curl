---
id: BL-2620
title: Make CURLOPT_MIMEPOST set to NULL send a zero-byte POST in upstream cases 584 and 589
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2620 — Make CURLOPT_MIMEPOST set to NULL send a zero-byte POST in upstream cases 584 and 589

## Goal

Upstream cases 584 and 589 (lib589) pass: setting `CURLOPT_MIMEPOST` to NULL leaves the handle making a zero-byte POST, as libcurl's setopt sets the POST method whatever the mime pointer is.

## Context

Found by BL-2533: both cases run, but `<verify><protocol>` differs: `expected "POST /589 HTTP/1.1\r\n", got "GET /589 HTTP/1.1\r\n"` (584 at line 12, its second request). `Lib589` matches lib589.c and is unit tested. Start at `Libcurl/Mime/Mime/EasyOptionSetters.MimePost.cs`: a null value clears the body but does not make the request a POST with an empty body (curl 8.22.0 `lib/setopt.c`, `CURLOPT_MIMEPOST` sets `data->set.method = HTTPREQ_POST_MIME`). If the engine also needs a change, add it to `touches` in the first pass.

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] Any product fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] No added case's output depends on the platform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
