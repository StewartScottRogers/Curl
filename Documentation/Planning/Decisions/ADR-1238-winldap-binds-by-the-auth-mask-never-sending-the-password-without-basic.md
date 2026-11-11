# ADR-1238: WinLDAP binds by the auth mask, never sending the password without Basic

- Status: Accepted
- Date: 2026-11-11
- Decided by Claude under Stewart's delegation (BL-4899)

## Context

The outside fitness review of 2026-11-11 (STD-001) found that `--ntlm`, `--digest` and
`--negotiate` with `-u` sent LDAP's simple bind, the password in cleartext. curl's Windows
build chooses in `ldap_win_bind`: a simple bind only when the auth mask holds Basic;
otherwise `ldap_win_bind_auth` calls WinLDAP's `ldap_bind_s` with `LDAP_AUTH_NEGOTIATE`,
`LDAP_AUTH_NTLM` or `LDAP_AUTH_DIGEST` (the first the mask holds, in that order) and `-u`'s
credentials, or `LDAP_AUTH_NEGOTIATE` as the logged-on user when it holds none of them.

Measured on 2026-11-11 with curl 8.22.0 (Schannel, WinLDAP) and `Record-CurlExchange.ps1
-Script`, against a server that answers searches with an empty SearchResultDone and binds
with success:

- `--ntlm`: `supportedCapabilities`, then the Sicily `sicilyNegotiate` bind named `NTLM`.
- `--negotiate`: the logon bind's reads (`supportedCapabilities`, `supportedSASLMechanisms`,
  `supportedCapabilities`), then the Sicily bind.
- `--digest`: `supportedCapabilities`, `supportedSASLMechanisms`, then the SASL `DIGEST-MD5`
  bind with empty credentials.
- Each attempt is made twice, then an UnbindRequest; exit 38
  `LDAP local: bind via ldap_win_bind Protocol Error`.
- `--basic` or no auth option: the simple bind, exit 0.

## Decision

The WinLDAP dialect makes the same choice (`LdapProtocolHandler.WinLdapAuthBindingOf`) and
`WinLdapAuthBind` (renamed from `WinLdapLogonBind`) binds by `WinLdapAuthMethod` with the
credentials `ILdapLogonTokenSource.Start` is now given. A `success` to the `DIGEST-MD5` bind
is `Protocol Error`, as measured; a DIGEST-MD5 challenge is `Local Error` until the response
is computed. The OpenLDAP dialect still always sends the simple bind, as curl's OpenLDAP
build does: it reads no auth mask for LDAP.

## Consequences

The password never leaves in cleartext when a challenge/response scheme was asked for. A
real DIGEST-MD5 exchange over LDAP still fails until its response is built.
