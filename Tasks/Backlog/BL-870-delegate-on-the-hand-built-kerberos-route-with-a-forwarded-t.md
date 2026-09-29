---
id: BL-870
title: Delegate on the hand-built Kerberos route with a forwarded ticket-granting ticket for --delegation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-631]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-870 — Delegate on the hand-built Kerberos route with a forwarded ticket-granting ticket for --delegation

## Goal

When the hand-built Negotiate or Kerberos context (ADR-0142's route K) answers and `SecurityContextRequest.Delegation` is `Always`, or `Policy` with an ok-as-delegate service ticket, the initial token carries the delegation flag and a `KRB-CRED` with a forwarded ticket-granting ticket, as MIT's `gss_init_sec_context` does for curl's GSS-API build.

## Context

- ADR-0188 (BL-631) decision 5: `HandBuiltKerberosSecurityContext.InitialStepAsync` builds `new KerberosGssContextOptions()` and ignores `request.Delegation`.
- `Curl.Kerberos`'s `KerberosGssContext` already honours `KerberosGssContextOptions.Delegation` and `ForwardedTicketGrantingTicket`; what is missing is getting that forwarded TGT (a TGS-REQ for `krbtgt/REALM` with the `forwarded` KDC option, from a forwardable TGT) in `KerberosServiceTicketSource` or a sibling, and mapping `SecurityDelegation` to `KerberosDelegation`.
- MIT sends no delegation when the TGT is not forwardable; keep that.

## Acceptance criteria

- [ ] `Curl.Authentication.UnitTests` pin, against `FakeKdc` and `FakeGssAcceptor`, that `Always` sends the delegation flag and a `KRB-CRED`, `Policy` sends it only for an ok-as-delegate ticket, and `None` or a non-forwardable TGT sends neither.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
