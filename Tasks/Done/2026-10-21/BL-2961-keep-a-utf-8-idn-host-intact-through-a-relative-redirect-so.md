---
id: BL-2961
title: Keep a UTF-8 IDN host intact through a relative redirect so upstream cases 2046 and 2047 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs, Curl.Protocol.Http.UnitTests/HttpRedirectLocationTests.cs, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/RedirectFollowerTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2961 — Keep a UTF-8 IDN host intact through a relative redirect so upstream cases 2046 and 2047 pass

## Goal

After `-L` follows a relative `Location` (`./20460001`) from a URL whose host is a UTF-8 IDN name (`http://åäö.se:8990/2046`), the next request goes to the same host, sent as `xn--4cab6c.se`, so upstream cases 2046 and 2047 pass.

## Context

Found by BL-2861 (2026-10-21), which made the `Host:` line and a proxy's absolute-form target carry the punycode host (`HttpUrlText.WireHostAndPort`), so 165 and 1448 pass. In 2046 and 2047 the first request is right, but the redirected one is not: in 2047 it goes out as `GET http://???.se/20470001` with `Host: ???.se`, and in 2046 no second request reaches the server (the `???` host does not match `--resolve xn--4cab6c.se:...`). `HttpRedirectLocation.Resolve` builds the target as `HttpUrlText.Origin(requestUrl) + path`, where `Origin` holds the host as typed (`åäö`); somewhere between there and the next transfer's `CurlUrl` the non-ASCII host is encoded through ASCII (each character becoming `?`). Find that step (start with the follow path that re-parses the resolved target, in Curl.Core or Curl.Engine), keep the host's UTF-8, and add its file to `touches`. curl 8.22.0 keeps `%{url_effective}` as `http://åäö.se:8990/20460001` (the case's `<stdout>`), so the fix must not punycode the effective URL. Cases at tag `curl-8_22_0`: https://github.com/curl/curl/tree/curl-8_22_0/tests/data

## Acceptance criteria

- [x] A focused unit test in the fixed library's `.UnitTests` project fails without the fix: a relative redirect from a UTF-8 IDN host keeps that host.
- [x] Upstream cases 2046 and 2047 pass and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- The `?` came from `Curl.Core.UnitLibrary/RedirectFollower.cs`'s `EncodedForFollow`, not from `HttpRedirectLocation`: it percent-escapes every Latin-1 character as one header byte, so the relative target's origin host `åäö` (text from the request's `CurlUrl`) became `%E5%E4%F6`, which `CurlUrlHost` decodes to bytes that are not UTF-8. Added `RedirectFollower.cs` and `RedirectFollowerTests.cs` to `touches` (no task in Doing on `origin/work/dark-factory` named them); `HttpRedirectLocation.cs` needed no change.
- Decision (sensible default): an authority holding a character above U+00FF, or Latin-1 characters whose bytes are not valid UTF-8, is text and kept as it is; any other authority is still escaped byte by byte, so an absolute `Location` with raw UTF-8 host bytes (`Ã¥` read as Latin-1) still follows as `%C3%A5`. `%{url_effective}` was already the unescaped target and stays `http://åäö.se:8990/20460001`.
- Measured: cases 2046 and 2047 pass through the Manual runner; Curl.Core.UnitLibrary 100% line and branch, worst CRAP 10 (first run caught `EncodedForFollow` at complexity 12; split into `IsWithin`).

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 3ad1c32b 1f5f797a dd1cc079.
- 2026-10-21: Doing -> Done. A relative redirect from a UTF-8 IDN host follows to that host (sent as punycode); upstream cases 2046 and 2047 pass
