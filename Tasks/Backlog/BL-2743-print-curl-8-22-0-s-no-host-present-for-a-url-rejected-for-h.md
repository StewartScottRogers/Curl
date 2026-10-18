---
id: BL-2743
title: Print curl 8.22.0's 'No host present' for a URL rejected for having no host
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2743 — Print curl 8.22.0's 'No host present' for a URL rejected for having no host

## Goal

A URL rejected for having no host prints `curl: (3) URL rejected: No host present`, curl 8.22.0's `curl_url_strerror(CURLUE_NO_HOST)` text, in place of 8.21.0's `No host part in the URL`.

## Context

Found by BL-2734. Real curl 8.22.0 (`curl.exe -sS "http://:80/"`, the Schannel mingw build, measured 2026-10-18) prints `curl: (3) URL rejected: No host present`; upstream `lib/strerror.c` at `curl-8_22_0` returns that for `CURLUE_NO_HOST`. Curl still prints 8.21.0's text from `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs` (`CurlUrlRejection.NoHost`) and `Curl.Core.UnitLibrary/ProxyUrlParser.cs`. Tests pinning the old text: `Curl.Protocol.Abstractions.UnitTests/CurlUrlRejectionTests.cs`, `Curl.Console.UnitTests/CurlCommandRunnerTests.cs`, `CurlCommandRunnerDisallowUsernameInUrlTests.cs` and `CurlCommandRunnerAdversarialCommandLineTests.cs`. Check that the proxy case (`-x http://:80`) prints the same text on 8.22.0 before changing `ProxyUrlParser`.

## Acceptance criteria

- [ ] `CurlUrlRejectionMessages.ToCurlMessage(CurlUrlRejection.NoHost)` is `No host present`, pinned in `CurlUrlRejectionTests`.
- [ ] `curl -sS http://:80/` through `Curl.Console` prints `curl: (3) URL rejected: No host present` and exits 3, pinned in `Curl.Console.UnitTests`.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-18: Created.
