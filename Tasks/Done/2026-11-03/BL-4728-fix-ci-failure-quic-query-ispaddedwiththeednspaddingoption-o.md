---
id: BL-4728
title: Fix CI failure Quic_Query_IsPaddedWithTheEdnsPaddingOption on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4728 — Fix CI failure Quic_Query_IsPaddedWithTheEdnsPaddingOption on Linux and macOS and Windows

## Goal

`Quic_Query_IsPaddedWithTheEdnsPaddingOption` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Quic_Query_IsPaddedWithTheEdnsPaddingOption` failed on Linux and macOS and Windows in CI run 41634593982 (https://github.com/StewartScottRogers/Curl/actions/runs/41634593982). First failing commit: 62178b13.

    Test method Curl.Conformance.Extended.UnitTests.DnsQuery.Encrypted.DnsOverQuicCases.Quic_Query_IsPaddedWithTheEdnsPaddingOption threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41634593982 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Quic_Query_IsPaddedWithTheEdnsPaddingOption` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- 2026-11-03: Nothing to change. CI run 41634593982 ran 62178b13, where RFC9250-5.4-1 was still in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` although BL-4718 had made it pass. 7e2b2728a (BL-4720), which landed after that run, removed the line. The test now passes locally, as do all 6 DnsOverQuicCases and the whole Curl.Conformance.Extended.UnitTests project (1719 passed, 1 skipped). That fix only removes a data line, so it does not depend on the platform. Per lane rule 11, the CI criterion is met by that and the shift's CI watch.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: bebe6e3e.
- 2026-11-03: Doing -> Done. Quic_Query_IsPaddedWithTheEdnsPaddingOption passes; 7e2b2728a removed the stale RFC9250-5.4-1 known failure
