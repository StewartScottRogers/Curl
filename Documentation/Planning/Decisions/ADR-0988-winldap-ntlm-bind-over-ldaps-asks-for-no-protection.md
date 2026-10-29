# ADR-0988: WinLDAP's bind as the logged-on user asks for no signing or sealing over ldaps

- Status: Accepted
- Date: 2026-10-28
- Decided by Claude under Stewart's delegation (BL-4046)

## Context

The WinLDAP dialect's bind without `-u` (ADR-0166) asked the security package for
`ProtectionLevel.EncryptAndSign` on every session and sealed the session after the bind, as
WinLDAP does on plaintext `ldap://`. BL-4044 measured curl 8.22.0 (Schannel, WinLDAP) over
`ldaps://` (`-sS -k ldaps://localhost:21909/dc=test`, a server answering the three rootDSE
searches and then silent until it closes): its `sicilyNegotiate` carries NTLM flags
`07 b2 08 a2` (no sign, seal, LM key or key exchange), where Curl sent `b7 b2 08 e2`, and it
exits 38 `curl: (38) LDAP local: bind via ldap_win_bind Unavailable`, where Curl printed
`... Timeout`.

## Decision

- `ILdapLogonTokenSource.Start` takes the `ProtectionLevel` to ask for. `WinLdapLogonBind`
  asks for `ProtectionLevel.None` on `ldaps` and `ProtectionLevel.EncryptAndSign` on `ldap`.
  `NegotiateAuthentication` with `None` sends exactly `07 b2 08 a2` on Windows (measured
  2026-10-28 with a file-based app).
- A bind over `ldaps` that succeeds starts no SASL security layer: TLS already protects the
  session, and an authentication asked for no protection has no sealing keys.
- A server that closes during an `ldaps` bind fails with WinLDAP's `Unavailable`; on `ldap`
  it stays `Timeout`, and a malformed reply stays `Timeout` on both (not measured otherwise).

## Consequences

- The plaintext path is unchanged: its flags are still the ones BL-830 measured.
- Re-measuring the whole exchange end to end needs BL-4044's recorder support for WinLDAP's
  second TLS connection and its `-k` fix for the system-store-only provider; BL-4044 does that
  measurement.
