# ADR-0276 — SOCKS5 authentication follows `--socks5-basic` and `--socks5-gssapi`, and GSS-API runs RFC 1961 on the security-context seam

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-615.

## Context

Until BL-615 the SOCKS5 greeting always offered no authentication and GSSAPI (and user name and
password with a credential), and a proxy that picked GSSAPI failed with a fixed SSPI message
(ADR-0084). BL-612 parsed `--socks5-basic`, `--socks5-gssapi`, `--socks5-gssapi-service` and
`--socks5-gssapi-nec`; nothing used them. Measured 2026-09-30 with `Record-CurlExchange.ps1
-Script` playing the SOCKS5 server (BL-615 Notes has every command and byte), curl 8.21.0
(Schannel) on Windows and curl 8.18.0 (OpenSSL, MIT Kerberos 1.22.1) in WSL:

- Neither option: `05 02 00 01`, or `05 03 00 01 02` with `-U`. `--socks5-basic` alone:
  `05 01 00`, or `05 02 00 02` with `-U`. `--socks5-gssapi` alone: `05 02 00 01`, with `-U` too.
- A refused user name and password (`01 01`): exit 97 `User was rejected by the SOCKS5 server (1 1).`
- `--socks5-basic` and the proxy picks 01: exit 97 `SOCKS5 GSSAPI per-message authentication is
  not enabled.`; `--socks5-gssapi` and the proxy picks 02: exit 97 `BASIC authentication proposed
  but not enabled.` Both builds.
- The proxy picks 01 with no Kerberos ticket: Windows `SSPI error: InitializeSecurityContext
  failed: SEC_E_TARGET_UNKNOWN (0x80090303) - The specified target is unknown or unreachable`;
  Linux `GSS-API error: gss_init_sec_context failed: No credentials were supplied, or the
  credentials were unavailable or inaccessible.` and, on the next line, `No Kerberos credentials
  available (default cache: FILE:/tmp/krb5cc_1000)`. Exit 97, nothing more sent.

No KDC was at hand, so a successful GSS-API exchange could not be recorded; it follows RFC 1961
as curl's `socks_sspi.c` and `socks_gssapi.c` run it.

## Decision

1. `Curl.Networking`'s `Socks5AuthenticationOptions` carries the choice into `TcpConnector` as a
   constructor argument per option group, as ADR-0273 carries the pre-proxy: no contract in
   `Curl.Protocol.Abstractions.UnitLibrary` changes. `Curl.Console`'s
   `Socks5AuthenticationMapping` builds it: neither option allows both methods, else only the
   ones given; a method not allowed is neither offered nor answered, and without user name and
   password the credential is dropped, as measured.
2. GSS-API runs on ADR-0142's `ISecurityContextFactory`, the router the proxy tunnel's
   Negotiate uses (`LateBoundSecurityContextFactory`): mechanism Kerberos, the default
   credential, `ProtectionLevel.EncryptAndSign` (the SSPI build asks confidentiality), service
   `--socks5-gssapi-service`, else `--proxy-service-name`, else `rcmd`, joined to the proxy's
   host; a service with `/` is the whole target, split at its first `/`. `--delegation` applies
   off Windows only: the SSPI build asks no delegation, the GSS-API build asks what the option says.
3. The exchange is RFC 1961: each token in a version 1, type 1 message with a two-byte length
   until the context completes; then the protection-level message (type 2) offering level 0,
   wrapped with the context without encryption, or bare under `--socks5-gssapi-nec`; the reply
   unwrapped (bare under NEC) must be one byte, and a level other than 0 fails with curl's
   `SOCKS5 GSS-API protection not yet implemented.`, since curl protects no tunnel byte either.
4. Failure texts follow the platform's build (`Socks5GssapiFailureText`): `SSPI` or `GSS-API` in
   curl's messages, the measured no-credential texts, SSPI's `SEC_E_TARGET_UNKNOWN` also for a
   refusal, and MIT's cache name from `KRB5CCNAME`, else `FILE:/tmp/krb5cc_<uid>` with the uid
   from `/proc/self/status` (0 where absent). The wrap, unwrap and other status texts are curl's
   and SSPI's or MIT's standard texts, not measured.

## Consequences

- `ADR-0084`'s "GSSAPI is offered but not implemented" no longer holds; its greeting stands as
  the default.
- A context the factory cannot make, or no factory (a connector built without one), fails as a
  missing credential does.
- Per-message protection of the tunnel itself stays unimplemented, exactly as in curl 8.21.0.
