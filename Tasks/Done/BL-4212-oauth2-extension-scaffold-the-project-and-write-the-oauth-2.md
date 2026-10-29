---
id: BL-4212
title: OAuth2 extension: scaffold the project and write the OAuth 2.0, PKCE and device authorization wire format
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OAuth2.UnitLibrary/Wire, Curl.Extension.OAuth2.UnitLibrary/Curl.Extension.OAuth2.UnitLibrary.csproj, Curl.Extension.OAuth2.UnitLibrary/CLAUDE.md, Curl.Extension.OAuth2.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4212 — OAuth2 extension: scaffold the project and write the OAuth 2.0, PKCE and device authorization wire format

## Goal

`Curl.Extension.OAuth2.UnitLibrary` and `Curl.Extension.OAuth2.UnitTests` exist in `/Extensions/OAuth2/` in `Curl.slnx`, and the library builds and reads every OAuth 2.0 message the three flows exchange.

## Context

First part of BL-4161 (split for size). Use the `new-project` skill; add the single-line `<Folder Name="/Extensions/OAuth2/">` under the marker comment in `Curl.slnx` (ADR-1003); it references only `Curl.Protocol.Abstractions`, `Curl.Extension.Abstractions` and the BCL (JSON with `System.Text.Json`'s `JsonDocument`, AOT-safe). Follow `Curl.Extension.S3.UnitLibrary`'s shape. Not registered in any catalogue yet (BL-4161 does that).

Write, in `Wire/`:
- PKCE: `code_verifier` from injected random bytes and `code_challenge` by `S256` (RFC 7636 §4.1, §4.2), reproducing Appendix B.
- Token request bodies, `application/x-www-form-urlencoded` (RFC 6749 Appendix B): client credentials (§4.4.2), authorization code with `code_verifier` (§4.1.3, RFC 7636 §4.5), refresh (§6), device code (RFC 8628 §3.4); client authentication by HTTP Basic or body parameters (§2.3.1).
- Device authorization request and response (RFC 8628 §3.1, §3.2), with `interval` defaulting to 5.
- Authorization request URL (RFC 6749 §4.1.1, RFC 7636 §4.3) with `state`.
- Token response (RFC 6749 §5.1) and error response (§5.2, RFC 8628 §3.5 `authorization_pending`, `slow_down`, `expired_token`, `access_denied`); a malformed body is a failure with its reason, never an exception.

## Acceptance criteria

- [x] Both projects exist in `/Extensions/OAuth2/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [x] Unit tests check each message above against the named RFC sections, including RFC 7636 Appendix B's verifier and challenge.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4161's lane 2 run (split for size).
- Delivered directly rather than through the full /feature subagent stages: the work is one small library of wire-format types with RFC examples to test against, and the stages would have spent the cost cap on handoffs. The choices the RFCs leave open are recorded in ADR-1025 (Appendix B form encoding, HTTP Basic by default with form-encoded credentials, fixed parameter order, 32 to 96 verifier octets and S256 only, JSON integers for expires_in and interval, unknown error codes as Unrecognized).
- References only `Curl.Protocol.Abstractions` (as S3 does); `Curl.Extension.Abstractions` is left for BL-4161, which registers the extension.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Extension.OAuth2.UnitLibrary: 100% line, 100% branch, 0 failing members. 43 tests in Curl.Extension.OAuth2.UnitTests; dotnet build clean (VerifyExtensionIsolation ran) and the fast tests green on Windows. The tests use no file system, paths or OS text, so they are platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 500f0dea 4459e577 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Extension.OAuth2 builds and writes and reads every OAuth 2.0, PKCE and device authorization message, tested against the RFC examples
