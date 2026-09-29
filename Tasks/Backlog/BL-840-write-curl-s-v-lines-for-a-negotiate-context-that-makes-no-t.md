---
id: BL-840
title: Write curl's -v lines for a Negotiate context that makes no token
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-527]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-840 — Write curl's -v lines for a Negotiate context that makes no token

## Goal

Under `-v`, `--negotiate` writes the lines curl 8.21.0 writes: the platform's context-failure line before the first request and again after the 401, and `Server auth using Negotiate with user '<user>'` before the request.

## Context

- ADR-0176 records the measured lines (BL-527 Notes). Windows (SSPI): `* InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`. Linux (MIT GSS-API, curl 8.18.0): `* gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. ` (trailing space). Both then `* Server auth using Negotiate with user ''`.
- The failure line comes from the `ISecurityContext` step's status; the seam (`SecurityContextStep`) carries no text yet, so the task decides where the text is made. `Server auth using Basic with user '...'` is not written for any scheme yet either; check curl for Basic and Digest and write them in the same change if the pattern is shared.
- Events go through `ITransferEvents.ReportInfo`.

## Acceptance criteria

- [ ] A `Curl.Protocol.Http.UnitTests` or `Curl.Authentication.UnitTests` test pins each platform's lines, in curl's order, for `--negotiate -u : -v` against a `401 Negotiate` with no ticket, in its own `OSCondition` test per platform.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
