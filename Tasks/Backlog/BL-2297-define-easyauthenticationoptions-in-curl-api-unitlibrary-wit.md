---
id: BL-2297
title: Define EasyAuthenticationOptions in Curl.Api.UnitLibrary with libcurl's defaults
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Options/Authentication, Curl.Api.UnitTests/Options/Authentication]
requirement: none
created: 2026-10-15
completed:
---
# BL-2297 — Define EasyAuthenticationOptions in Curl.Api.UnitLibrary with libcurl's defaults

## Goal

`Curl.Api.UnitLibrary/Options/Authentication` (namespace `Curl.Api.Options.Authentication`) defines `EasyAuthenticationOptions`, an immutable record carrying every option of the plan's `EasyAuthenticationOptions` row (netrc, user and password, login options, HTTP and proxy auth bitmasks, SASL, OAuth bearer, service names, GSS-API delegation, Kerberos level, AWS SigV4, TLS-SRP), each defaulting to libcurl 8.22.0's default.

## Context

ADR-0506; plan section "CURLOPT groups". Follow the rules for option-group tasks written in BL-2294's Context. `HTTPAUTH`/`PROXYAUTH` become a `[Flags]` enum with `CURLAUTH_*`'s bit values (including `ANY`, `ANYSAFE`, `ONLY`); `NETRC` an enum of `CURL_NETRC_*`. Secrets are plain `string?` here; no logging of them anywhere (the security auditor checks secrets in output). BL-2306 retargets the engine onto this record.

## Acceptance criteria

- [ ] `EasyAuthenticationOptions` exists in `Curl.Api.UnitLibrary/Options/Authentication` and carries every option in the plan's row, each documented with its `CURLOPT_*` name and default.
- [ ] Tests in `Curl.Api.UnitTests/Options/Authentication` pin every default, the `CURLAUTH_*` bit values, and that a `with` copy changes only the property set.
- [ ] The record's `ToString()` does not contain a password, bearer token or TLS-SRP password set on it (a test sets each and asserts its absence).
- [ ] This task's Notes list each `CURLOPT_*` of the plan's row with the property that carries it (none left without one).
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
