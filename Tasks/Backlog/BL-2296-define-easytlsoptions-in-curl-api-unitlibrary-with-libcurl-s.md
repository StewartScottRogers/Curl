---
id: BL-2296
title: Define EasyTlsOptions in Curl.Api.UnitLibrary with libcurl's defaults
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Options/Tls, Curl.Api.UnitTests/Options/Tls]
requirement: none
created: 2026-10-15
completed:
---
# BL-2296 — Define EasyTlsOptions in Curl.Api.UnitLibrary with libcurl's defaults

## Goal

`Curl.Api.UnitLibrary/Options/Tls` (namespace `Curl.Api.Options.Tls`) defines `EasyTlsOptions`, an immutable record carrying every option of the plan's `EasyTlsOptions` row - the origin's TLS options, their `PROXY_*` twins (one nested record used twice is fine) and the `DOH_SSL_*` options - each defaulting to libcurl 8.22.0's default.

## Context

ADR-0506; plan section "CURLOPT groups". Follow the rules for option-group tasks written in BL-2294's Context. Blobs (`*_BLOB`) are `ReadOnlyMemory<byte>?`; `SSLVERSION` and `SSL_OPTIONS` become enums (`CURL_SSLVERSION_*` with the `MAX` part, `CURLSSLOPT_*` flags). Defaults are libcurl's, not the Schannel build's behaviour; where the Schannel build ignores an option, say so in the doc comment (ADR-0009). BL-2304 retargets the engine onto this record.

## Acceptance criteria

- [ ] `EasyTlsOptions` exists in `Curl.Api.UnitLibrary/Options/Tls` and carries every option in the plan's row, each documented with its `CURLOPT_*` name and default.
- [ ] Tests in `Curl.Api.UnitTests/Options/Tls` pin every default and that a `with` copy changes only the property set.
- [ ] This task's Notes list each `CURLOPT_*` of the plan's row with the property that carries it (none left without one).
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
