# ADR-0321 — The Schannel build checks revocation for a `--cacert` chain

- **Status:** Accepted
- **Date:** 2026-09-27
- **Renumbered:** from ADR-0086, a number another ADR also held (BL-663)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-368.

## Context

ADR-0009 makes `SslStreamTlsProvider` behave like curl's Schannel build on Windows and its
OpenSSL build elsewhere. BL-150 measured curl 8.21.0's Schannel build
(`x86_64-w64-mingw32`) with `--cacert root.pem` against an `openssl s_server` on loopback
whose leaf was signed by that private root, which publishes no revocation endpoint:

```
curl: (60) schannel: the revocation status is unknown
```

With `--ssl-no-revoke` the same transfer succeeds. curl's Schannel build builds the chain
itself with `CERT_CHAIN_REVOCATION_CHECK_CHAIN` unless `--ssl-no-revoke` is given. Until
BL-368 the provider set `X509RevocationMode.NoCheck` for every `--cacert` chain, so it
succeeded where curl fails. The OpenSSL build of curl checks no revocation unless asked
(`--crlfile`, `--cert-status`), so it succeeds with or without the option.

`--ssl-no-revoke` is listed in `CurlHelpTable` but not parsed yet, so today nothing can
switch the check off.

## Decision

- **The Schannel build checks revocation for a `--cacert` chain**, with
  `X509RevocationMode.Online` in the chain policy, and reports a chain whose status is
  unknown with curl's line, exit 60.
- **`TlsClientOptions.SkipRevocationCheck` is `--ssl-no-revoke`**: set, the chain policy
  uses `X509RevocationMode.NoCheck` again. Parsing the option and passing it through is
  follow-up work (Cli and Console), filed from BL-368.
- **The root is not checked**, .NET's default `X509RevocationFlag.ExcludeRoot`. A
  self-signed server certificate given as its own `--cacert` keeps succeeding, as the
  existing tests pin; revocation of a self-signed root is not something a CA can publish.
- **The OpenSSL build never checks**, and ignores `SkipRevocationCheck`.
- **Without `--cacert` nothing changes.** The system store path leaves revocation to
  `SslStream`'s default, which does not check; BL-150 did not measure a revoked or
  unknown-status public certificate against the system store.
- Where several trust errors apply, the Schannel build names the first of: not time
  valid, incomplete chain, untrusted root, revocation status unknown, as curl's
  `schannel_verify.c` checks them in that order. So an expired leaf from a private root
  reports `not time valid`, not the revocation status.

## Consequences

- A `--cacert` transfer to a server whose private CA has no revocation endpoint fails on
  Windows, exactly as it does with curl.exe. Until `--ssl-no-revoke` is parsed, the only
  way past it is `-k`; scripts that already pass `--ssl-no-revoke` fail at option parsing
  today regardless of this decision.
- A CA that publishes a revocation endpoint is contacted during the handshake, as Schannel
  does; an unreachable endpoint gives `the revocation status is unknown`.
- Tests: `SslStreamTlsProviderTests.ChainErrors.cs` pins the failure, the success with
  `SkipRevocationCheck`, and the OpenSSL build's success; `TlsFailureMessagesTests` pins
  the order of the trust errors.

## Alternatives considered

- **Keep `NoCheck`.** Friendlier for private CAs, but prints nothing where curl.exe fails
  with exit 60, which breaks the drop-in rule in the direction a script cannot detect.
- **Check revocation only once `--ssl-no-revoke` is parsed.** Leaves the known difference
  in place for no gain; the option lands as its own small task, and the check is correct
  without it.
- **Check the root too (`X509RevocationFlag.EntireChain`).** Would fail every self-signed
  `--cacert` server certificate, which the existing tests pin as accepted. BL-150 did not
  measure that case without `--ssl-no-revoke`; if a measurement shows curl.exe fails it,
  this is the line to revisit.
