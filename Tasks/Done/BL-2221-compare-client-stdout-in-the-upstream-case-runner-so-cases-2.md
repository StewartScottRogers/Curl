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
completed: 2026-10-14
---
# BL-2221 — Compare <client><stdout> in the upstream case runner so cases 210, 211, 212 and 407 run

## Goal

Make the upstream case runner act on a case's `<client><stdout>` expectation so cases 210, 211, 212 and 407 run instead of being screened "the harness does not act on <client><stdout>".

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; the 8.22.0 gap run 2026-10-13_1406 had 38 `harness-unsupported` and 5 `needs-server:ftp` cases, and running `Curl.Conformance.UnitTests` on 2026-10-14 names 210, 211, 212 (ftp) and 407 (ftps) for this part. The gap tool uses `Curl.Conformance.UnitLibrary`.

In `UpstreamCaseScreening`, `ClientParts` omits `stdout`. In upstream's `FILEFORMAT.md` (curl-8_22_0, https://github.com/curl/curl/blob/curl-8_22_0/docs/tests/FILEFORMAT.md), check what `<client><stdout>` means: read test210.curltest (line 29), test211, test212 and test407 and `runtests.pl` / `singletest.pm`'s handling of it, before deciding. Where it is a verification, add it to `UpstreamCaseVerification` with the existing stdout comparison rules (`nonewline`, `crlf`, `mode="text"` via `UpstreamTestSectionLineEndings`); where it is input to the case, feed it as the run does. Record the decision in Notes.

## Acceptance criteria

- [x] Notes states, from `runtests.pl` at curl-8_22_0, what `<client><stdout>` means and where the harness now acts on it.
- [x] Cases 210, 211, 212 and 407 are no longer screened for `<client><stdout>`. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [x] Unit tests in `UpstreamCaseScreeningTests` / `UpstreamCaseVerificationTests` pin the new behaviour (a match and a mismatch); platform-neutral.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- What `<client><stdout>` means at curl-8_22_0: nothing. `tests/runtests.pl` reads stdout only as `getpart("verify", "stdout")` (lines 1276-1332), `tests/singletest.pm` never reads a `stdout` part, and `docs/tests/FILEFORMAT.md` documents `<stdout>` only under `<verify>`. In 210, 211, 212 and 407 the part sits in `<client>` by mistake and upstream never checks it.
- Decision (sensible default, matching upstream): the harness accepts the part and ignores it, as runtests.pl does - `stdout` joins `UpstreamCaseScreening.ClientParts`, and `UpstreamCaseVerification` is unchanged (it compares only `<verify><stdout>`). Comparing it would hold Curl to a check real curl's suite never makes. No ADR: a harness fidelity fix, not a product behaviour choice.
- Tests: `UpstreamCaseScreeningTests` (407 now screens to null; an inline case with `<client><stdout>` runs) and `UpstreamCaseVerificationTests.FindFirstDifference_ClientStdout_IsNotComparedWhetherOrNotTheOutputMatches` (matching and mismatching output both give no difference).
- Results: 210 passes and is on `PassingUpstreamCases.txt`. Real gaps: 211 (`expected "PASV", got "EPSV"` on the reused connection) and 212 (`expected "PORT ", got "EPRT |1|"`) - BL-2236; 407 (`expected "PROT C", got "PROT P"`, the same cause as 400, 401, 403, 406, 408, 1112) - BL-2237.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary: 0 failing members.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. Harness ignores <client><stdout> as runtests.pl does; 210 passes, 211/212/407 run and fail on real gaps (BL-2236, BL-2237)
