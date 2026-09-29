---
id: BL-629
title: Parse --aws-sigv4 and sign HTTP requests with it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-628]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-629 — Parse --aws-sigv4 and sign HTTP requests with it

## Goal

`--aws-sigv4 <provider-spec>` with `-u key:secret` makes every HTTP request of the transfer carry the headers BL-628's signer produces, in the header positions curl 8.21.0 uses, overriding Basic as curl does.

## Context

- Conformance audit 2026-09-28, row 22 (Major). Signer: BL-628.
- Parse in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`; the HTTP authenticator is composed in `Curl.Console` (`IHttpAuthenticator`, ADR-0014); header order rules are FR-066 and ADR-0022. Decide in the XML docs whether signing happens in an authenticator or in the request options, choosing whichever lets the signer see the final headers and body.
- Use BL-628's measurements; add a case with `-H` custom headers and one with a redirect (`-L`) if curl re-signs.

## Acceptance criteria

- [ ] `Curl.Cli.UnitTests` cover parsing; `Curl.Console.UnitTests` pin the request bytes for BL-628's measured cases (fixed time) and the extra cases above, measured first with `Record-CurlExchange.ps1`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-29 (lane 3): `touches` widened to `Curl.Protocol.Abstractions.UnitLibrary`,
  `Curl.Protocol.Http.UnitLibrary` and their tests. Why: signing cannot be wired from
  `Curl.Console` alone without breaking a contract.
  - `IHttpAuthenticator` returns one `Authorization` *value* ("without the header name or
    line ending"). SigV4 must put three lines in curl's `Authorization` slot
    (`Authorization`, `X-Amz-Date`, `x-amz-content-sha256`, right after `Host`, before
    `User-Agent`). Putting CRLFs in the value would work on the wire but break the documented
    contract. Moving them to `-H` custom headers would put them after `Accept`, not where
    curl puts them.
  - `HttpProtocolHandler.ExecuteAsync` calls `CreateAuthorizationAsync` pre-emptively and does
    not catch `HttpAuthenticationFailedException` there (only `AnswerChallengesAsync` does).
    Without that catch, the signer's errors (exit 3, 27, 43, 100) cannot end the transfer
    cleanly.
  - BL-819 (in Doing) touches both projects, so this task goes back to Backlog until it is Done.
- Plan for the next run (decide and record in an ADR). Decision taken now: sign in an
  authenticator, not in the request options, because the authenticator is asked for every
  request, redirects included, and curl re-signs every hop (measured below).
  1. Abstractions: let an authenticator return the extra header lines that go after
     `Authorization`. For example, an `HttpAuthorization` result with `Value` and
     `ExtraHeaderLines`, or a new `IHttpAuthenticator` member with a default that returns none.
  2. Http: write those lines in the `Authorization` slot, and catch
     `HttpAuthenticationFailedException` on the pre-emptive call too.
  3. Console: a per-transfer `AwsSigV4HttpAuthenticator` holds the `-H` headers, the
     `-d` bytes, the `-T` size, `--path-as-is` and the `--aws-sigv4` value. It maps each
     `HttpAuthRequest` (method, URL, credential) to an `AwsSigV4Request`.
  4. Cli: parse `--aws-sigv4 <string>` (in `CurlHelpTable` and `CurlOptionAliasTable`
     already); it makes the auth scheme SigV4, overriding Basic.
  5. Update `--ai-help`.
- Measured with curl 8.21.0 (mingw, Schannel) via `Record-CurlExchange.ps1`, `-u AKID:SECRET`,
  CRLF line ends. The signatures depend on the date; recompute them with the date shown.

  `--aws-sigv4 aws:amz:us-east-1:s3 -H "X-Custom:  a   b " -H "Content-Type: text/plain" http://127.0.0.1:18629/bucket/key`
  ```
  GET /bucket/key HTTP/1.1
  Host: 127.0.0.1:18629
  Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=content-type;host;x-amz-content-sha256;x-amz-date;x-custom, Signature=a6ee51dc33bbbbe7a73f1d4e932381e7aada94fbbe3ff25a9a24134876814dec
  X-Amz-Date: 20260929T165340Z
  x-amz-content-sha256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
  User-Agent: curl/8.21.0
  Accept: */*
  X-Custom:  a   b 
  Content-Type: text/plain
  ```
  `-L --aws-sigv4 aws:amz:us-east-1:s3 http://127.0.0.1:18630/first`, answered
  `302 Location: /second?x=1` then `200`. curl re-signs the second hop for its own path and
  query:
  ```
  GET /first HTTP/1.1
  Host: 127.0.0.1:18630
  Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=d64892f15fbd9b7fe585d3df0ed3a26e84e620588f73c9268d6acce9a104f8a9
  X-Amz-Date: 20260929T165340Z
  x-amz-content-sha256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
  User-Agent: curl/8.21.0
  Accept: */*

  GET /second?x=1 HTTP/1.1
  Host: 127.0.0.1:18630
  Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=0921049d176f0b5d18a2ea8fc9dcb971867d5cc9fe7be0eaf89fb0f86a325ccb
  X-Amz-Date: 20260929T165340Z
  x-amz-content-sha256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
  User-Agent: curl/8.21.0
  Accept: */*
  ```

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Protocol.Abstractions.UnitLibrary and Curl.Protocol.Http.UnitLibrary (multi-line Authorization slot, pre-emptive auth failure), which BL-819 in Doing touches
- 2026-09-29: Backlog -> Doing.
