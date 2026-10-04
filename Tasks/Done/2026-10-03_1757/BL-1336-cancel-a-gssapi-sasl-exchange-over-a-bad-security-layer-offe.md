---
id: BL-1336
title: Cancel a GSSAPI SASL exchange over a bad security-layer offer with curl's 'GSSAPI handshake failure' -v lines
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1335]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1336 — Cancel a GSSAPI SASL exchange over a bad security-layer offer with curl's 'GSSAPI handshake failure' -v lines

## Goal

When the GSSAPI exchange's security-layer step cannot answer the server's offer, `SecurityContextSaslExchange.RespondAsync` returns `null` with `ISaslExchange.CancelReason` (BL-1335) set to the line curl 8.21.0 writes for that case, so the IMAP, POP3 and SMTP handlers can cancel and write it.

## Context

- Today `Curl.Authentication.UnitLibrary/SecurityContextSaslExchange.cs` `AnswerSecurityLayerOffer` returns `null` when `context.Unwrap` fails, when the unwrapped offer is not 4 bytes, or when its first byte lacks the no-security-layer bit (`NoSecurityLayer`, 0x01), with nothing to say why; the handlers then fail with exit 67.
- curl 8.21.0 (tag `curl-8_21_0`), `Curl_auth_create_gssapi_security_message`, each case an `infof` line then `CURLE_BAD_CONTENT_ENCODING` (which `lib/curl_sasl.c` lines 789-793 turn into a cancel):
  - the Windows (SSPI) build, `lib/vauth/krb5_sspi.c`: empty challenge (line 268) `GSSAPI handshake failure (empty security message)`; `DecryptMessage` fails (line 300) `GSSAPI handshake failure (decryption failed)`; not 4 bytes (line 306) `GSSAPI handshake failure (invalid security data)`; no `KERB_WRAP_NO_ENCRYPT` bit (line 318) `GSSAPI handshake failure (invalid security layer)`.
  - the GSS-API build (Linux, macOS), `lib/vauth/krb5_gssapi.c`: the same texts at lines 198, 217 and 233, except that a failed `gss_unwrap` (line 210) writes `gss_unwrap() failed: ` followed by the GSS library's own status text (`Curl_gss_log_error`, `lib/curl_gssapi.c` lines 428-440).
- Choose the build's texts as `NegotiateHttpAuthenticator` does (`wordsFailuresAsSspi`, defaulting to `OperatingSystem.IsWindows()`), passed in by `SaslAuthenticator` where it builds the exchange (`SaslAuthenticator.cs` line 186), so both platforms are testable everywhere.
- The GSS-API build's unwrap failure text depends on MIT krb5's message table, which Curl's hand-built Kerberos does not reproduce: off Windows, set `CancelReason` to `gss_unwrap() failed: ` with no status text, and say so in the member's remarks as a known divergence to measure later. Decided here under the standing delegation (simplest choice that keeps the cancel behaviour right).
- Only the security-layer step changes; a context token step that fails stays as it is (exit 67 / exit 94 paths pinned by BL-856).

## Acceptance criteria

- [x] Tests in `Curl.Authentication.UnitTests` drive a GSSAPI exchange through a completed `ScriptedSecurityContext` and pin, for the SSPI wording: an empty offer gives `null` with `CancelReason` `GSSAPI handshake failure (empty security message)`; a failing `Unwrap` gives `GSSAPI handshake failure (decryption failed)`; an unwrapped offer of 3 bytes gives `GSSAPI handshake failure (invalid security data)`; an offer of `{ 0x02, 0, 0, 0 }` gives `GSSAPI handshake failure (invalid security layer)`.
- [x] The same tests pin the GSS-API wording: the same texts, except `gss_unwrap() failed: ` for a failing `Unwrap`.
- [x] A test pins that a good offer (`{ 0x01, 0, 0x10, 0 }`) still answers with the wrapped `{ 0x01, 0, 0, 0 }` plus the authorization identity and leaves `CancelReason` `null`, and that the NTLM exchange never sets it.
- [x] `dotnet build Curl.Authentication.UnitTests -warnaserror` is clean; `dotnet test Curl.Authentication.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports no failing member.

## Notes

- Plan: `SecurityContextSaslExchange` takes `wordsFailuresAsSspi` and sets `CancelReason` in the security-layer step, checking curl's order (empty, unwrap, length, layer bit). `SaslAuthenticator` passes a new public init property `WordsGssapiFailuresAsSspi` (default `OperatingSystem.IsWindows()`), the same shape as `GssapiDelegation`, so tests pick either wording. Sensible default taken: an init property rather than a new constructor parameter, so no existing caller changes. `ScriptedSecurityContext` gained `UnwrapFails` to drive a failed decryption.
- An empty offer is now checked before `Unwrap` is called, as curl does.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` gives 100% line, 99.68% branch; every member this task changed passes. The two failing members are the pre-existing ones BL-1303 already recorded (`NtlmHttpAuthenticator.ContextRequestFor` 80% branch, `SystemSecurityContext.Step` 87.5% branch), in files this task did not change, whose remaining branches are taken by off-Windows tests skipped on Windows.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. A GSSAPI SASL exchange over a security-layer offer curl cannot answer returns null with curl's GSSAPI handshake failure line as CancelReason, in the SSPI or GSS-API wording
