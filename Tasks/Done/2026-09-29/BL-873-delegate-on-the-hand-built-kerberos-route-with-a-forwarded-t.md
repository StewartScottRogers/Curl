---
id: BL-873
title: Delegate on the hand-built Kerberos route with a forwarded ticket-granting ticket for --delegation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-631]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions/ADR-0188-service-names-and-delegation-reach-every-security-context-and-sspi-never-delegates.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-873 — Delegate on the hand-built Kerberos route with a forwarded ticket-granting ticket for --delegation

## Goal

When the hand-built Negotiate or Kerberos context (ADR-0142's route K) answers and `SecurityContextRequest.Delegation` is `Always`, or `Policy` with an ok-as-delegate service ticket, the initial token carries the delegation flag and a `KRB-CRED` with a forwarded ticket-granting ticket, as MIT's `gss_init_sec_context` does for curl's GSS-API build.

## Context

- ADR-0188 (BL-631) decision 5: `HandBuiltKerberosSecurityContext.InitialStepAsync` builds `new KerberosGssContextOptions()` and ignores `request.Delegation`.
- `Curl.Kerberos`'s `KerberosGssContext` already honours `KerberosGssContextOptions.Delegation` and `ForwardedTicketGrantingTicket`; what is missing is getting that forwarded TGT (a TGS-REQ for `krbtgt/REALM` with the `forwarded` KDC option, from a forwardable TGT) in `KerberosServiceTicketSource` or a sibling, and mapping `SecurityDelegation` to `KerberosDelegation`.
- MIT sends no delegation when the TGT is not forwardable; keep that.

## Acceptance criteria

- [x] `Curl.Authentication.UnitTests` pin, against `FakeKdc` and `FakeGssAcceptor`, that `Always` sends the delegation flag and a `KRB-CRED`, `Policy` sends it only for an ok-as-delegate ticket, and `None` or a non-forwardable TGT sends neither.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- `KerberosKdcClient` gained `GetForwardedTicketGrantingTicketAsync(CredentialCache, ...)`, which forwards the cache's live ticket-granting ticket (sharing `CachedTicketGrantingTicket` with the service-ticket path); `KerberosServiceTicketSource.GetForwardedTicketGrantingTicketAsync` reads `krb5.conf` and the cache and calls it.
- The forwarded ticket is fetched only when the context will delegate (`always`, or `policy` with an ok-as-delegate service ticket), so `none` and plain `policy` spend no extra KDC exchange.
- Any `KerberosKdcException`, `KerberosFileException`, `KerberosConfigurationException` or `KerberosCryptographyException` while forwarding gives no delegation rather than a failed context, as MIT drops `GSS_C_DELEG_FLAG` when `krb5_fwd_tgt_creds` fails (ADR-0210). No new ADR: ADR-0210 already decided the forwarding; ADR-0188 decision 5 is updated to say route K now delegates.
- Added ADR-0188 to `touches` to correct its decision 5, which said route K does not delegate; no task in Doing touches it.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --delegation always, and policy with an ok-as-delegate ticket, forward the TGT on the hand-built Kerberos route; build clean, fast tests green, both libraries 100% covered.
