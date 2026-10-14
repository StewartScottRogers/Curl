---
id: BL-2221
title: Compare <client><stdout> in the upstream case runner so cases 210, 211, 212 and 407 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2221 — Compare <client><stdout> in the upstream case runner so cases 210, 211, 212 and 407 run

## Goal

Make the upstream case runner act on a case's `<client><stdout>` expectation so cases 210, 211, 212 and 407 run instead of being screened "the harness does not act on <client><stdout>".

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; the 8.22.0 gap run 2026-10-13_1406 had 38 `harness-unsupported` and 5 `needs-server:ftp` cases, and running `Curl.Conformance.UnitTests` on 2026-10-14 names 210, 211, 212 (ftp) and 407 (ftps) for this part. The gap tool uses `Curl.Conformance.UnitLibrary`.

In `UpstreamCaseScreening`, `ClientParts` omits `stdout`. In upstream's `FILEFORMAT.md` (curl-8_22_0, https://github.com/curl/curl/blob/curl-8_22_0/docs/tests/FILEFORMAT.md), check what `<client><stdout>` means: read test210.curltest (line 29), test211, test212 and test407 and `runtests.pl` / `singletest.pm`'s handling of it, before deciding. Where it is a verification, add it to `UpstreamCaseVerification` with the existing stdout comparison rules (`nonewline`, `crlf`, `mode="text"` via `UpstreamTestSectionLineEndings`); where it is input to the case, feed it as the run does. Record the decision in Notes.

## Acceptance criteria

- [ ] Notes states, from `runtests.pl` at curl-8_22_0, what `<client><stdout>` means and where the harness now acts on it.
- [ ] Cases 210, 211, 212 and 407 are no longer screened for `<client><stdout>`. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [ ] Unit tests in `UpstreamCaseScreeningTests` / `UpstreamCaseVerificationTests` pin the new behaviour (a match and a mismatch); platform-neutral.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-14: Created.
