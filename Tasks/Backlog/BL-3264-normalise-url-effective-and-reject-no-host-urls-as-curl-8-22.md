---
id: BL-3264
title: Normalise %{url_effective} and reject no-host URLs as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3182]
touches: [Curl.Engine.UnitLibrary/UrlEffective.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlIPv4Address.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs, Curl.Core.UnitLibrary/UrlSchemeGuesser.cs, Curl.Engine.UnitTests/UrlEffectiveNormalizationTests.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostPortTableTests.cs, Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionTests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Core.UnitTests/UrlSchemeGuesserTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3264 — Normalise %{url_effective} and reject no-host URLs as curl 8.22.0 does

## Goal

`%{url_effective}`, `%{url.host}` and `%{url.port}` print what curl 8.22.0 prints for odd URLs, and a URL with no host fails with curl's `URL rejected: No host present`.

## Context

Split from BL-3173 (over its cost cap). The URL model is `Curl.Protocol.Abstractions.UnitLibrary` (`CurlUrlParser.cs`, `CurlUrlHost.cs`, `CurlUrlIPv4Address.cs`, `CurlUrlRejectionMessages.cs`); the scheme guess is `Curl.Core.UnitLibrary/UrlSchemeGuesser.cs`; `Curl.Engine.UnitLibrary/UrlEffective.cs` builds `%{url_effective}`. The shared contract library is edited only in its URL files named in `touches`. The 20 measured inputs are in the Notes of BL-3173 (Deferred); copy that list into this task's Notes first.

## Acceptance criteria

- [ ] Notes record curl 8.22.0's `%{url_effective}`, `%{url.host}`, `%{url.port}` and, for rejected ones, exit code and text for the 20 inputs of BL-3173's Notes.
- [ ] A table-driven test in `Curl.Engine.UnitTests/UrlEffectiveNormalizationTests.cs` runs the 20 inputs through `UrlEffective.Normalize` and compares with the recorded `%{url_effective}`; a second table in `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostPortTableTests.cs` compares `%{url.host}` and `%{url.port}`.
- [ ] `//127.0.0.1:18080/echo` returns exit 3 with `curl: (3) URL rejected: No host present`, in a test through the command runner (`Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionTests.cs`).
- [ ] `http:127.0.0.1:18080/echo` and URLs with leading or trailing spaces report `%{url_effective}` as typed (their rejection text pinned as measured), and `Curl.Core.UnitTests/UrlSchemeGuesserTests.cs` has a row showing `/x` stays unchanged.
- [ ] `dotnet build Curl.Engine.UnitLibrary Curl.Protocol.Abstractions.UnitLibrary Curl.Core.UnitLibrary -warnaserror` is clean, the fast tests of Curl.Engine, Curl.Protocol.Abstractions, Curl.Core and Curl.Console pass, and `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Core.UnitLibrary,Curl.Console` is within the gates.

## Notes

- Split from BL-3173.
- Continues the stashed work of BL-3173.

## Log

- 2026-10-23: Created.
