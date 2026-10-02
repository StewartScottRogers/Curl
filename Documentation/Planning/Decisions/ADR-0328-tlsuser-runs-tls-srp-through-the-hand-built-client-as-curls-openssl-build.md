# ADR-0328 — --tlsuser runs TLS-SRP through the hand-built client as curl's OpenSSL build does

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-712.
Wires ADR-0229's SRP exchange to `--tlsuser`, `--tlspassword` and `--tlsauthtype`, as
ADR-0151 routes them.

## Context

curl offers TLS-SRP only in its OpenSSL and GnuTLS builds; the Schannel build has none.
curl 8.18.0's OpenSSL 3.5.5 build was measured against `openssl s_server` in WSL on
2026-10-01 (BL-712 Notes). What it does is in `lib/vtls/openssl.c`: once a TLS user is set,
libcurl defaults the authentication type to SRP, prints `Using TLS-SRP username: <user>`,
fails with exit 43 `failed setting SRP password` when no password was given, and, unless
`--ciphers` names a list, prints `Setting cipher list SRP` and sets OpenSSL's `SRP` cipher
list.

## Decision

1. **Routing.** `TlsClientRouting` sends any connection with `TlsUser` set to the hand-built
   client on every platform. `--tlspassword` alone turns nothing on, as in curl.
2. **The login.** `TlsSrp.CredentialsOf` makes the `TlsSrpCredentials` from the user and the
   password, verbatim (ADR-0229 point 3). With no password the connect fails with exit 43
   and curl's text after the user line, before the ClientHello (measured).
3. **The cipher list.** Without `--ciphers`, the TLS 1.2 suites become OpenSSL 3.5's `SRP`
   list at the default security level, in its order: `0xc022`, `0xc021`, `0xc020`, `0xc01f`,
   `0xc01e`, `0xc01d` (`openssl ciphers -stdname SRP`; 3DES is below the level). The TLS 1.3
   suites stay, since OpenSSL's cipher list does not govern TLS 1.3: a TLS 1.3 server
   completes the handshake without SRP, as measured. With `--ciphers` that list is offered.
4. **Verbose lines.** Both lines are reported through `ITransferEvents.ReportInfo` when the
   handshake is prepared.
5. **Failure text.** Every exit 35 of an SRP handshake uses the OpenSSL build's line on every
   platform, since only that build has SRP (ADR-0151). OpenSSL's alert reasons gain
   `unknown_psk_identity` (`tlsv1 alert unknown psk identity`, measured from a server that
   does not know the user); a server without SRP answers `handshake_failure` (measured), and
   a wrong password the server's `decrypt_error` (OpenSSL's server answer to a Finished that
   does not verify, the `tlsv1 alert decrypt error` line).
6. **The proxy forms.** `--proxy-tlsuser`, `--proxy-tlspassword` and `--proxy-tlsauthtype`
   are not yet parsed, and `curl -V` does not yet list `TLS-SRP`. Both need
   `Curl.Cli.UnitLibrary`, which another lane held while BL-712 ran, so BL-1127 does them.
   The proxy's `TlsClientOptions` already never carry the origin's SRP options.

## Consequences

- `--tlsuser u --tlspassword p` authenticates with SRP on Windows, Linux and macOS.
- OpenSSL 3.5.5's `s_server -srpvfile` fails its user lookup (it prints a corrupt user name
  and answers `unknown_psk_identity` to any user), so a successful SRP transfer and a wrong
  password could not be measured against it. The success is pinned against
  `Curl.Tls.UnitTests`' in-memory SRP server (ADR-0229), and the wrong password's text comes
  from OpenSSL's alert reasons.
