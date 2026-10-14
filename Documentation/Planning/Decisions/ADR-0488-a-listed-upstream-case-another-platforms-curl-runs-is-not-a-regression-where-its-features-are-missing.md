# ADR-0488 — A listed upstream case another platform's Curl runs is not a regression where its features are missing

- **Status:** Accepted
- **Date:** 2026-10-13

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-2176.

## Context

`PassingUpstreamCases.txt` is one list for every platform, and the ratchet of ADR-0013 decision 5
fails a listed case that is skipped. Curl matches curl's Schannel build on Windows and its OpenSSL
build on Linux and macOS (`UpstreamCurlPlatform`), so some upstream cases can run on one platform
only: test2087 and test3023 name `Schannel` in `<features>` and pass on Windows, but off Windows
they are skipped with "Curl lacks the feature Schannel", so listing them would turn the Linux and
macOS CI jobs red. Leaving them unlisted lets them regress on Windows unnoticed.

## Decision

- A case is skipped *on this platform only* when this platform's Curl does not meet its
  `<features>` but another platform's Curl (`UpstreamCurlPlatform.All`: Windows, Linux, macOS)
  meets every one of them. `UpstreamCaseScreening.FindFeatureOfAnotherPlatform` decides it, and
  `UpstreamCaseRunner` checks it before any other screening, returning
  `UpstreamCaseOutcome.SkippedOnThisPlatform` (kind `Skipped`, `RunsOnlyOnAnotherPlatform` set).
- `UpstreamCaseRatchet.Judge` reports a listed case skipped on this platform only as
  `Inconclusive` ("is on PassingUpstreamCases.txt and runs only on another platform"), not `Fail`;
  it is held to the list on the platforms that can run it.
- A listed case skipped for any other reason still fails, including one needing a feature no
  platform's Curl has (such as `Debug`), so a feature dropped everywhere cannot hide a regression.
- test2087 and test3023 are listed.

The list stays one file rather than one per platform: the feature list already says where a case
can run, so a per-platform list would repeat it and drift from it.

## Consequences

- Off Windows, an unlisted case needing `Schannel` now reports its feature as its skip reason even
  when another screening reason would also apply; it was `Inconclusive` before and still is.
- A case whose `<features>` are met nowhere is screened exactly as before.
