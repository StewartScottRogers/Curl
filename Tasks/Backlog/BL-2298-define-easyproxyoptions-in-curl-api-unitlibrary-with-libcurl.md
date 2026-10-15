---
id: BL-2298
title: Define EasyProxyOptions in Curl.Api.UnitLibrary with libcurl's defaults
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Options/Proxy, Curl.Api.UnitTests/Options/Proxy]
requirement: none
created: 2026-10-15
completed:
---
# BL-2298 — Define EasyProxyOptions in Curl.Api.UnitLibrary with libcurl's defaults

## Goal

`Curl.Api.UnitLibrary/Options/Proxy` (namespace `Curl.Api.Options.Proxy`) defines `EasyProxyOptions`, an immutable record carrying every option of the plan's `EasyProxyOptions` row (proxy, port, type, pre-proxy, no-proxy, tunnelling, CONNECT headers, SOCKS5 auth and GSS-API, HAProxy protocol), each defaulting to libcurl 8.22.0's default.

## Context

ADR-0506; plan section "CURLOPT groups". Follow the rules for option-group tasks written in BL-2294's Context. `PROXYTYPE` becomes an enum of `CURLPROXY_*`; `SOCKS5_AUTH` a `[Flags]` enum of the `CURLAUTH_*` bits it accepts; `HEADEROPT` an enum of `CURLHEADER_*`. Proxy credentials belong to `EasyAuthenticationOptions` (BL-2297) and proxy TLS to `EasyTlsOptions` (BL-2296); do not duplicate them. BL-2305 retargets the engine onto this record.

## Acceptance criteria

- [ ] `EasyProxyOptions` exists in `Curl.Api.UnitLibrary/Options/Proxy` and carries every option in the plan's row, each documented with its `CURLOPT_*` name and default.
- [ ] Tests in `Curl.Api.UnitTests/Options/Proxy` pin every default and enum value, and that a `with` copy changes only the property set.
- [ ] This task's Notes list each `CURLOPT_*` of the plan's row with the property that carries it (none left without one).
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
