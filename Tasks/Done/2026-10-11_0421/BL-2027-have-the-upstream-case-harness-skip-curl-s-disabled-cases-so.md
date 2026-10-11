---
id: BL-2027
title: Have the upstream-case harness skip curl's DISABLED cases, so GF-0062's test938 measures excluded
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Smtp.UnitTests]
lane: no
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2027 — Have the upstream-case harness skip curl's DISABLED cases, so GF-0062's test938 measures excluded

## Goal

The gap harness that runs upstream's `tests/data` cases measures every case listed in that tarball's `tests/data/DISABLED` as `excluded` (reason: disabled upstream), so GF-0062's only item, `behaviour:test938`, stops measuring as a gap and GF-0062 can close.

## Context

Interactive only: the gap harness (`Measure-UpstreamCases.cs` in the gap office's tools) and the upstream cases are audit-guarded (ADR-0433), so no lane may read or change them. The Curl project in `touches` is only a placeholder that keeps the board's overlap check meaningful; this task changes nothing in it.

BL-1992 found GF-0062 is not a Curl defect (ADR-0470):

- curl 8.21.0 lists `938` in `tests/data/DISABLED` (https://raw.githubusercontent.com/curl/curl/curl-8_21_0/tests/data/DISABLED), so upstream's own test suite does not run it: its `<protocol>` still expects the old `user NUL user NUL password` PLAIN message.
- Real curl 8.21.0 (Schannel, Windows) measured with `Record-CurlExchange.ps1 -Smtp -SmtpReply 'EHLO=250-localhost\r\n250 AUTH PLAIN'` and `-u user.one:secret smtp://127.0.0.1:<port>/938001` sends `AUTH PLAIN`, then `AHVzZXIub25lAHNlY3JldA==` (`NUL user.one NUL secret`), byte for byte what Curl sends.
- curl 8.21.0's `lib/vauth/cleartext.c` builds the message from `Curl_creds_sasl_authzid(creds)`, empty without `--sasl-authzid`.

The same check likely moves other findings' items to `excluded`: 323, 594, 836 and 882 are also in that DISABLED list.

## Acceptance criteria

- [x] The harness reads `tests/data/DISABLED` (lines that are not blank and do not start with `#`) and measures each listed case as `excluded` with the reason `disabled upstream`.
- [x] A rerun of the harness for case 938 reports it `excluded`, and GF-0062 is closed on the `gap` branch under ADR-0433's rule for `excluded` items.

## Notes

- 2026-10-10 (interactive): the reason string is `disabled-upstream`, hyphenated like the other excluded reasons, and both format documents list it. GF-0062 closed on the 2026-10-10_2142 re-measurement (gap PR #108).

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. The gap tool skips upstream's DISABLED cases and measures them excluded (disabled-upstream); GF-0062 closed (gap PR #108, 9af0622a5)
