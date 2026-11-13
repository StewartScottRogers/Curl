---
id: BL-5153
title: Fix CI failure RunAsync_SecondRunAfterAnH3AltSvcHeader_ConnectsOverHttp3 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-5152]
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-11-13
completed:
---
# BL-5153 — Fix CI failure RunAsync_SecondRunAfterAnH3AltSvcHeader_ConnectsOverHttp3 on Linux and macOS

## Goal

`RunAsync_SecondRunAfterAnH3AltSvcHeader_ConnectsOverHttp3` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_SecondRunAfterAnH3AltSvcHeader_ConnectsOverHttp3` failed on Linux and macOS in CI run 43523918778 (https://github.com/StewartScottRogers/Curl/actions/runs/43523918778). First failing commit: 73f075a3.

    Test method Curl.Console.CurlCommandRunnerAltSvcVersionTests.RunAsync_SecondRunAfterAnH3AltSvcHeader_ConnectsOverHttp3 threw exception:

Lanes test only on Windows, so reproduce with `gh run view 43523918778 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_SecondRunAfterAnH3AltSvcHeader_ConnectsOverHttp3` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Root cause (all five alt-svc h3 tests in CurlCommandRunnerAltSvcVersionTests, not only this one): BL-5099 (c769d9e6e, ADR-1268) made AltSvcTransferCache ignore an h3 entry when no version option is given and `actsAsQuicBuild` is false; nothing in production sets it, so the Linux and macOS runs of these tests (Exclude Windows) find no QUIC connect ("curl: (7) unused"). The test runner's `parsesAsQuicBuild: true` only reaches the parser, not the engine. Fix options: add `--http3` or a QUIC-build flag in the tests, or pin the new behaviour.
- Requeued with depends-on BL-5152: it holds the same file (Curl.Console.UnitTests/CurlCommandRunnerAltSvcVersionTests.cs) for the same cause. Once it is Done, re-run the test list from CI run 43523918778 and fix what still fails (the others: RunAsync_H3EntryForAnotherPort_*, RunAsync_H3EntryForAnotherPortAndQuicFails_*, RunAsync_RedirectFromAnH3Alternative_*). No edits made.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b55f9dd5.
- 2026-11-13: Doing -> Backlog. Waits on BL-5152, which holds CurlCommandRunnerAltSvcVersionTests.cs for the same BL-5099 cause
