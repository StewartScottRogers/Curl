---
id: BL-4509
title: Measure --ntlm --service-name without -u on the OpenSSL curl and pin each platform's answer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Authentication.cs]
requirement: none
created: 2026-11-02
completed:
---
# BL-4509 — Measure --ntlm --service-name without -u on the OpenSSL curl and pin each platform's answer

## Goal

`--ntlm --service-name <name>` with no `-u` sends what the platform's curl sends: measured on the Schannel build (Windows) and the OpenSSL build (Linux and macOS), each answer pinned in its own test.

## Context

Found by the shift-end review (ADR-0710) of the shift merged at e3af7deb5, by the type-design-analyzer and silent-failure-hunter reviewers. BL-4460 (ADR-1183) made `HttpProtocolHandler.StartsWithAnEmptyUser` (`Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`, about lines 380-390) answer the origin as an empty NTLM user when `--service-name` is set and NTLM is the one scheme, so a Type 1 message goes on the first request. ADR-1183 measured only curl 8.22.0 (Schannel), and the `HttpRequestOptions.ServiceName` doc comment explains it as "the default Windows credentials" (SSPI). The rule has no platform condition and its new tests in `HttpProtocolHandlerTests.Authentication.cs` carry no `OSCondition`, so on Linux and macOS Curl now sends an NTLM exchange the OpenSSL curl may not send. CLAUDE.md: match the platform's curl, measure real curl before pinning, pin each platform's answer in its own test.

## Acceptance criteria

- [ ] The OpenSSL curl 8.22.0's request for `curl --ntlm --service-name x http://127.0.0.1:<port>/` (and with `-d x`) is recorded (`Record-CurlExchange.ps1` on Linux or macOS, or a CI job), and the result is written into ADR-1183.
- [ ] If the OpenSSL curl sends no `Authorization` there, the empty-user NTLM rule applies on Windows only, and a test marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` pins that no `Authorization` header is sent off Windows.
- [ ] The Windows answer stays pinned in a test marked `[OSCondition(OperatingSystems.Windows)]`, or stays unconditioned if both builds agree as recorded in ADR-1183.
- [ ] The `HttpRequestOptions.ServiceName` doc comment says which platforms the rule applies on.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-02: Created.
