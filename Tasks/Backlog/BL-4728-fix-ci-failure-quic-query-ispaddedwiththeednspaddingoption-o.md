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
completed:
---
# BL-4728 — Fix CI failure Quic_Query_IsPaddedWithTheEdnsPaddingOption on Linux and macOS and Windows

## Goal

`Quic_Query_IsPaddedWithTheEdnsPaddingOption` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Quic_Query_IsPaddedWithTheEdnsPaddingOption` failed on Linux and macOS and Windows in CI run 41634593982 (https://github.com/StewartScottRogers/Curl/actions/runs/41634593982). First failing commit: 62178b13.

    Test method Curl.Conformance.Extended.UnitTests.DnsQuery.Encrypted.DnsOverQuicCases.Quic_Query_IsPaddedWithTheEdnsPaddingOption threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41634593982 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Quic_Query_IsPaddedWithTheEdnsPaddingOption` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-03: Created.
