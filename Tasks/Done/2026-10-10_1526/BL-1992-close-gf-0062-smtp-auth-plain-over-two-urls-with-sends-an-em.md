---
id: BL-1992
title: Close GF-0062: SMTP AUTH PLAIN over two URLs with -: sends an empty authorization identity; upstream expects user, user, password
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1992 — Close GF-0062: SMTP AUTH PLAIN over two URLs with -: sends an empty authorization identity; upstream expects user, user, password

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0062 (SMTP AUTH PLAIN over two URLs with -: sends an empty authorization identity; upstream expects user, user, password), so a later gap analysis measures each of `behaviour:test938` as `match`.

## Context

- Finding: GF-0062, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test938`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test938 (two smtp URLs joined by -:, -u user.one:secret then user.two:secret) expected 'upstream test938 passes', actual '<verify><protocol> differs at byte 25 (line 3): expected "dXNlci5vbmUAdXNlci5vbmUAc2VjcmV0\r\n", got "AHVzZXIub25lAHNlY3JldA==\r\n"': curl sends 'user.one NUL user.one NUL secret' and Curl sends 'NUL user.one NUL secret'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 938

Suggestion, copied from the finding:

Measure test938's command line against the reference with Record-CurlExchange.ps1 -Smtp to confirm when curl 8.21.0 fills PLAIN's authorization identity with the user name. Then make Curl.Authentication.UnitLibrary's PLAIN message (and Curl.Protocol.Smtp.UnitLibrary's SmtpSaslAuthentication) build it the same way, keeping the empty identity where test833-style cases expect it.

## Acceptance criteria

- [x] `behaviour:test938`: Curl answers what curl 8.21.0 answers. Measured: real curl 8.21.0 sends `AHVzZXIub25lAHNlY3JldA==` (`NUL user.one NUL secret`), the same as Curl; test938 is in curl's own `tests/data/DISABLED`, so it cannot measure `match` against a real curl and instead measures `excluded` once BL-2027 lands (ADR-0470). Criterion reworded from "the item measures `match`", which no drop-in replacement can meet.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). No option changed.

## Notes

- Measured real curl 8.21.0 (Schannel, `/mingw64/bin/curl`) with
  `Record-CurlExchange.ps1 -Port 18927 -Smtp -SmtpReply 'EHLO=250-localhost\r\n250 AUTH PLAIN' -CurlArgs '-sS','--mail-from','a@b','--mail-rcpt','c@d','-T',<file>,'-u','user.one:secret','smtp://127.0.0.1:18927/938001'`:
  `AUTH PLAIN`, `334 `, `AHVzZXIub25lAHNlY3JldA==`, `235`. That is exactly the "got" side of GF-0062's evidence.
- Upstream (https://raw.githubusercontent.com/curl/curl/curl-8_21_0/tests/data/DISABLED) lists 938, and 8.21.0's
  `lib/vauth/cleartext.c` builds PLAIN from `Curl_creds_sasl_authzid`, empty without `--sasl-authzid`; test833 expects
  `%00user%00secret`. test938's `<protocol>` is stale upstream data, not curl's behaviour.
- Decision (ADR-0470): no code change; Curl keeps matching real curl, pinned already by
  `SaslAuthenticatorTests.Begin_Plain_InitialResponseMatchesCurl` (`AHUAcA==` for `-u u:p`).
- The finding closes through the harness: BL-2027 (interactive only, the harness is an audit path) has it measure
  every case in `tests/data/DISABLED` as `excluded`. This task is Done rather than parked behind BL-2027, because no
  Curl work is left for a lane to pick up.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Curl already sends what curl 8.21.0 sends (measured); test938 is disabled upstream, harness exclusion filed as BL-2027 (ADR-0470).
