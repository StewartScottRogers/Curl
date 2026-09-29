---
id: BL-826
title: Follow Kerberos cross-realm referrals in the TGS exchange
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-690]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-826 — Follow Kerberos cross-realm referrals in the TGS exchange

## Goal

`KerberosKdcClient` gets a service ticket in another realm by following the KDC's cross-realm referrals: a TGS-REP carrying `krbtgt/OTHER@REALM` is used to ask OTHER's KDCs, as MIT's `krb5_get_credentials` does with `canonicalize`.

## Context

- Follow-up from BL-690.
- BL-690 and ADR-0168 refuse a TGS-REP naming a server other than the one asked for (`UnexpectedReply`).
- RFC 6806 section 8 (referrals), MIT `src/lib/krb5/krb/get_creds.c` and `gc_via_tkt.c`; a limit on referral hops as MIT's (KRB5_REFERRAL_MAXHOPS).

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` get a service ticket in a second realm through a fake KDC that answers with one referral, and fail with a typed error on a referral loop past the hop limit.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
