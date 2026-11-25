---
id: BL-5945
title: Write curl's Server auth using NTLM line for a redirected request sent without a header on an NTLM-authenticated connection
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.NtlmNegotiation.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AuthReports.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.NtlmAuthenticatedConnection.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5945 — Write curl's Server auth using NTLM line for a redirected request sent without a header on an NTLM-authenticated connection

## Goal

With `--ntlm -L -v`, a redirected request that reuses the authenticated connection writes the
same `auth using` line as curl 8.22.0.

## Context

Found by BL-5932. `OnNtlmAuthenticatedConnection` in `HttpProtocolHandler.NtlmNegotiation.cs` drops the Type 1
from such a request (BL-3003), and the handler then writes no `* Server auth using NTLM with
user '...'` line. libcurl's `output_auth_headers` writes it whenever NTLM is picked, header or
not (measured for the retry case in BL-5932). Not measured for a redirect: measure real curl
with `Record-CurlExchange.ps1` first, including a bare `NTLM` 401 answering that request
(`NTLM auth restarted`).

## Acceptance criteria

- [ ] For a measured `--ntlm -L -v` redirect on one connection, the port's `*` lines about authentication are curl 8.22.0's, in the same positions.
- [ ] A unit test in `Curl.Original.Protocol.Http.UnitTests` pins those lines.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
