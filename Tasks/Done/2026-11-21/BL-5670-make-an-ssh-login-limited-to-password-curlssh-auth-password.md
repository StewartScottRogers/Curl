---
id: BL-5670
title: Make an SSH login limited to password (CURLSSH_AUTH_PASSWORD) try only the password, never keyboard-interactive
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Ssh.UnitLibrary/Authentication/SshUserAuthentication.cs, Curl.Api.UnitLibrary/Options/Protocols/EasySshOptions.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5670 — Make an SSH login limited to password (CURLSSH_AUTH_PASSWORD) try only the password, never keyboard-interactive

## Goal

With the allowed SSH login methods set to password only (`Curl.Api` `SshAuthenticationTypes.Password`, or `Libcurl`'s `CURLOPT_SSH_AUTH_TYPES` = `CURLSSH_AUTH_PASSWORD`), a refused password ends the login after one password attempt, as libcurl does; no keyboard-interactive attempt follows.

## Context

From Surly cookbook bug report `2026-11-21T0408-Curl-bug-report.md` (batch 8 of the 1,000-recipe plan), finding 1. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

Repro: `surl -v --directory site --throwaway-hostkey --user alice:s3cret sftp://127.0.0.1:2222/`; a `Curl.Api` `CurlRequest` to `sftp://127.0.0.1:2222/` with `new CurlSshOptions { AuthenticationTypes = SshAuthenticationTypes.Password }` and a wrong password (the cookbook recipe `sftp-refuse-the-first-login-in-csharp` shows the setup). Observed in surl's `-v` log: a password attempt, then a keyboard-interactive attempt. Expected: one password attempt, as libcurl with `CURLSSH_AUTH_PASSWORD` makes (libcurl's libssh2 backend tries keyboard-interactive only when `CURLSSH_AUTH_KEYBOARD` is in the mask). Either the mask does not reach `Curl.Ssh.UnitLibrary/Authentication/SshUserAuthentication.cs`, or the method order there ignores it. Check the console (`CURLOPT_SSH_AUTH_TYPES` has no curl tool option, but `Libcurl` and `Curl.Api` set it) and `Libcurl` too, and each mask bit (publickey, password, host, keyboard, agent).

## Acceptance criteria

- [x] With the mask set to password only, a wrong password produces exactly one `SSH_MSG_USERAUTH_REQUEST` with method `password` and the transfer fails with `LoginDenied` (67), pinned by a test against the in-memory SSH server.
- [x] With the mask set to keyboard-interactive only, no password request is sent; with the default mask, the order matches libcurl's, pinned by tests.
- [x] The same holds through `Libcurl`'s `CURLOPT_SSH_AUTH_TYPES`, pinned by a test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: the mask reached `EasySshOptions` but never the SSH library. Added `SshOptions.AuthenticationMask` (Curl.Protocol.Abstractions), mapped from `EasySshOptions.AuthenticationTypes` in `SshOptionsMapping`, passed by `SshProtocolHandler` to `SshUserAuthentication.AllowedMethodMask`, which gates publickey, password, agent and keyboard-interactive on their bits as libcurl's libssh2 backend does (host-based is not implemented in curl or here). Files outside the original `touches` (SshOptions.cs, SshOptionsMapping.cs, SshProtocolHandler.cs) were added; no other Doing task named them. Decided by Claude under Stewart's delegation; no ADR needed (a bug fix, no new behaviour choice).
- Libcurl criterion: Libcurl's setter already stored the mask in the same `EasySshOptions`; `ManagedEasyTransferTests.SshAuthTypes` pins it through the engine both Libcurl and Curl.Api drive.
- Tests: `SshProtocolHandlerTests.AuthenticationMask`, `ManagedEasyTransferTests.SshAuthTypes`. Measure-CodeQuality for Curl.Ssh.UnitLibrary: 0 failing members; fast tests of dependents green in that run.
- Linux and macOS: tests are platform-neutral.

## Log

- 2026-11-21: Created from Surly bug report 2026-11-21T0408, finding 1.
- 2026-11-21: Backlog -> Doing. Criteria at claim: c66c95b2 cc0f29f3 38f618d6 3d836d5e.
- 2026-11-21: Doing -> Done. SSH auth-type mask now limits login methods; password-only no longer tries keyboard-interactive
