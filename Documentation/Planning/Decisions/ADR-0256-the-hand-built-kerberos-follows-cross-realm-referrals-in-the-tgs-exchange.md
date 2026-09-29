# ADR-0256 — The hand-built Kerberos follows cross-realm referrals in the TGS exchange

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-826.
Amends ADR-0168, which refused every TGS-REP naming a server other than the one asked for.

## Context

A service in another realm (an Active Directory forest trust, an MIT cross-realm setup)
is reached by asking the client's own KDC, which answers not with the service's ticket but
with a cross-realm ticket-granting ticket `krbtgt/OTHER@REALM`: a referral (RFC 6806
section 8, RFC 4120 section 1.2). MIT's `krb5_get_credentials` (`get_creds.c`,
`gc_via_tkt.c`) sets `canonicalize`, follows each referral with the ticket it was given,
and gives up after `KRB5_REFERRAL_MAXHOPS` (10) referrals.

## Decision

`KerberosKdcClient.GetTicketFromTicketGrantingServiceAsync` loops over TGS exchanges:

- Every TGS-REQ sets `canonicalize` (`KerberosKdcOptions.Canonicalize`), as MIT's does.
- A TGS-REP whose server is the one asked for (realm and components) ends the loop.
- A TGS-REP from realm R's KDC naming `krbtgt/OTHER@R`, OTHER not R, is a referral: the
  next request goes to OTHER's KDCs with that ticket and its session key. When the realm
  asked for was R itself (the caller guessed the service's realm, the MIT referral case),
  the service's realm becomes OTHER; when it was another realm (a known target reached
  through intermediate realms), it stays.
- Anything else, including `krbtgt/R@R` or a `krbtgt` named by another realm than the
  KDC's, is `UnexpectedReply` as before.
- After `KerberosKdcClient.MaximumReferralHops` (10, MIT's limit) referrals, the eleventh
  is `KerberosKdcError.ReferralLimitExceeded`. A referral loop ends there; like MIT's hop
  count, no realm-seen set is kept.
- Cross-realm tickets got on the way are disposed (their session keys zeroed); the
  caller's ticket-granting ticket stays the caller's. None is written to the cache, as
  ADR-0168 writes nothing back.

## Consequences

- `KerberosKdcError` gains `ReferralLimitExceeded`; callers that switch on it treat it as
  any other failure to get a ticket.
- `capaths` from `krb5.conf` are not read: the path is the one the KDCs refer along.
- Tests drive the referrals through `FakeReferralKdcs` in `Curl.Kerberos.UnitTests`, KDCs
  of several realms that issue per-ticket session keys and check each request is
  authenticated in its ticket-granting ticket's key.
