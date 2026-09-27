---
id: BL-150
title: Measure the TLS failure cases ADR-0009 left unmeasured and pin them in SslStreamTlsProvider
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-064]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-150 — Measure the TLS failure cases ADR-0009 left unmeasured and pin them in SslStreamTlsProvider

## Goal

Every TLS failure case listed below is measured against the reference curl builds. For each one, `TlsFailureMessages` and its tests either change to the measured text, or keep BL-064's default with the measurement written down.

## Context

- ADR-0009 (`Documentation/Planning/Decisions/ADR-0009-tls-behaviour-matches-the-platforms-usual-curl-build.md`) decided which build to match: Schannel-build messages on Windows and OpenSSL-build messages elsewhere. BL-064 implemented it in `Curl.Networking.UnitLibrary` (`TlsFailureMessages.cs`, `SslStreamTlsProvider.cs`, `TlsClientOptions.cs`, with tests in `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs`, `SslStreamTlsProviderTests.cs` and `TlsClientOptionsTests.cs`). Where the ADR had no measurement, BL-064 chose a default. These are the cases to measure, each with the default BL-064 took:
  1. **Server closes mid-handshake (exit 35).** Schannel default: `schannel: failed to receive handshake, SSL/TLS connection failed`. This is curl source text and has not been measured. OpenSSL default: `TLS connect error: <innermost .NET exception message>`. That is a stand-in, because .NET raises the EOF before OpenSSL sees it.
  2. **Name mismatch against a certificate with subjectAltName entries (exit 60).** The ADR measured only a CN-only certificate. BL-064 uses the ADR's CN form `SSL: certificate subject name '<CN>' does not match target hostname '<host>'` for every certificate. The OpenSSL build probably prints `SSL: no alternative certificate subject name matches target hostname '<host>'` for SAN certificates, or `ipv4 address` / `ipv6 address` in place of `hostname` when the target is an IP literal.
  3. **Schannel name mismatch without `--cacert` (exit 60).** BL-064 uses the ADR's `schannel: CertFindExtension() returned no extension.`. Native Schannel probably reports SEC_E_WRONG_PRINCIPAL (0x80090322) here instead.
  4. **`--cacert` naming a directory, OpenSSL build.** Default: exit 77 `error adding trust anchors from file: <path>`.
  5. **Whether `--cacert` and `--capath` replace the default trust store or add to it.** ADR-0009 section 4 left this to BL-064. BL-064 decided: `--cacert` replaces the store, `--capath` alone adds beside the system store, and both together trust the union of the cacert and capath certificates.
  6. **OpenSSL verify errors beyond the measured 18.** BL-064 maps an untrusted chain ending in a self-signed root to 19, an incomplete chain to 20, and certificate validity failures to 9 (not yet valid) and 10 (expired).
  7. **macOS.** SslStream there does not use OpenSSL, so exit 35 has no OpenSSL error string and falls back as in case 1. The CA bundle path the OpenSSL build uses on macOS has not been measured (ADR-0009 section 4).
  8. **Linux strings were measured on curl 8.18.0, not 8.21.0.** Re-measure the ADR's OpenSSL-build strings on 8.21.0.
- Reference builds: curl 8.21.0 Schannel on Windows, and an OpenSSL build of curl 8.21.0 on Linux. For macOS, record what was or was not measured. Run each case with `-sS` and capture standard error byte for byte.
- **Measuring needs the reference curl binaries and a TLS test server**, for example `openssl s_server` with purpose-made certificates (CN-only, SAN with DNS names, SAN with IP addresses, self-signed, missing intermediate, expired, not yet valid). If the running agent does not have them, do not guess. Move this task to `Blocked` with the reason "needs Stewart to provide curl 8.21.0 Schannel and OpenSSL reference binaries and a TLS test server (openssl s_server) to measure against".
- Do not edit ADR-0009. If a measurement contradicts one of its decisions, file a new ADR task for Stewart rather than changing this one's scope.
- Out of scope: the exit 60 help block (BL-149, `Curl.Console`) and printing `SslStreamTlsProvider.Warnings` (BL-072).

## Acceptance criteria

- [ ] Case 1: named tests in `Curl.Networking.UnitTests` pin the measured Schannel and OpenSSL exit 35 text for a server that closes mid-handshake, or Notes record the measurement and why the default stands.
- [ ] Case 2: a named test pins the OpenSSL-build exit 60 message for a SAN certificate whose names do not match, one for each of hostname, IPv4 and IPv6 targets, or Notes record why the default stands.
- [ ] Case 3: a named test pins the Schannel-build exit 60 message for a name mismatch without `--cacert`, or Notes record why the default stands.
- [ ] Case 4: a named test pins the OpenSSL-build exit code and message for `--cacert <directory>`.
- [ ] Case 5: named tests pin the measured trust-store behaviour for `--cacert` alone, `--capath` alone, and both together, in each build.
- [ ] Case 6: named tests pin the OpenSSL verify-result text for self-signed-root (19), incomplete chain (20), not yet valid (9) and expired (10), each matching the measured bytes.
- [ ] Case 7: Notes record what was measured on macOS (exit 35 text, CA bundle path), or that it was not measured and why.
- [ ] Case 8: the OpenSSL-build strings BL-064 took from the 8.18.0 measurements are checked against 8.21.0. Named tests pin any that changed, and Notes say which were confirmed.
- [ ] Notes state, for every case, the curl version and build it was measured against and the exact command line used.
- [ ] ADR-0009 is unchanged (`git diff master -- Documentation/Planning/Decisions/ADR-0009-tls-behaviour-matches-the-platforms-usual-curl-build.md` is empty).
- [ ] No new test carries `TestCategory=Integration` or opens a socket.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes, including 100% line and branch coverage of `Curl.Networking.UnitLibrary`.

## Notes

- Filed as a follow-up to BL-064.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Lane handed over mid-run while the factory's restart logic was fixed; the run had only just resumed and left no work.
