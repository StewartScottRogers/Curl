---
id: BL-3946
title: Fix CI failure test1034: on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3885]
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostReplacementCharacterTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3946 — Fix CI failure test1034: on Linux and macOS

## Goal

`test1034:` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test1034:` failed on Linux and macOS in CI run 40724749646 (https://github.com/StewartScottRogers/Curl/actions/runs/40724749646). First failing commit: 158f6fe6.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 40724749646 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test1034:` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes
- Diagnosis (lane 2, 2026-10-28): CI's ubuntu log says `<verify><errorcode>: expected exit code 3, got 7`. Cause is 1e7e2ecd4 (BL-3884, ADR-0942), not 158f6fe6: off Windows `CurlCommandRunner` reads a `-K` config file as UTF-8, so test1034's bytes `e2 90` reach the host as `U+FFFD`. `CurlUrlHost.EncodeLabel` refuses only a lone surrogate or a control character, so `U+FFFD` is punycode-encoded and the transfer tries the proxy (exit 7). On Windows the ANSI read gives `â` + `U+0090` (control), refused, exit 3.
- Fix: in `CurlUrlHost.EncodeLabel` also refuse `U+FFFD` (UTS #46 disallows it, so libidn2 refuses it too, and in a UTF-8 locale invalid UTF-8 is unconvertible as ADR-0942 says); add a focused unit test (ADR-0482) that `invalid-utf8-�.local` with refuseUnconvertibleName gives `IdnConversionFailed`. Note the change in ADR-0942's Decision.
- Touches widened to `CurlUrlHost.cs` and a new test file because the fix lands there; BL-3885 (Doing) holds `CurlUrlHost.cs`, so this task depends on it and waits in Backlog.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 1a4e3b30.
- 2026-10-28: Doing -> Backlog. Waits on BL-3885, which holds Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs where the U+FFFD refusal lands
- 2026-10-28: Backlog -> Doing. Criteria at claim: 1a4e3b30.
- 2026-10-28: Doing -> Blocked. Fix landed in d620937b7; waiting on the CI workflow to pass on Linux and macOS for it. The interactive session that made the fix moves it to Done when CI is green.
