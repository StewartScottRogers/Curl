---
id: BL-1227
title: Carry curl's 'aws_sigv4: picked service/region from host' -v lines on AwsSigV4SigningResult
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1226]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1227 — Carry curl's 'aws_sigv4: picked service/region from host' -v lines on AwsSigV4SigningResult

## Goal

`AwsSigV4Signer.Sign` returns, on its `AwsSigV4SigningResult`, the `-v` lines curl 8.21.0 writes when `--aws-sigv4` leaves the service or region out and they are taken from the host name, `aws_sigv4: picked service <service> from host` and `aws_sigv4: picked region <region> from host`, in that order, so the HTTP caller can print them before the `String to sign` line it already prints.

## Context

- Today `Curl.Authentication.UnitLibrary/AwsSigV4Scope.cs` `Parse` takes the service and region from the host when the parameter lacks them (and fails with exit 3 `aws-sigv4: service missing in parameters and hostname` / `region missing ...` when it cannot) but records nothing for `-v`. `AwsSigV4SigningResult` (`AwsSigV4SigningResult.cs`) carries `HeaderLines`, `CanonicalRequest` and `StringToSign`, which `Curl.Console/AwsSigV4HttpAuthenticator.cs` turns into `aws_sigv4: String to sign (enclosed in []) - [...]` and `aws_sigv4: Signature - ...`.
- curl 8.21.0, `lib/http_aws_sigv4.c` lines 850-869 at `curl-8_21_0`: `infof "aws_sigv4: picked service %.*s from host"` once the service is read from the first host label, then `infof "aws_sigv4: picked region %.*s from host"` when the region is read from the second.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Port <p> -CurlArgs '-v','--aws-sigv4','aws:amz','-u','AKID:secret','http://s3.eu-west-1.localhost:<p>/'`: after `* using HTTP/1.x`, `* aws_sigv4: picked service s3 from host`, `* aws_sigv4: picked region eu-west-1 from host`, then `* aws_sigv4: String to sign (enclosed in []) - [AWS4-HMAC-SHA256 ...`, `* aws_sigv4: Signature - ...`, exit 0.
- This task changes only the Authentication library. Printing the lines needs `Curl.Console/AwsSigV4HttpAuthenticator.cs` to report them before the `String to sign` line; that is a separate follow-up task, filed once `Curl.Console` has no live work.

## Acceptance criteria

- [x] `AwsSigV4SigningResult` has a member, named for what it holds, listing the picked-from-host lines; it is empty for a failed or unsigned result.
- [x] Tests in `Curl.Authentication.UnitTests` pin: `aws:amz` with host `s3.eu-west-1.localhost` gives both lines in order; `aws:amz:us-east-1` with the same host gives only the service line (`s3`); `aws:amz:us-east-1:s3` gives none; the exit 3 failures give none.
- [x] Every existing SigV4 test passes unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- The member is `AwsSigV4SigningResult.PickedFromHostLines`. `AwsSigV4Scope.FromHost` records the lines on the scope as it picks each part, and `Signed` copies them to the result. `Failed` and `NotSigned` carry an empty list.
- Divergence from curl, kept on purpose: when the service comes from the host and the region then fails (`s3.localhost`), curl has already written `picked service s3 from host` before the exit 3 error. The criteria say a failed result carries no lines, so this one carries none. A failed result has no lines to print, and the line before the error is only `-v` noise. If byte parity on that path matters, it needs a separate task.
- Follow-up: BL-1244 prints the lines in `Curl.Console`.
- Measured: Curl.Authentication.UnitLibrary 100% line, 100% branch, 0 failing members, worst CRAP 10. Curl.Authentication.UnitTests: 785 passed, 4 skipped.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. AwsSigV4SigningResult.PickedFromHostLines carries curl's 'aws_sigv4: picked service/region from host' -v lines
