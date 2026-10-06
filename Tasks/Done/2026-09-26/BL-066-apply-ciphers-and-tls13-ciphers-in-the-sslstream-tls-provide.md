---
id: BL-066
title: Apply --ciphers and --tls13-ciphers in the SslStream TLS provider as decided
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-060, BL-062]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-066 — Apply --ciphers and --tls13-ciphers in the SslStream TLS provider as decided

## Goal

`SslStreamTlsProvider` applies `TlsClientOptions.Ciphers` (`--ciphers`) and
`TlsClientOptions.Tls13Ciphers` (`--tls13-ciphers`) on each platform exactly as the ADR
from BL-060 decides, including the exit 59 failure where it decides one.

## Context

- BL-060 (Stewart) decides, per platform, whether each option is honoured (and with
  which name syntax), ignored (and with which warning), or refused with exit 59
  (`CURLE_SSL_CIPHER`, <https://curl.se/libcurl/c/libcurl-errors.html>;
  `CurlExitCode.SslCipher`). Read that ADR in `Documentation/Planning/Decisions/` first.
  Its Context records the measured curl 8.21.0 Schannel lines, for example
  `curl: (59) schannel: Failed setting algorithm cipher list`.
- BL-062 adds `SslStreamTlsProvider` and `TlsClientOptions`; add
  `string? Ciphers` and `string? Tls13Ciphers`, the option values verbatim as BL-067
  parses them.
- BCL: `SslClientAuthenticationOptions.CipherSuitesPolicy` with `TlsCipherSuite` values;
  not supported on Windows (the handshake throws `PlatformNotSupportedException` when it
  is set). Any OpenSSL-name-to-`TlsCipherSuite` map is hand-written; no package.
- Tests use BL-062's in-memory handshake harness; tests that exercise
  `CipherSuitesPolicy` run only where it is supported and otherwise report
  `Assert.Inconclusive` with the reason.

## Acceptance criteria

- [x] `TlsClientOptions` has `Ciphers` and `Tls13Ciphers`; `null` keeps the earlier
      behaviour.
- [x] For each platform the ADR from BL-060 names, a named test asserts the decided
      outcome for a valid list, and for an unknown name such as `BOGUS`: the negotiated
      suite, the warning text, or exit 59 with the decided message.
- [x] `PlatformNotSupportedException` never escapes `AuthenticateAsClientAsync`; a test
      on Windows asserts it.
- [x] No test is tagged `Integration` and no test opens a socket.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

- Delivered directly in the session rather than through the full `/feature` subagent
  chain: ADR-0011 already fixes every behaviour, so there was no design left to plan.
- `OpenSslCipherSuites` (new, internal) holds the 27-entry OpenSSL-name table for the
  TLS 1.2-and-below suites, IANA matching through `Enum.TryParse<TlsCipherSuite>`
  (restricted to names beginning `TLS_` so digits never match), splitting on `:` `,`
  and space, and the ADR's composition: TLS 1.3 suites first (as OpenSSL orders them),
  then TLS 1.2-and-below; duplicates kept once. Each table entry is pinned by a DataRow.
- The exit 59 messages live in `TlsFailureMessages`, with the others.
- Order (default taken): the cipher options are checked before `--cert` is loaded in
  both builds, so a bad cipher list is exit 59 even when the certificate is also bad.
  Schannel's credential set-up applies the algorithm list before the client
  certificate; the OpenSSL build's order for this pairing was not measured.
- On Windows `CipherSuitesPolicy` cannot be constructed, and CA1416 requires a guard,
  so the OpenSSL build (only reachable from tests there) checks
  `OperatingSystem.IsWindows()` and returns exit 59 with the message of the option
  given (`--ciphers` first) instead of constructing it. No
  `PlatformNotSupportedException` can escape.
- The negotiation tests (`...OpenSslBuildOverTls12_NegotiatesTheNamedSuite`,
  `...OverTls13_NegotiatesTheNamedSuite`) are `Assert.Inconclusive` on Windows. WSL on
  this host has no .NET SDK, so they have not been run on Linux yet; CI on Linux is
  where they run.
- Tests: Curl.Networking.UnitTests 233 passed, 6 skipped (Linux-only) on Windows.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --ciphers and --tls13-ciphers follow ADR-0011: Schannel build exit 59 / ignore, OpenSSL build CipherSuitesPolicy with exit 59 for lists naming no suite
