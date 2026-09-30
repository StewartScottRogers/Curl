---
id: BL-832
title: Pass TLS channel bindings into the hand-built GSS-API Kerberos checksum
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-691]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions/ADR-0171-the-hand-built-gss-api-kerberos-initiator-sends-mit-flags-and-reads-rfc-4121-and-rfc-4757-tokens-in-strict-sequence.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-832 — Pass TLS channel bindings into the hand-built GSS-API Kerberos checksum

## Goal

`KerberosGssContextOptions` takes optional channel bindings, and `KerberosGssContext` puts their MD5 (RFC 4121 section 4.1.1.2, over RFC 2744's `gss_channel_bindings_struct` encoding) in the authenticator checksum's `Bnd` field in place of the zeros it sends now, if measurement shows the platform curl's GSS-API Negotiate passes them.

## Context

- BL-691 sends all-zero bindings (ADR-0171, Consequences). Whether curl's GSS-API Negotiate over HTTPS passes `tls-server-end-point` bindings (RFC 5929) depends on the curl version; measure curl 8.21.0 with MIT on Linux against a loopback HTTPS server with `Record-CurlExchange.ps1` before pinning anything, and record the answer in an ADR (or amend ADR-0171).
- If curl passes none, record that and finish with the zeros kept.

## Acceptance criteria

- [x] The ADR states whether curl 8.21.0 with MIT sends channel bindings, from a recorded exchange.
- [x] When it does, `Curl.Kerberos.UnitTests` pin the `Bnd` field for a known `tls-server-end-point` value against a hand-computed MD5 of the RFC 2744 structure; when it does not, a test pins the zeros with the ADR cited.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-691.
- **Answer: yes, curl with MIT sends channel bindings over HTTPS.** Recorded 2026-09-29 and written into ADR-0171 as an amendment ("Channel bindings").
- **Which curl.** The only Linux curl at hand was WSL Ubuntu's 8.18.0 (OpenSSL 3.5.5, `mit-krb5/1.22.1`); 8.21.0 exists here only as the Windows Schannel/SSPI build, which does not use MIT. I took 8.18.0 as the measurement and said so in the ADR. A newer curl is not expected to drop bindings it already sends.
- **How it was recorded** (reusable by BL-916):
  1. WSL's user-space MIT KDC from an earlier lane (`~/krbtools`, realm `EXAMPLE.TEST`, `alice`/`alicepw`, `HTTP/server.example.test` in `kdc/http.keytab`, KDC on 127.0.0.1:18888).
  2. A throwaway WSL script started the KDC, ran `kinit alice`, fetched the served certificate with `openssl s_client`, then ran `/usr/bin/curl -sk --negotiate -u : --resolve server.example.test:18443:<host IP> https://server.example.test:18443/`.
  3. `Record-CurlExchange.ps1 -Tls -ListenAddress <Windows host WSL IP> -Connections 3 -Curl wsl.exe` served 401 `WWW-Authenticate: Negotiate` then 200. No script change was needed.
  4. A throwaway C# file-based app referencing `Curl.Kerberos.UnitLibrary` pulled the AP-REQ out of the recorded SPNEGO token and decrypted its authenticator with the ccache's HTTP session key (usage 11).
- **Result.** `Bnd` was `CCA1946F38023F173903A4E68BDCCCEA`, which is exactly MD5(00x16 || LE32(len) || "tls-server-end-point:" || SHA-256(cert)) for the sha256RSA certificate with SHA-256 `D7AD5D5F…568B08`. An earlier run with a different throwaway certificate also gave a non-zero `Bnd`.
- **Design.** `KerberosGssContextOptions.ChannelBindings` (`byte[]?`) is the application data only. curl never passes addresses, so address types are 0 and empty. `null` keeps the zeros. Building `tls-server-end-point:` from the connection's certificate belongs to the caller: filed as BL-915 (`Curl.Authentication.UnitLibrary`).
- **Found on the way.** The recorded checksum flags were `0x136`: MIT sends `GSS_C_TRANS_FLAG`, which contradicts ADR-0171's "Flags" bullet. Filed as BL-916 rather than widening this task.
- Tests: `NextToken_ChannelBindings_PutsTheirMd5InTheChecksumAsCurlWithMitDoes` (the recorded value) and `NextToken_ChannelBindings_HashesTheRfc2744StructureWithNoAddresses` (a hand-computed MD5 over "abc", `420B92DA…`). The existing zero-`Bnd` pin still covers no bindings.
- Added the ADR-0171 file to `touches` so it could be amended. No task in Doing names it.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Hand-built GSS-API Kerberos puts the MD5 of RFC 2744 channel bindings in Bnd, as curl 8.18.0 with MIT was recorded doing over HTTPS
