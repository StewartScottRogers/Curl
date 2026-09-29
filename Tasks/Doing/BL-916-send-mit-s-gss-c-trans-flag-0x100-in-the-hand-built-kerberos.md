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
completed:
---
# BL-916 — Send MIT's GSS_C_TRANS_FLAG (0x100) in the hand-built Kerberos checksum flags as measured

## Goal

`KerberosGssContext`'s initial-token checksum carries the flags MIT was measured sending: `0x136` for curl's default request (mutual, replay, confidentiality, integrity and `GSS_C_TRANS_FLAG` 0x100), not the `0x36` it sends now, and ADR-0171's "Flags" bullet says so.

## Context

- BL-832 recorded curl 8.18.0 with MIT krb5 1.22.1 doing `--negotiate` over HTTPS (twice) and decrypted the authenticator: the checksum's flags word was `36010000`, i.e. `0x136`. ADR-0171 (written before any MIT machine was at hand) decided MIT's local `GSS_C_TRANS_FLAG` is not sent; the measurement says it is.
- Re-measure before changing: the recording recipe is in BL-832's Notes (WSL's user-space KDC under `~/krbtools`, `Record-CurlExchange.ps1 -Tls -Curl wsl.exe`, decrypt the authenticator with the session key from the ccache, key usage 11). Also check whether SASL `GSSAPI` or SOCKS5 GSS-API change it, and whether `KerberosGssContext.Flags` should expose it.

## Acceptance criteria

- [ ] ADR-0171's "Flags" bullet states the measured flags word and cites the recording.
- [ ] `KerberosGssContextTests` pin the checksum flags `36010000` for the default request (and `37010000` with delegation), replacing the `36000000` pins.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-832.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
