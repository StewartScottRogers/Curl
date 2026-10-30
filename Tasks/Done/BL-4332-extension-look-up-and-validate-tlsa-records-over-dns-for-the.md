---
id: BL-4332
title: Extension: Look up and validate TLSA records over DNS for the Dane extension
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4333, BL-4335]
touches: [Curl.Extension.Dane.UnitLibrary/Lookup, Curl.Extension.Dane.UnitTests/Lookup]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4332 — Extension: Look up and validate TLSA records over DNS for the Dane extension

## Goal

`Curl.Extension.Dane.UnitLibrary/Lookup/` asks a DNS server for `_<port>._tcp.<host>` TLSA and returns the RRset as secure, insecure, bogus or absent after walking the DNSSEC chain of trust from the root.

## Context

Part of the split of BL-4151 (DNSSEC and DANE TLSA extension). Built on BL-4333's `Wire/` codec and BL-4335's `Dnssec/` verifiers. It takes an `IDatagramConnector` (from `Curl.Protocol.Abstractions`; production uses `Curl.Networking.UnitLibrary`'s `UdpDatagramConnector`) and an `IConnector` for TCP fallback when a reply is truncated (RFC 7766 section 5 / RFC 1035 section 4.2.2, two-octet length prefix), the server's address, and a `TimeProvider` for timeouts. Queries go to a recursive resolver with DO and CD set (RFC 4035 section 3.2.2, so the client validates itself); a reply's AD bit is never trusted. The walk: TLSA name per RFC 6698 section 3; follow CNAMEs (RFC 7671 section 7); for each zone from the root down fetch DNSKEY and DS and verify them with BL-4335's code; the TLSA RRSIG against the zone's DNSKEY. Absence: NOERROR/NODATA or NXDOMAIN proved with NSEC (RFC 4035 section 5.4) or NSEC3 (RFC 5155 section 8) gives absent-secure; an insecure delegation (proved absence of DS) gives insecure; anything failing verification gives bogus. Record in an ADR marked "Decided by Claude under Stewart's delegation" anything left out (for example NSEC3 opt-out handling) and why.

## Acceptance criteria

- [x] Unit tests drive the lookup over fake datagram and stream connectors replaying recorded responses for a signed TLSA RRset (secure), an unsigned zone below a proved insecure delegation (insecure), a tampered signature (bogus) and an NSEC- and an NSEC3-proved absence, each named after its RFC section, with no network.
- [x] A truncated UDP reply is retried over TCP and a timeout gives a lookup failure; unit tests with a fake `TimeProvider`.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records what the lookup leaves out and why.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.Dane.UnitLibrary`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4151's run on 2026-10-29 when it split that task for size. Document the folder in XML comments; BL-4151 adds it to the library's CLAUDE.md.
- Plan as built (ADR-1137): `DnssecQueryTransport` (UDP with DO+CD, TCP retry on TC with a length prefix, one
  `TimeProvider` timeout, null on any failure); `TlsaLookup` walks DS top down from the trust anchors (a constructor
  parameter, `RootTrustAnchors.DelegationSigners` in production) through every ancestor, anchors each zone's DNSKEY
  RRset by DS, then judges the TLSA RRset, a CNAME (up to eight) or the NSEC/NSEC3 denial (`DenialOfExistence`,
  `Nsec3Hash`, `LookupNames` for canonical order). Results: Secure, Insecure, Bogus, Absent, Failed.
- Default taken: an unproved NODATA for DS keeps the name in the parent's zone (safe, since a hidden delegation's
  signatures then fail as Bogus); only a proved NS-without-DS gives Insecure. Insecure results carry no records.
- Tests generate a signed ". > test. > example.test." tree with Ed25519 keys and a test trust anchor, encode the
  replies to wire bytes and replay them through `FakeDnsServer` (fake `IDatagramConnector` and `IConnector`), with
  `ManualTimeProvider` firing the timeout. Nothing depends on the platform (no paths, OS errors or certificates).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.Dane.UnitLibrary` 100% line, 100% branch, 0 failing
  members, worst CRAP 10. `dotnet build` clean; fast tests green (Dane.UnitTests 162 passed).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 6bb14aa5 fab06f69 65981ee3 6db41805 3d836d5e.
- 2026-10-29: Doing -> Done. TLSA lookup over DNS with DNSSEC chain validation from the root: secure, insecure, bogus, absent and failed
