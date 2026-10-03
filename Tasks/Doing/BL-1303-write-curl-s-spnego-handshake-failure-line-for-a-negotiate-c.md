---
id: BL-1303
title: Write curl's SPNEGO handshake failure line for a Negotiate challenge that starts with =
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1303 — Write curl's SPNEGO handshake failure line for a Negotiate challenge that starts with =

## Goal

When a 401 answers `--negotiate` with a `Negotiate` challenge whose token starts with `=` (such as `WWW-Authenticate: Negotiate =`), Curl writes curl 8.21.0's `SPNEGO handshake failure (empty challenge message)` info line, on both the Windows (SSPI) and the GSS-API build, and nothing else for that challenge, instead of stepping the security context again and writing `InitializeSecurityContext failed: ...`.

## Context

- curl 8.21.0 (tag `curl-8_21_0`), `Curl_auth_decode_spnego_message`:
  - `lib/vauth/spnego_sspi.c` lines 173-185 (Windows): when the challenge text is not empty and starts with `=`, it is not base64-decoded, `chlg` stays empty, and `infof(data, "SPNEGO handshake failure (empty challenge message)")` returns `CURLE_BAD_CONTENT_ENCODING` before any `InitializeSecurityContext` call;
  - `lib/vauth/spnego_gssapi.c` lines 132-143: the same test and the same line before any `gss_init_sec_context`.
  The error ends Negotiate for the transfer; the 401 is the result (exit 0 without `-f`).
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response 'HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Negotiate =\r\nContent-Length: 0\r\n\r\n'` and `-sv -o NUL --negotiate -u : http://127.0.0.1:PORT/`: exit 0; after the request, stderr is
  ```
  < HTTP/1.1 401 Unauthorized
  * SPNEGO handshake failure (empty challenge message)
  < WWW-Authenticate: Negotiate =
  < Content-Length: 0
  < 
  * Connection #0 to host 127.0.0.1:PORT left intact
  ```
  Curl writes `* InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package` in the SPNEGO line's place; every other line matches (both write the same `InitializeSecurityContext failed` line for the first request, before `Server auth using Negotiate with user ''`).
- Curl today: `Curl.Authentication.UnitLibrary/NegotiateHttpAuthenticator.cs` takes the challenge token with `HttpChallengeSchemes.NegotiateTokenOf` and `DecodeBase64`, and reports a failed step through `NegotiateFailureLines.For` (line 232) to `HttpAuthRequest.Events`. Find which path answers this 401 (the measured run had no context awaiting a leg, since the first leg failed) and put the `=` test ahead of any context step on it.
- `wordsFailuresAsSspi` picks the Windows or the GSS-API wording of other failures; this line is the same on both, so the tests can run on every platform.

## Acceptance criteria

- [ ] A test in `Curl.Authentication.UnitTests` replays the measured exchange (first leg failing for want of credentials, then a 401 with `Negotiate =`) through a fake `ISecurityContextFactory` and asserts the info line `SPNEGO handshake failure (empty challenge message)` reported once for the challenge, no context step made for it, and no Authorization header for a further request.
- [ ] A test where the first leg succeeded and the server answers `Negotiate =abc` asserts the same line and that the awaiting context is disposed without a step.
- [ ] Tests run with `wordsFailuresAsSspi` both `true` and `false` and assert the same line.
- [ ] A test pins that an empty `Negotiate` challenge (no token) and an undecodable token not starting with `=` keep today's behaviour.
- [ ] `dotnet build Curl.Authentication.UnitTests -warnaserror` is clean; `dotnet test Curl.Authentication.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
