---
id: BL-2915
title: Port upstream unit program 1675: urlapi ipv4_normalize, urlencode_str, ipv6_parse, parse_file, same origin and parse_hostname_login
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1675UrlApi.cs, Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1675UrlApiTests.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.UrlParsing.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary/Curl.Engine.UnitLibrary.csproj, Curl.Engine.UnitLibrary/UrlPartSetter.cs, Curl.Engine.UnitLibrary/UrlPartText.cs, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitLibrary/Curl.Core.UnitLibrary.csproj, Curl.Protocol.Abstractions.UnitLibrary/Curl.Protocol.Abstractions.UnitLibrary.csproj]
requirement: none
created: 2026-10-21
completed:
---
# BL-2915 — Port upstream unit program 1675: urlapi ipv4_normalize, urlencode_str, ipv6_parse, parse_file, same origin and parse_hostname_login

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

- [ ] `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1675UrlApi.cs` checks every row of unit1675.c's six sections (named in its doc comment), is registered in `UnitPrograms.UrlParsing.cs`, and has tests in `Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1675UrlApiTests.cs`.
- [ ] With `CURL_UPSTREAM_CASE=1675`, `dotnet test Curl.Conformance.UnitTests --filter Name~NamedUpstreamCase` ends `Passed` on Windows, and 1675 is on `PassingUpstreamCases.txt`.
- [ ] Each fix to Curl has a focused unit test in the fixed library's `.UnitTests` project.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
