---
id: BL-2685
title: Run upstream AWS SigV4 libtest cases 1970 to 1975 with the harness's Debug stand-in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2675]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2685 — Run upstream AWS SigV4 libtest cases 1970 to 1975 with the harness's Debug stand-in

## Goal

Upstream cases 1970, 1971, 1972, 1973, 1974 and 1975 run (not skipped) through the case runner, and those that pass on Windows hold the ratchet.

## Context

Found by BL-2548, which ported lib1970, lib1971, lib1973, lib1974 and lib1975 as `LibAwsSigV4S3Request` and lib1972 as `Lib1972` in `Curl.Conformance.LibTest.UnitLibrary` (registered in `LibTestPrograms.Ported`, unit tested over a fake engine). Each case is skipped: `test1970 is skipped: Curl lacks the feature Debug`; the Debug build fixes the SigV4 signing time. BL-2675 extends the harness's Debug stand-in in `UpstreamConformanceTests.cs` to the `aws` cases 1933 to 1938; once it is Done, run these six the same way. They sign with `aws:amz:us-east-1:s3` and differ in body (upload with size 0, upload through a read callback answering 0, a MIME post, a POST, no body, and an upload with a caller-set `X-Amz-Content-Sha256`), so they check the content hash S3 signing carries. Any case that still fails is left off and filed with its differing lines.

## Acceptance criteria

- [ ] Each of cases 1970 to 1975 runs (not skipped) through the case runner on Windows; each that passes is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, and each that fails is filed as a task with its differing lines.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

## Log

- 2026-10-17: Created.
