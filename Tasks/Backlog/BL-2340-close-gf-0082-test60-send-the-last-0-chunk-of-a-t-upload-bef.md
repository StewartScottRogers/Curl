---
id: BL-2340
title: Close GF-0082 test60: send the last 0-chunk of a -T - upload before the gap harness's server answers on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2340 — Close GF-0082 test60: send the last 0-chunk of a -T - upload before the gap harness's server answers on Linux and macOS

## Goal

A later gap analysis measures `behaviour:test60` (`-T - -H 'Content-Length: 1'`, stdin `more than one byte`) as `match` on Linux and macOS.

## Context

- Gap finding GF-0082, split out of BL-2337. BL-2337 closed the finding's other two items, test1072 and test1073 (ADR-0520).
- The finding: on Linux, the request Record-CurlExchange records from Curl stops at byte 179, where the closing `0\r\n\r\n` chunk should start. On macOS, Curl exits 56 because it writes that chunk after the server has closed. The reference sends the closing chunk and exits 0.
- BL-2337 could not reproduce this outside the gap harness. A self-contained `linux-x64` publish of Curl, run in WSL Ubuntu against `nc`, sent the closing chunk and exited 0, byte for byte like `/usr/bin/curl`. That held with stdin from a shell pipe, and also with stdin written 300 ms after start, stdout and stderr redirected.
- So the difference needs the harness: stdin written by pwsh's `Process.StandardInput` and then closed (Record-CurlExchange.ps1, around line 3150), the cross-check's 3-second idle server, or the native AOT build.
- Start by running `pwsh -File Gap/Tools/Measure-ReferenceCrossCheck.ps1` for test60 on Linux. Lanes cannot read `Gap/`, so this is a CI or interactive step: `gh workflow run gap-platforms.yml -f ref=<branch>`. Then trace what Curl's `HttpRequestBodyWriter.WriteStreamAsync` second stdin read returns, and when.

## Acceptance criteria

- [ ] The cause is found and recorded under Notes, with the measurement that shows it.
- [ ] `behaviour:test60` measures `match` on Linux and macOS in a gap-platforms run on the branch.
- [ ] A focused unit test in the fixed library's `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-15: Created.
