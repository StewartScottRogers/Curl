---
id: BL-2008
title: Re-close GF-0015: --retry does not follow a 429's Retry-After as curl does (resend, --fail, --retry-max-time warning)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2008 — Re-close GF-0015: --retry does not follow a 429's Retry-After as curl does (resend, --fail, --retry-max-time warning)

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0015 (--retry does not follow a 429's Retry-After as curl does (resend, --fail, --retry-max-time warning)), so a later gap analysis measures each of `behaviour:test366`, `behaviour:test1633`, `behaviour:test1634` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0015 ([BL-1808]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0015, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test366`, `behaviour:test1633`, `behaviour:test1634`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test1633 (-d moo --retry 1 -L): the --output file against <reply><data> differs at byte 177 (line 12): expected 'HTTP/1.1 301 OK', got the end. test1634 (--retry 1 --fail): expected 'HTTP/1.1 429 too many requests swsbounce', got 'HTTP/1.1 200 OK'. test366 (--retry 2 --retry-max-time 10 with a too-long Retry-After): stderr differs from the reference curl, which exits 0. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 366,1633,1634

Suggestion, copied from the finding:

In Curl.Console's retry loop: on a 429 with Retry-After, keep the 429's output when the retry is made (test1634 under --fail). Resend the POST and then follow -L (test1633). When Retry-After exceeds --retry-max-time, give up the retry without a message, matching the reference curl's stderr for test366.

## Acceptance criteria

- [ ] `behaviour:test366`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test1633`: Curl answers what curl 8.21.0 answers, `upstream test1633 passes`, so the item measures `match`.
- [ ] `behaviour:test1634`: Curl answers what curl 8.21.0 answers, `upstream test1634 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
