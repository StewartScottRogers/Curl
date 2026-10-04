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
completed: 2026-10-03
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

- [x] `AltSvcHeaderParser` (or its result) gives `UnknownPortNumber` for `h2=":abc"`, `h2="[::1]:99999"` and `h2="host:"`, `BadIpv6Hostname` for `h2="[::1:443"`, and `BadHostname` for a host one character over `AltSvcEntry.MaxHostLength`, each pinned by a test.
- [x] `AltSvcCache` returns outcomes in header order: for `h2="a.test:443", h2=":abc"` an added alternative then `UnknownPortNumber`, pinned by a test.
- [x] `HstsTransferPolicy` implements `IHstsStore`: `max-age=abc` on a name host returns `false` and stores nothing; on an IP-address host returns `true` and stores nothing; `max-age=60` returns `true` and stores the host; each pinned by a test.
- [x] `LearnFrom` stays until BL-1420 removes its caller, or is removed here if nothing calls it.
- [x] 100% line and branch coverage, complexity at most 10 (`Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`).

## Notes

- `AltSvcHeader` gained `SkipReason`; `AltSvcHeaderParser` sets it where curl writes a line: an unclosed, empty or overlong IPv6 literal is `BadIpv6Hostname`; a name host over `AltSvcEntry.MaxHostLength` (counted to the next `:` or the end) is `BadHostname`; an empty, non-numeric or out-of-range port, a missing `:` after `]`, or a name with no `:` at all is `UnknownPortNumber`. A missing `=`, opening or closing quote stops reading with no reason, as curl writes no such line (choice: those cases were not measured and curl's `altsvc.c` has no text for them).
- `AltSvcCache.ApplyHeader` now returns `IReadOnlyList<AltSvcHeaderOutcome>`: an `Adding` per entry added, then the `Skipping` reason. `Curl.Console`'s `AltSvcTransferCache` still ignores the return value until BL-1420 switches it.
- `HstsTransferPolicy` implements `IHstsStore`; `HstsCache.ApplyHeader` returns `bool` and gained an overload taking the receive time, so `max-age` counts from the `now` the caller passes. An `http` origin returns `true` and stores nothing (choice: curl only learns STS over https and writes no line otherwise).
- `LearnFrom` stays: `RedirectFollower` still calls it (BL-1420 removes the caller). It now applies each header through `StoreFromResponse`.
- Measure-CodeQuality (Curl.Core.UnitLibrary): 100% line, 100% branch, worst CRAP 10, 0 failing members.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. AltSvcHeaderParser reports why it skips an alternative, AltSvcCache returns outcomes in header order, HstsTransferPolicy is an IHstsStore
