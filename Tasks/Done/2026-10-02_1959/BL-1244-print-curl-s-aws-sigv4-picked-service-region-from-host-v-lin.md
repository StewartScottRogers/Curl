---
id: BL-1244
title: Print curl's 'aws_sigv4: picked service/region from host' -v lines before the String to sign line
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1227]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1244 — Print curl's 'aws_sigv4: picked service/region from host' -v lines before the String to sign line

## Goal

`curl -v --aws-sigv4 aws:amz -u AKID:secret http://s3.eu-west-1.localhost:<p>/` writes `* aws_sigv4: picked service s3 from host` and `* aws_sigv4: picked region eu-west-1 from host` after `* using HTTP/1.x` and before `* aws_sigv4: String to sign (enclosed in []) - [...`, as curl 8.21.0 does.

## Context

- BL-1227 put the lines on `AwsSigV4SigningResult.PickedFromHostLines` (Curl.Authentication.UnitLibrary): both lines in order for `aws:amz`, only the service line when the parameter names the region, none when it names both or on failure.
- `Curl.Console/AwsSigV4HttpAuthenticator.cs` already writes the `String to sign` and `Signature` lines from the result; it should write each `PickedFromHostLines` entry as an informational `-v` line first.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Port <p> -CurlArgs '-v','--aws-sigv4','aws:amz','-u','AKID:secret','http://s3.eu-west-1.localhost:<p>/'`: `* using HTTP/1.x`, `* aws_sigv4: picked service s3 from host`, `* aws_sigv4: picked region eu-west-1 from host`, `* aws_sigv4: String to sign (enclosed in []) - [AWS4-HMAC-SHA256 ...`, `* aws_sigv4: Signature - ...`, exit 0.
- Upstream: `lib/http_aws_sigv4.c` lines 850-869 at `curl-8_21_0`.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests` pins the two picked lines, in order, before the `String to sign` line for `aws:amz` and host `s3.eu-west-1.localhost`.
- [x] A test pins that `aws:amz:us-east-1:s3` writes no picked line.
- [x] Without `-v` no picked line is written.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member.

## Notes

- `AwsSigV4HttpAuthenticator.Sign` reports each `PickedFromHostLines` entry as an informational line before the string to sign. A custom `Authorization` header or a failed signing carries no picked lines, so nothing extra is printed there, as in curl, which returns before parsing the host.
- Tests: `AwsSigV4HttpAuthenticatorTests.CreateAuthorization_ServiceAndRegionFromTheHost_ReportsThePickedLinesBeforeTheStringToSign` and three end-to-end `CurlCompositionAwsSigV4Tests` (`-v aws:amz`, `-v aws:amz:us-east-1:s3`, and `aws:amz` without `-v`).
- `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line and branch coverage; the one failing member, `CurlCommandRunner.TransferUrlAsync` (complexity 12), was already failing before this task, is in a file it does not touch, and is filed as BL-1247.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. curl -v --aws-sigv4 prints the 'aws_sigv4: picked service/region from host' lines before the String to sign line
