---
id: BL-847
title: Fail an NTLM Type 3 past curl's 1024-byte buffer with exit 100 off Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-526]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-847 — Fail an NTLM Type 3 past curl's 1024-byte buffer with exit 100 off Windows

## Goal

On Linux and macOS, an `--ntlm` transfer whose Type 3 answer would not fit curl's
1024-byte `NTLM_BUFSIZE` fails with the exit code and error message the platform's curl
gives (expected `CURLE_TOO_LARGE`, exit 100), instead of ending on the 401 with exit 0.

## Context

curl's own NTLM (`lib/vauth/ntlm.c`, `Curl_auth_create_ntlm_type3_message`, used by the
OpenSSL builds on Linux and macOS) builds Type 3 in a buffer of `NTLM_BUFSIZE` (1024)
bytes and fails with `CURLE_TOO_LARGE` (exit 100 per
https://curl.se/libcurl/c/libcurl-errors.html, curl 8.21.0) when it does not fit.

BL-526 (ADR-0180,
`Documentation/Planning/Decisions/ADR-0180-http-ntlm-answers-in-three-legs-from-a-fresh-context-per-leg.md`)
chose a default instead: `Curl.Authentication.UnitLibrary/HandBuiltNtlmSecurityContext.cs`
answers `SecurityContextStatus.Refused` when the answer passes 1024 bytes, and
`NtlmHttpAuthenticator` (constructed with `refusedChallengeFailsTransfer: false` off
Windows by `Curl.Console/CurlComposition.cs`) then sends nothing, so the transfer ends on
the 401 with exit 0. The Windows route answers through SSPI and has no such limit, so
this task changes only the hand-built route.

The failure reaches the HTTP handler as `HttpAuthenticationFailedException`
(`Curl.Protocol.Abstractions.UnitLibrary/HttpAuthenticationFailedException.cs`), which
already carries an exit code (BL-526 uses it for exit 94); use it with the measured code
and message.

Measure first: `Record-CurlExchange.ps1 -Curl wsl.exe` with the WSL setup in
`Documentation/Planning/Decisions/ADR-0142-ntlm-negotiate-and-kerberos-answer-through-sspi-on-windows-and-curls-own-code-or-the-system-gss-api-elsewhere.md`
(and `-ListenAddress` set to the host's WSL address, as BL-526 did), `--ntlm -u
<about 600 characters>:p`, server answering the first request with 401
`WWW-Authenticate: NTLM <Type 2>` where Type 2 is MS-NLMP 4.2.4.3's CHALLENGE:
`TlRMTVNTUAACAAAADAAMADgAAAAzgoriASNFZ4mrze8AAAAAAAAAACQAJABEAAAABgBwFwAAAA9TAGUAcgB2AGUAcgACAAwARABvAG0AYQBpAG4AAQAMAFMAZQByAHYAZQByAAAAAAAA`.
Record exit code, stdout, stderr (with `-sS`) and whether a second request is sent.

## Acceptance criteria

- [ ] Notes record the measurement: curl version, command, exit code, stdout, the exact
      stderr line, and how many requests curl sent.
- [ ] The hand-built route (`HandBuiltNtlmSecurityContext` and/or
      `NtlmHttpAuthenticator`) throws `HttpAuthenticationFailedException` carrying the
      measured exit code (expected `CurlExitCode` 100, `TooLarge`) and the measured
      message when Type 3 passes 1024 bytes; an SSPI-shaped refusal on Windows is
      unchanged (its existing tests still pass).
- [ ] A test in `Curl.Authentication.UnitTests` pins the exit code and message for a
      user name of about 600 characters, and a test pins that a Type 3 of exactly the
      largest size that fits still answers.
- [ ] `dotnet build -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and
      branch coverage for `Curl.Authentication.UnitLibrary`.
- [ ] ADR-0180's "a Type 3 past 1024 bytes ends on the 401" choice is marked as
      superseded by this task in a line appended to its Consequences (or the task notes
      why no ADR edit was needed). If that edit is needed, add
      `Documentation/Planning/Decisions` to `touches` first.

## Notes

- If the measurement shows curl does not fail with exit 100 (for example, it sends
  nothing and exits 0), pin what curl does, record it here, and close with that.

## Log

- 2026-09-28: Created.
