---
id: BL-827
title: Reach a Kerberos KDC through an MS-KKDCP HTTPS proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-690]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-827 — Reach a Kerberos KDC through an MS-KKDCP HTTPS proxy

## Goal

`KerberosKdcSender` reaches an `https://` KDC entry through an MS-KKDCP proxy (`KDC-PROXY-MESSAGE` over HTTPS POST) instead of skipping it.

## Context

- Follow-up from BL-690.
- ADR-0168 skips `https://` KDCs; `KerberosKdcLocator` already parses them (ADR-0160).
- MS-KKDCP; MIT `src/lib/krb5/os/sendto_kdc.c` (HTTPS transport). The HTTPS exchange needs its own injected seam, not a socket in this library.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` send an AS-REQ to an `https://` KDC through a fake proxy seam and read its reply, with the `KDC-PROXY-MESSAGE` bytes pinned against MIT's encoding.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Decided by Claude under Stewart's delegation:

- **Separate seam.** New public `IKerberosKdcProxyTransport.PostAsync(host, port, path, body)`: one HTTPS POST, returns the reply body, `IOException` on failure. It is not a new member of `IKerberosKdcTransport`, because that would break `Curl.Networking`'s `KerberosKdcSocketTransport` and `Curl.Console.UnitTests`' fake, both outside `touches`. `KerberosKdcSender` and `KerberosKdcClient` take it as an optional last argument; without it `https://` KDCs are skipped as before (ADR-0168), so production behaviour is unchanged until BL-882 wires one in.
- **Encoding as MIT's.** `KerberosKdcProxyMessage` follows MS-KKDCP 2.2.2 as MIT `asn1_k5.c` (`kkdcp_message`) encodes it for `sendto_kdc.c`: explicit tags, `kerb-message [0]` = the request framed as over TCP (4-byte big-endian length + message), `target-domain [1]` = the realm as a GeneralString, no `dclocator-hint`. Decoding ignores a `dclocator-hint` and refuses a `kerb-message` whose prefix is missing or does not match (`Malformed`), as MIT does. Pinned in `KerberosKdcProxyMessageTests` (bytes built by hand from the ASN.1; no MIT build on this machine to record from).
- **Bad proxy reply tries the next KDC.** A reply that is not a `KDC-PROXY-MESSAGE` becomes an `IOException`, so the realm's next KDC is tried, as MIT's `service_https_read` kills the connection and moves on.
- **No ADR written here.** BL-594 (in Doing) touches `Documentation/Planning/Decisions`, so this lane stayed out; BL-883 records the ADR and amends ADR-0168.
- Follow-ups filed: BL-882 (production HTTPS transport in `Curl.Networking` + wiring in `Curl.Console`), BL-883 (the ADR).
- Tests: `Curl.Kerberos.UnitTests` 498 passed; new: `KerberosKdcProxyMessageTests` (encode pinned, decode, malformed rows), 3 `KerberosKdcSenderTests`, 1 `KerberosKdcClientTests` end-to-end AS+TGS through the proxy. Solution fast tests all green.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. KerberosKdcSender reaches https:// KDCs through an injected MS-KKDCP proxy seam with MIT-encoded KDC-PROXY-MESSAGEs
