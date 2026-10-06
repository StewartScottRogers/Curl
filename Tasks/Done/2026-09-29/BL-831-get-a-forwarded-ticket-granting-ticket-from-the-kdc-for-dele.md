---
id: BL-831
title: Get a forwarded ticket-granting ticket from the KDC for --delegation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-691]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions/ADR-0210-the-hand-built-kerberos-forwards-a-ticket-granting-ticket-as-mit-s-krb5-fwd-tgt-creds-does.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-831 — Get a forwarded ticket-granting ticket from the KDC for --delegation

## Goal

`KerberosKdcClient` gets a forwarded ticket-granting ticket (a TGS-REQ for `krbtgt/REALM@REALM` with the `forwarded` KDC option, as MIT's `krb5_fwd_tgt_creds` does) from a forwardable TGT, so `KerberosGssContextOptions.ForwardedTicketGrantingTicket` can be filled for `--delegation`.

## Context

- BL-691 built `KerberosGssContext`, which delegates only when the caller supplies a forwarded TGT (ADR-0171); without one it drops the delegation flag, as MIT does for a TGT that is not forwardable.
- RFC 4120 section 2.6 and 3.3 (`forwarded` option, bit 2 of `KDCOptions`; the TGT must carry `forwardable`). MIT asks for no addresses (`noaddresses`) by default.
- Consumer: BL-631 (`--delegation` on the hand-built route), composed by BL-527.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` with `FakeKdc` show a forwardable TGT turned into a forwarded one by a TGS-REQ carrying the `forwarded` option and the TGT's realm, and a TGT without `forwardable` refused with a typed `KerberosKdcException` and no request sent.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-691.
- Delivered as `KerberosKdcClient.GetForwardedTicketGrantingTicketAsync(ticketGrantingTicket, ct)`:
  one TGS-REQ to the ticket's realm for `krbtgt/REALM@REALM` with `forwarded`, `forwardable`
  and the ticket's own `proxiable`/`renewable` (MIT's `flags2options` with GSS's
  `forwardable = 1`), `till` the ticket's end, `rtime` its renew-until with `renewable`,
  addressless, no `canonicalize`; the reply must name `krbtgt/REALM@REALM`. The existing TGS
  exchange took `options` and `expectedServer` parameters to share the code.
- Decision (ADR-0210): a ticket without `forwardable` fails as the new
  `KerberosKdcError.TicketNotForwardable` before sending, since the KDC's answer
  (`KDC_ERR_BADOPTION`) is known and the caller drops delegation either way.
- Touches widened to the new ADR file and the Decisions README index (for its row); no
  task in Doing names either.
- `FakeKdc` now also answers TGS-REQs for `krbtgt/EXAMPLE.TEST` and marks a reply to a
  `forwarded` request `forwarded` + `forwardable`.
- Verified: full `dotnet build Curl.slnx -warnaserror` clean, fast tests green (Kerberos
  578), `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` 100% line and branch,
  0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. KerberosKdcClient gets a forwarded ticket-granting ticket from a forwardable one for --delegation and refuses one that is not forwardable
