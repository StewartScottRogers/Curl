---
id: BL-5062
title: Report chain trust failures before purpose and raw-status errors in TlsFailureMessages on both TLS profiles
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/TlsFailureMessagesTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5062 — Report chain trust failures before purpose and raw-status errors in TlsFailureMessages on both TLS profiles

## Goal

When a server certificate's chain is not trusted, Curl's TLS failure line names the trust failure first, as curl does on both profiles:
- **OpenSSL:** before `unsuitable certificate purpose (26)`.
- **Schannel:** the untrusted root before the raw `schannel: error 0x...` mask.

## Context

The shift-end review of 2026-11-12 found this in the BL-5009 and BL-5032 changes (silent-failure-hunter and type-design-analyzer, ADR-0710). The `--cacert` chain policy now also adds the serverAuth application policy (`ServerCertificateVerification.cs` ~217), so `NotValidForUsage` comes up more often.

**OpenSSL** (`Curl.Networking.UnitLibrary/TlsFailureMessages.cs:865`)
- `OpenSslVerifyError` returns `unsuitable certificate purpose (26)` before it looks at trust.
- OpenSSL checks the chain's trust (errors 18, 19 and 20) before it checks purpose. The comment above the method says the same: "OpenSSL reports a failed trust before...".
- Failure case: a self-signed CA certificate (keyUsage `keyCertSign,cRLSign`) is served as the leaf and is not in `--cacert`. curl prints `self-signed certificate (18)`; Curl prints `(26)`.

**Schannel** (`TlsFailureMessages.cs:899-904`)
- The new `schannel: error 0x...` branch runs before the `UntrustedRoot` and `RevocationStatusUnknown` checks, and it masks those bits out of the number it prints.
- curl's `schannel_verify.c` checks for an untrusted root before it falls back to the raw trust-error mask.
- Failure case: a chain with `UntrustedRoot | NotValidForUsage` prints `schannel: error 0x00000010`.

Measure real curl with `Record-CurlExchange.ps1` for both cases before pinning the text.

## Acceptance criteria

- [ ] On the OpenSSL profile, a chain that is untrusted and also not valid for server use reports the trust error, not `(26)`. A unit test in `TlsFailureMessagesTests` pins it.
- [ ] On the OpenSSL profile, a trusted chain whose leaf keyUsage forbids server use still reports `unsuitable certificate purpose (26)`.
- [ ] On the Schannel profile, `UntrustedRoot` together with another status reports the text measured from curl's Schannel build. The measurement is recorded in this task's Notes and pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-12: Created.
