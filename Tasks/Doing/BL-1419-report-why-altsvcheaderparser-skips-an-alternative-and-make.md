---
id: BL-1419
title: Report why AltSvcHeaderParser skips an alternative and make HstsTransferPolicy an IHstsStore (ADR-0409)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1418]
touches: [Curl.Core.UnitLibrary/AltSvc, Curl.Core.UnitLibrary/Hsts, Curl.Core.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1419 — Report why AltSvcHeaderParser skips an alternative and make HstsTransferPolicy an IHstsStore (ADR-0409)

## Goal

`Curl.Core.UnitLibrary` says why it skips an `Alt-Svc` alternative and learns one `Strict-Transport-Security` header at a time through `IHstsStore`, as ADR-0409 decides.

## Context

- ADR-0409; the contract comes from BL-1418.
- `Curl.Core.UnitLibrary/AltSvc/AltSvcHeaderParser.cs` stops at a bad host (`TryReadHost`, `TryReadUntil`) or port (`TryReadPort`) without a reason; `AltSvcCache` stores the parsed alternatives.
- curl 8.21.0 `lib/altsvc.c` lines 515-545: `Bad alt-svc hostname, ignoring.` (host over the length limit), `Bad alt-svc IPv6 hostname, ignoring.`, `Unknown alt-svc port number, ignoring.`. Measured (BL-1407): `h2=":abc"`, `h2="[::1]:99999"` and `h2="host:"` are `UnknownPortNumber`; `h2="[::1:443"` is `BadIpv6Hostname`.
- `Curl.Core.UnitLibrary/Hsts/HstsTransferPolicy.cs` `LearnFrom` learns from the report after the transfer; `HstsHeaderParser.Parse` returns `null` for an illegal header. curl writes no line for an IP-address host (measured, BL-1407).

## Acceptance criteria

- [ ] `AltSvcHeaderParser` (or its result) gives `UnknownPortNumber` for `h2=":abc"`, `h2="[::1]:99999"` and `h2="host:"`, `BadIpv6Hostname` for `h2="[::1:443"`, and `BadHostname` for a host one character over `AltSvcEntry.MaxHostLength`, each pinned by a test.
- [ ] `AltSvcCache` returns outcomes in header order: for `h2="a.test:443", h2=":abc"` an added alternative then `UnknownPortNumber`, pinned by a test.
- [ ] `HstsTransferPolicy` implements `IHstsStore`: `max-age=abc` on a name host returns `false` and stores nothing; on an IP-address host returns `true` and stores nothing; `max-age=60` returns `true` and stores the host; each pinned by a test.
- [ ] `LearnFrom` stays until BL-1420 removes its caller, or is removed here if nothing calls it.
- [ ] 100% line and branch coverage, complexity at most 10 (`Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`).

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
