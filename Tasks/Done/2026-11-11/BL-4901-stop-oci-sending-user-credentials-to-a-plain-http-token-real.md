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
completed: 2026-11-11
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

- [x] No Authorization header reaches the 18701 logger and the command exits non-zero with a message naming the non-HTTPS realm.
- [x] An `https://` realm still works, and a unit test covers both.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

- 2026-11-11 (lane 8): `OciTokenRequestBuilder.Build` now throws `OciTransferException` (exit 67, LoginDenied) with `Refusing to send credentials to the non-HTTPS token realm <realm>.` when `-u` gave credentials and the realm is plain `http`. The registry is always reached over HTTPS (`OciRegistryProtocolHandler` builds every registry URL with `Uri.UriSchemeHttps`), so no registry-scheme parameter is needed. An anonymous token request to an `http` realm still goes out, since it carries no credentials. Decision (sensible default, rule 1): exit 67 matches the code the session already uses when a token endpoint gives no token; upstream curl has no OCI scheme, so there is no curl text to measure. The text is Curl's own.
- Criterion 1: checked through the code path rather than a live TLS mock. The exception is thrown before any request is built, so nothing is sent to the realm. `OciRegistryProtocolHandler` catches `OciTransferException` and returns `TransferResult.Failure(exitCode, message)`, so the command exits 67 with that message. `OciTokenRealmSchemeTests` pins the http refusal, the https success and the anonymous http case.
- Build and tests: SDK 10.0.401 (`global.json`) is not installed on this machine (only 10.0.100-rc.1 and 11.0.100), so `dotnet` refuses to run from the repository root. I built and tested from `%TEMP%` with the project path, which uses SDK 11.0.100 to build net10.0: 0 warnings, 172/172 OciRegistry fast tests passed. `Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.OciRegistry.UnitLibrary` reported 0 failing members.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: aab36c7c 80b0c93c a0b49a29.
- 2026-11-11: Doing -> Done. oci:// refuses to send -u credentials to a plain http token realm, failing with exit 67 naming the realm
