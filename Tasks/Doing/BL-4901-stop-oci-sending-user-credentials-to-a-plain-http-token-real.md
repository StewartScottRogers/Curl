---
id: BL-4901
title: Stop oci:// sending --user credentials to a plain http:// token realm
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary/Auth/OciTokenRequestBuilder.cs, Curl.Extended.Protocol.OciRegistry.UnitTests/OciTokenRealmSchemeTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4901 — Stop oci:// sending --user credentials to a plain http:// token realm

## Goal

When the registry is HTTPS, credentials are never sent to a non-HTTPS token realm: the transfer fails naming the realm.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-006 (Medium (release blocker), confirmed: reproduced at least twice).

- Repro: TLS OCI mock on 18704 that, for repositories named `httprealm`, answers 401 with `WWW-Authenticate: Bearer realm="http://127.0.0.1:18701/token",service="reg",scope="repository:httprealm:pull"`; a plain HTTP logger on 18701. `curl.exe -sS -k -u user:pass oci://127.0.0.1:18704/httprealm:1.0`
- Expected: No credentials to a non-HTTPS realm when the registry is HTTPS (the downgrade curl guards against for redirects, CVE-2022-27774): refuse with a clear error.
- Actual: The plain-HTTP logger receives `GET /token?service=reg&scope=...` with `Authorization: Basic dXNlcjpwYXNz`.
- Where to start: `OciTokenRequestBuilder.cs` accepts any absolute http/https realm.

## Acceptance criteria

- [ ] No Authorization header reaches the 18701 logger and the command exits non-zero with a message naming the non-HTTPS realm.
- [ ] An `https://` realm still works, and a unit test covers both.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: aab36c7c 80b0c93c a0b49a29.
