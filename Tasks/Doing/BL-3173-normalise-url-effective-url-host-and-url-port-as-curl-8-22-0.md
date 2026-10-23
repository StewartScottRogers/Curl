---
id: BL-3173
title: Normalise %{url_effective}, url.host and url.port as curl 8.22.0 does and reject no-host URLs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/UrlEffective.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlIPv4Address.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs, Curl.Core.UnitLibrary/UrlSchemeGuesser.cs, Curl.Engine.UnitTests/UrlEffectiveNormalizationTests.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostPortTableTests.cs, Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3173 — Normalise %{url_effective}, url.host and url.port as curl 8.22.0 does and reject no-host URLs

## Goal

`%{url_effective}`, `%{url.host}` and `%{url.port}` print what curl 8.22.0 prints for odd URLs, and malformed forms fail with the same error text.

## Context

Stewart reported on 2026-10-22. Measured with `-w '%{url_effective}'` against a refused port 9, real curl 8.21.0 reports `http://127.1:9/echo` as `http://127.0.0.1:9/echo`; `http://0x7f.1:9/` and `http://2130706433:9/` as `http://127.0.0.1:9/`; `http://127.0.0.1.:9/` as `http://127.0.0.1:9/` (trailing dot dropped); port `09` as `9`; `http://h.invalid:/echo` as `http://h.invalid/echo`. Curl reports every input unchanged, because `Curl.Engine.UnitLibrary/UrlEffective.cs` keeps "the authority as typed". Stewart also reported: `//127.0.0.1:18080/echo` must fail with the URL-rejected no-host error (measured real 8.21 text `curl: (3) URL rejected: No host part in the URL`; Stewart wrote "No host present"; measure 8.22.0) and not Curl's `Unsupported number of slashes following scheme`; `http:127.0.0.1:18080/echo` and URLs with leading or trailing spaces report the URL unchanged, not with an inserted `http://`; zone IDs print as upstream does (`[::1%25lo]` shown as `::1` in the connect error). The URL model is `Curl.Protocol.Abstractions.UnitLibrary` (`CurlUrl.cs`, `CurlUrlParser.cs`, `CurlUrlHost.cs`, `CurlUrlIPv4Address.cs`, `CurlUrlRejectionMessages.cs`); the scheme guess for a scheme-less URL is `Curl.Core.UnitLibrary/UrlSchemeGuesser.cs`. Measure curl 8.22.0 with `Record-CurlExchange.ps1` for the 20 inputs before pinning. Note `Curl.Protocol.Abstractions.UnitLibrary` is a shared contract library: this task edits only its URL files, named in `touches`. The URL-rejection task for unknown schemes and bad ports shares `CurlUrlParser.cs` and `UrlSchemeGuesser.cs` with this one; the board serialises them.

## Acceptance criteria

- [ ] Notes record curl 8.22.0's `%{url_effective}`, `%{url.host}` and `%{url.port}` (and exit code and text for rejected ones) for the 20 inputs, including every example above.
- [ ] A table-driven test in `Curl.Engine.UnitTests/UrlEffectiveNormalizationTests.cs` runs the 20 inputs through `UrlEffective.Normalize` and compares with the recorded `%{url_effective}`; a second table in `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostPortTableTests.cs` compares `%{url.host}` and `%{url.port}` (as `CurlUrl` yields them).
- [ ] `//127.0.0.1:18080/echo` returns exit 3 with 8.22.0's measured no-host text, in a test through the command runner (`Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionTests.cs`).
- [ ] `http:127.0.0.1:18080/echo` and URLs with leading/trailing spaces report `%{url_effective}` unchanged (or as 8.22.0 measures, noted in the test); zone-ID connect-error text is pinned.
- [ ] `dotnet build Curl.Engine.UnitLibrary Curl.Protocol.Abstractions.UnitLibrary Curl.Core.UnitLibrary -warnaserror` is clean and the fast tests of the matching `.UnitTests` projects pass; the existing `UrlEffective` and `CurlUrl` tests still pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 3debb382 9eefc518 e6155fdf dd37067d 92579dca.
