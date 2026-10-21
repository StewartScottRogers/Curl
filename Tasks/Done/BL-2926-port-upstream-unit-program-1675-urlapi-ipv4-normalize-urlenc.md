---
id: BL-2926
title: Port upstream unit program 1675: urlapi ipv4_normalize, urlencode_str, ipv6_parse, parse_file, same origin and parse_hostname_login
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1675UrlApi.cs, Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1675UrlApiTests.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.UrlParsing.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary/Curl.Engine.UnitLibrary.csproj, Curl.Engine.UnitLibrary/UrlPartSetter.cs, Curl.Engine.UnitLibrary/UrlPartText.cs, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitLibrary/Curl.Core.UnitLibrary.csproj, Curl.Protocol.Abstractions.UnitLibrary/Curl.Protocol.Abstractions.UnitLibrary.csproj, Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1675Functions.cs, Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1675Login.cs, Curl.Engine.UnitLibrary/UrlEncodeQuery.cs, Curl.Engine.UnitTests/UrlPartTextTests.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2926 — Port upstream unit program 1675: urlapi ipv4_normalize, urlencode_str, ipv6_parse, parse_file, same origin and parse_hostname_login

## Goal

Upstream case 1675 (URL API unit tests) runs as a ported unit program, `Unit1675UrlApi`, and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2891 (2026-10-21), which ported 1620, 1627 and 1653 and waits on this task for 1675. Follow ADR-0711 and the shape of `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1653PortParsing.cs`: one `IUnitProgram` registered in `UnitPrograms.UrlParsing.cs`, every row of `https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1675.c` checked in order, each section's failures written as the C program writes them and ending with its `abort_if`, and an injectable function per section so a test covers the failure branches.

Map each section to Curl's counterpart:
- `ipv4_normalize`: `CurlUrlIPv4Address.TryNormalize` (Curl.Protocol.Abstractions; add `<InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary" />` to its csproj - BL-2891's stashed work adds the same line, so its re-apply keeps one).
- `ipv6_parse`: `CurlUrlHost.TryNormalize` on the bracketed host (host `[fe80::1]`, zone id `eth0`).
- `parse_file`: `CurlUrl` parsing of `file:` URLs; the two `_WIN32` rows go in an `OperatingSystem.IsWindows()` branch and the test pins each platform.
- `parse_hostname_login`: `CurlUrlAuthority.Parse` (user, password, options; offset = index after `@`). Note the C struct starts with user `u`, password `p`, options `o`: a scheme without login options keeps `o`, and no login clears all three.
- `urlencode_str`: the encoder in `Curl.Engine.UnitLibrary/UrlPartSetter.cs` (`Encoded`) / `UrlPartText.cs`; Engine needs an `InternalsVisibleTo` for `Curl.Conformance.UnitProgram.UnitLibrary`, or expose the encoder.
- `Curl_url_same_origin`: `RedirectFollower.IsSameOrigin` in Curl.Core is private; make it reachable (Core already lets this library in? check its csproj) and run the `USE_SSL` rows too.

Where Curl's answer differs from the C table, fix Curl in its own library with a focused unit test there (ADR-0482).

## Acceptance criteria

- [x] `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1675UrlApi.cs` checks every row of unit1675.c's six sections (named in its doc comment), is registered in `UnitPrograms.UrlParsing.cs`, and has tests in `Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1675UrlApiTests.cs`.
- [x] With `CURL_UPSTREAM_CASE=1675`, `dotnet test Curl.Conformance.UnitTests --filter Name~NamedUpstreamCase` ends `Passed` on Windows, and 1675 is on `PassingUpstreamCases.txt`.
- [x] Each fix to Curl has a focused unit test in the fixed library's `.UnitTests` project.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- `Unit1675UrlApi` checks all six sections in unit1675.c's order. Each section writes the C program's failure lines. If any row fails, it writes the section's abort_if line (`unit1675.c Abort assertion '<msg>' met`, with no line number since the port has no `__LINE__`) and ends with exit 1, as abort_if's goto does. The Curl functions are injected through `Unit1675Functions` (`Unit1675Functions.Curl` is Curl's own), and the login results are a `Unit1675Login` record. Both are new files beside the port, added to `touches`.
- urlencode_str maps to the new `UrlPartText.UrlEncodeString(text, relative, UrlEncodeQuery)`, with the enum in a new `UrlEncodeQuery.cs` (C's QUERY_NO 0, QUERY_NOT_YET 1, QUERY_YES 2). Curl's old `Encode` differed from curl 8.22.0 in three ways:
  - it never upper-cased a `%xx` escape;
  - it had no QUERY_NO, so a `?` always turned later spaces into `+`;
  - it had no find_host_sep, so it did not keep a non-relative URL's text before the host separator.

  `Encode(text, query)` now calls the new method with Yes or NotYet. Its callers (UrlHandleParts, UrlRelativeReference) keep their space handling and now also upper-case escapes, as upstream does. Focused tests are in `Curl.Engine.UnitTests/UrlPartTextTests.cs`. The method is named `UrlEncodeString` rather than overloading `Encode`, because a second `Encode` makes the cref in UrlHandleParts.cs ambiguous (CS0419), and that file is not in `touches`.
- Gap left for later: under CURLU_URLENCODE, upstream encodes a fragment with QUERY_NO, but `UrlHandleParts.UrlEncoded` still passes `query: false` (NotYet). A `?` inside a fragment therefore still turns the spaces after it into `+`. The fix belongs in UrlHandleParts.cs (filed as BL-2952).
- ipv6_parse: `CurlUrlHost.TryNormalize` accepted `[::1` (no closing bracket) as `[::]`. The new `SplitBracketedAddress` refuses it with BadIPv6, and `[` alone no longer throws. Focused tests are in `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostTests.cs`. Both files were added to `touches`; no Doing task on origin/work/dark-factory names them.
- same_origin: `RedirectFollower.IsSameOrigin` is now internal (Core's InternalsVisibleTo already names this library). An href with no host is a relative reference, so it takes the base's scheme and authority. An href with a host and no scheme takes the base's scheme. Every href is built as an absolute URL. All nine rows run, the USE_SSL rows included, and all pass.
- parse_hostname_login maps to `CurlUrlAuthority.Parse`. The C struct starts with u/p/o. A login replaces the user and password, and keeps "o" as the options unless the scheme has login options (CurlUrlScheme.HasLoginOptions). No login, or a refusal, clears all three. The offset is the index after `@`.
- parse_file maps to `CurlUrl.TryParse(...).AbsolutePath`. The two _WIN32 rows run only when `IsWindows` is true, and both pass on Windows. The tests inject `IsWindows` to pin each branch.
- Measured with `Measure-CodeQuality.ps1` for Engine, Abstractions, Core and UnitProgram: 100% line and branch coverage, worst CRAP 10, no failing member. This is after splitting the four methods the first run flagged as over complexity 10.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 1b131cfd d384e230 ecb0e5d8 dd1cc079.
- 2026-10-21: Doing -> Done. Upstream unit program 1675 is ported, passes on Windows and is on the ratchet; urlencode_str now upper-cases escapes and honours QUERY_NO, and ipv6_parse refuses a host with no closing bracket
