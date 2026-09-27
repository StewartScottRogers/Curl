---
id: BL-354
title: Record the SOCKS handshake decisions in an ADR
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-213]
touches: [Documentation/Planning/Decisions, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-09-27
completed:
---
# BL-354 — Record the SOCKS handshake decisions in an ADR

## Goal

An ADR, marked "Decided by Claude under Stewart's delegation", records the behaviour choices BL-213 made for SOCKS tunnels.

## Context

- BL-213 implemented SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h in `Curl.Networking.UnitLibrary` (`SocksProxyTunnel`, `Socks4Handshake`, `Socks5Handshake`), measured against curl 8.21.0 (mingw, Schannel). Its `touches` did not include `Documentation/Planning/Decisions` (BL-302 held it), so the ADR was split out.
- The decisions to record, with BL-213's Notes as the evidence:
  1. The SOCKS5 greeting offers GSSAPI (`05 02 00 01`, or `05 03 00 01 02` with a credential) because the reference build does; GSSAPI itself is not implemented, and a proxy that picks it fails with exit 97 and the SSPI message the reference build printed (`SSPI error: InitializeSecurityContext failed: SEC_E_TARGET_UNKNOWN (0x80090303) - The specified target is unknown or unreachable`).
  2. SOCKS4 sends the first IPv4 address the resolver (or `--resolve`) returns; with none, exit 97 `SOCKS4 connection to <first address> not supported` (measured only for an IPv6 literal).
  3. SOCKS5 sends the first resolved address, IPv4 or IPv6.
  4. A host is an address literal only when `IPAddress.TryParse` accepts it and, for IPv4, it prints back identically; anything else (such as `1`) is a name.
  5. User names, passwords and host names are sent as UTF-8.
  6. An exception while the handshake is sent or read disposes the proxy connection and propagates, as the HTTP CONNECT path does (ADR-0023).

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions` holds an ADR recording decisions 1 to 6 above with their reasons, marked "Decided by Claude under Stewart's delegation", and its row is in the index in `Documentation/Planning/Decisions/README.md`.
- [ ] `Socks5Handshake`'s remarks and `Curl.Networking.UnitLibrary/CLAUDE.md` cite the ADR by number (this adds `Curl.Networking.UnitLibrary` to `touches`).

## Notes

## Log

- 2026-09-27: Created.
