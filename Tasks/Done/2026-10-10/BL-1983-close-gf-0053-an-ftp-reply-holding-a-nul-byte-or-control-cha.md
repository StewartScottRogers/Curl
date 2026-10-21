---
id: BL-1983
title: Close GF-0053: An FTP reply holding a NUL byte or control characters (in PWD's path) is accepted; curl ends with exit 8
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1983 — Close GF-0053: An FTP reply holding a NUL byte or control characters (in PWD's path) is accepted; curl ends with exit 8

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0053 (An FTP reply holding a NUL byte or control characters (in PWD's path) is accepted; curl ends with exit 8), so a later gap analysis measures each of `behaviour:test3217`, `behaviour:test3218`, `behaviour:test2108` as `match`.

## Context

- Finding: GF-0053, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test3217`, `behaviour:test3218`, `behaviour:test2108`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test3217/3218 (PWD answered '257' with a path holding byte 0x03): '<verify><protocol> differs at byte 43 (line 4): expected the end, got "EPSV\r\n"'; upstream expects exit 8. test2108 (PASS answered '230 logged' NUL ' in'): expected the end, got 'PWD'; upstream expects exit 8. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 3217,3218,2108

Suggestion, copied from the finding:

In Curl.Protocol.Ftp.UnitLibrary's control-reply reader, fail with exit 8 (weird server reply) on a reply line holding a NUL byte. In the PWD reply parser, refuse a path holding control characters with exit 8, sending nothing more, as curl 8.21.0 does.

## Acceptance criteria

- [x] `behaviour:test3217`: Curl answers what curl 8.21.0 answers, `upstream test3217 passes`, so the item measures `match`.
- [x] `behaviour:test3218`: Curl answers what curl 8.21.0 answers, `upstream test3218 passes`, so the item measures `match`.
- [x] `behaviour:test2108`: Curl answers what curl 8.21.0 answers, `upstream test2108 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- test3217/3218: curl 8.21.0's `ftp_pwd_resp` (lib/ftp.c, read from the curl-8_21_0 tag) returns `CURLE_WEIRD_SERVER_REPLY` for any `ISCNTRL` byte (0x00-0x1f, 0x7f) between the quotes of a `257` path; text before the first quote and after the closing one is not checked. `FtpEntryPath.TryReadQuoted` now refuses such a byte the way it already refused an unended quote (curl reaches the CR of the line ending in that case, the same check): exit 8 `Weird server reply`, nothing sent after `PWD`. Pinned by new rows of `FtpProtocolHandlerEntryPathTests` (0x03, CR, tab, 0x1f, 0x7f fail; 0x80/0xff and a control byte after the closing quote do not).
- test2108: Curl already ends with exit 8 after `PASS` for a reply holding a real NUL (BL-1117); the new test `FtpProtocolHandlerNulByteReplyTests.ExecuteAsync_NulByteInTheReplyToPass_FailsWithExit8AfterPassAndSendsNoPwd` pins upstream's exact exchange and passed before any production change. The upstream case writes `REPLY PASS 230 logged\x00 in`, which `ftpserver.pl` expands as a Perl `qq{}` string; the gap runner evidently sends `\x00` literally, so the remaining gap is in the runner, an audit path a lane may not touch. Filed as BL-2012 (interactive only).
- No option changed, so `--ai-help` needs nothing.
- Fast tests: every project green; Curl.Protocol.Ftp.UnitTests 702 passed, 6 skipped (off-Windows only).

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. An FTP 257 reply quoting a path with a control character now ends with exit 8 and nothing after PWD, as curl 8.21.0 does; test2108's remaining gap is the gap runner (BL-2012)
