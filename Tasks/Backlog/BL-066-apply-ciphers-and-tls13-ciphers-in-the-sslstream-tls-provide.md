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
completed:
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

- [ ] `TlsClientOptions` has `Ciphers` and `Tls13Ciphers`; `null` keeps the earlier
      behaviour.
- [ ] For each platform the ADR from BL-060 names, a named test asserts the decided
      outcome for a valid list, and for an unknown name such as `BOGUS`: the negotiated
      suite, the warning text, or exit 59 with the decided message.
- [ ] `PlatformNotSupportedException` never escapes `AuthenticateAsClientAsync`; a test
      on Windows asserts it.
- [ ] No test is tagged `Integration` and no test opens a socket.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

## Log

- 2026-09-26: Created.
