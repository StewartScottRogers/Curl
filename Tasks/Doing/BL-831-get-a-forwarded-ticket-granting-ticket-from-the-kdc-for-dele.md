---
id: BL-831
title: Get a forwarded ticket-granting ticket from the KDC for --delegation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-691]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-831 — Get a forwarded ticket-granting ticket from the KDC for --delegation

## Goal

`KerberosKdcClient` gets a forwarded ticket-granting ticket (a TGS-REQ for `krbtgt/REALM@REALM` with the `forwarded` KDC option, as MIT's `krb5_fwd_tgt_creds` does) from a forwardable TGT, so `KerberosGssContextOptions.ForwardedTicketGrantingTicket` can be filled for `--delegation`.

## Context

- BL-691 built `KerberosGssContext`, which delegates only when the caller supplies a forwarded TGT (ADR-0171); without one it drops the delegation flag, as MIT does for a TGT that is not forwardable.
- RFC 4120 section 2.6 and 3.3 (`forwarded` option, bit 2 of `KDCOptions`; the TGT must carry `forwardable`). MIT asks for no addresses (`noaddresses`) by default.
- Consumer: BL-631 (`--delegation` on the hand-built route), composed by BL-527.

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` with `FakeKdc` show a forwardable TGT turned into a forwarded one by a TGS-REQ carrying the `forwarded` option and the TGT's realm, and a TGT without `forwardable` refused with a typed `KerberosKdcException` and no request sent.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-691.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
