---
id: BL-916
title: Send MIT's GSS_C_TRANS_FLAG (0x100) in the hand-built Kerberos checksum flags as measured
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions/ADR-0171-the-hand-built-gss-api-kerberos-initiator-sends-mit-flags-and-reads-rfc-4121-and-rfc-4757-tokens-in-strict-sequence.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-916 — Send MIT's GSS_C_TRANS_FLAG (0x100) in the hand-built Kerberos checksum flags as measured

## Goal

`KerberosGssContext`'s initial-token checksum carries the flags MIT was measured sending: `0x136` for curl's default request (mutual, replay, confidentiality, integrity and `GSS_C_TRANS_FLAG` 0x100), not the `0x36` it sends now, and ADR-0171's "Flags" bullet says so.

## Context

- BL-832 recorded curl 8.18.0 with MIT krb5 1.22.1 doing `--negotiate` over HTTPS (twice) and decrypted the authenticator: the checksum's flags word was `36010000`, i.e. `0x136`. ADR-0171 (written before any MIT machine was at hand) decided MIT's local `GSS_C_TRANS_FLAG` is not sent; the measurement says it is.
- Re-measure before changing: the recording recipe is in BL-832's Notes (WSL's user-space KDC under `~/krbtools`, `Record-CurlExchange.ps1 -Tls -Curl wsl.exe`, decrypt the authenticator with the session key from the ccache, key usage 11). Also check whether SASL `GSSAPI` or SOCKS5 GSS-API change it, and whether `KerberosGssContext.Flags` should expose it.

## Acceptance criteria

- [x] ADR-0171's "Flags" bullet states the measured flags word and cites the recording.
- [x] `KerberosGssContextTests` pin the checksum flags `36010000` for the default request (and `37010000` with delegation), replacing the `36000000` pins.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-832.
- **Re-measured 2026-09-29** with BL-832's recipe, over plain HTTP this time: a WSL script (`~/bl916.sh`) starts the user-space MIT KDC, runs `kinit.mit alice` (with `-f` for the delegation run), then `/usr/bin/curl -s --negotiate -u : [--delegation always] --resolve server.example.test:18916:<host> http://server.example.test:18916/`; `Record-CurlExchange.ps1 -Port 18916 -ListenAddress 172.26.96.1 -Connections 2 -Curl wsl.exe` served 401 Negotiate then 200 (no script change needed). A throwaway C# file-based app referencing `Curl.Kerberos.UnitLibrary` took the AP-REQ out of the SPNEGO token, found the HTTP session key in the copied ccache and decrypted the authenticator (usage 11). Checksum flags: `36010000` by default, `37010000` with `--delegation always`. Matches BL-832's two HTTPS recordings.
- **SASL `GSSAPI` and SOCKS5 GSS-API** were not recorded (no GSSAPI IMAP or SOCKS5 server at hand); they call the same MIT `gss_init_sec_context`, whose context flags always include `GSS_C_TRANS_FLAG`, so they send it too. Recorded in ADR-0171.
- **`KerberosGssContext.Flags` exposes it** as the new `KerberosGssFlags.Transfer` (0x100), because MIT's returned flags include it and the checksum is written from `Flags`. Decided by Claude under Stewart's delegation; recorded in ADR-0171's "Flags" bullet.
- **Gate fix:** `Measure-CodeQuality.ps1` flagged `KerberosEncryption.Create` at complexity 12 (from BL-894, not this change). Folded the two AES-SHA1 arms into a new `AesSha1KerberosEncryption.ForType`, like the AES-SHA2 and Camellia types, bringing it under 10. Inside this task's `touches`.
- Also noted the flag in `Curl.Kerberos.UnitLibrary/CLAUDE.md`.
- Tests: 667 Kerberos tests pass; full fast suite green; Kerberos library 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The hand-built Kerberos checksum sends MIT's measured flags, 0x136 (0x137 with delegation), GSS_C_TRANS_FLAG included
