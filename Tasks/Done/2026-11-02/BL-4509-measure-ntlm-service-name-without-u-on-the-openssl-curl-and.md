---
id: BL-4509
title: Measure --ntlm --service-name without -u on the OpenSSL curl and pin each platform's answer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Authentication.cs, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4509 — Measure --ntlm --service-name without -u on the OpenSSL curl and pin each platform's answer

## Goal

`--ntlm --service-name <name>` with no `-u` sends what the platform's curl sends: measured on the Schannel build (Windows) and the OpenSSL build (Linux and macOS), each answer pinned in its own test.

## Context

Found by the shift-end review (ADR-0710) of the shift merged at e3af7deb5, by the type-design-analyzer and silent-failure-hunter reviewers. BL-4460 (ADR-1183) made `HttpProtocolHandler.StartsWithAnEmptyUser` (`Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`, about lines 380-390) answer the origin as an empty NTLM user when `--service-name` is set and NTLM is the one scheme, so a Type 1 message goes on the first request. ADR-1183 measured only curl 8.22.0 (Schannel), and the `HttpRequestOptions.ServiceName` doc comment explains it as "the default Windows credentials" (SSPI). The rule has no platform condition and its new tests in `HttpProtocolHandlerTests.Authentication.cs` carry no `OSCondition`, so on Linux and macOS Curl now sends an NTLM exchange the OpenSSL curl may not send. CLAUDE.md: match the platform's curl, measure real curl before pinning, pin each platform's answer in its own test.

## Acceptance criteria

- [x] The OpenSSL curl 8.22.0's request for `curl --ntlm --service-name x http://127.0.0.1:<port>/` (and with `-d x`) is recorded (`Record-CurlExchange.ps1` on Linux or macOS, or a CI job), and the result is written into ADR-1183.
- [x] If the OpenSSL curl sends no `Authorization` there, the empty-user NTLM rule applies on Windows only, and a test marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` pins that no `Authorization` header is sent off Windows.
- [x] The Windows answer stays pinned in a test marked `[OSCondition(OperatingSystems.Windows)]`, or stays unconditioned if both builds agree as recorded in ADR-1183.
- [x] The `HttpRequestOptions.ServiceName` doc comment says which platforms the rule applies on.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added `Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs` to touches: criterion 4 rewrites its `ServiceName` doc comment; no task in Doing on origin/work/dark-factory names it.
- Measured the OpenSSL build with curl 8.18.0 (Ubuntu in WSL, OpenSSL/3.5.5; no 8.22.0 OpenSSL build available) against an `nc` loopback listener: `--ntlm --service-name x` sends a plain GET and `-d x` the whole POST, neither with `Authorization`. Recorded in ADR-1183 (amendment). Record-CurlExchange.ps1 was not used because WSL has no pwsh; nc captured the bytes.
- Decision: `StartsWithAnEmptyUser(options, onWindows)` applies the service-name NTLM rule only on Windows (`OperatingSystem.IsWindows()`); the Digest/authzid rule stays on every platform. The method takes the platform as a parameter so both branches are covered on any CI platform.
- Tests: Windows answer `[OSCondition(OperatingSystems.Windows)]`, off-Windows no-Authorization `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`, plus an unconditioned DataRow test of `StartsWithAnEmptyUser`. Criterion about Linux/macOS met by keeping the change platform-neutral; CI checks the off-Windows test.
- Full build clean, all fast tests green (114 test projects), Measure-CodeQuality for Curl.Original.Protocol.Http.UnitLibrary: 0 failing members. Abstractions got only a doc-comment change (no IL), so it was not measured separately.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: e4cb6f5a b990f68d 0599ace1 ec513870 3d836d5e.
- 2026-11-02: Doing -> Done. --ntlm --service-name without -u sends NTLM Type 1 on Windows only; off Windows no Authorization, as the OpenSSL curl
