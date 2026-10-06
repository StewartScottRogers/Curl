---
id: BL-826
title: Follow Kerberos cross-realm referrals in the TGS exchange
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-690]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-826 — Follow Kerberos cross-realm referrals in the TGS exchange

## Goal

`KerberosKdcClient` gets a service ticket in another realm by following the KDC's cross-realm referrals: a TGS-REP carrying `krbtgt/OTHER@REALM` is used to ask OTHER's KDCs, as MIT's `krb5_get_credentials` does with `canonicalize`.

## Context

- Follow-up from BL-690.
- BL-690 and ADR-0168 refuse a TGS-REP naming a server other than the one asked for (`UnexpectedReply`).
- RFC 6806 section 8 (referrals), MIT `src/lib/krb5/krb/get_creds.c` and `gc_via_tkt.c`; a limit on referral hops as MIT's (KRB5_REFERRAL_MAXHOPS).

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` get a service ticket in a second realm through a fake KDC that answers with one referral, and fail with a typed error on a referral loop past the hop limit.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan (ADR-0200, decided by Claude under Stewart's delegation): `GetTicketFromTicketGrantingServiceAsync` loops over TGS exchanges, every TGS-REQ with `canonicalize`. A reply naming the server asked for ends it; one from realm R's KDC naming `krbtgt/OTHER@R` (OTHER not R) is followed with that ticket to OTHER's KDCs; anything else stays `UnexpectedReply`. If the realm asked for was R (the MIT referral case) the service's realm becomes OTHER; otherwise (a known realm reached through intermediates) it stays. The eleventh referral is the new `KerberosKdcError.ReferralLimitExceeded` (`MaximumReferralHops` = 10, MIT's `KRB5_REFERRAL_MAXHOPS`). Intermediate tickets are disposed; the caller's is not.
- No realm-seen loop detection: the hop limit is what the acceptance criterion names and what MIT's counter does; a loop ends at the limit.
- Tests: `FakeReferralKdcs` (KDCs of several realms, per-ticket session keys, each request checked in its TGT's key) and `KerberosKdcClientTests.Referrals.cs`, a partial of `KerberosKdcClientTests` so there stays one test class per production class.
- Touches widened to `Documentation/Planning/Decisions` for ADR-0200 (a new file plus a one-phrase pointer in ADR-0168 and a README row); no task in Doing names it.
- Verified: `dotnet build Curl.slnx -warnaserror` clean; Kerberos 487/487; `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` 100% line, 100% branch, 0 failing members (worst CRAP 10). In the full fast run `Curl.Networking.UnitTests` failed 3 under load once and passed 1505/1505 on both reruns; not touched by this task.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. KerberosKdcClient follows krbtgt/OTHER@REALM referrals to OTHER's KDCs, up to 10 hops, then ReferralLimitExceeded
