# ADR-0210 — The hand-built Kerberos forwards a ticket-granting ticket as MIT's krb5_fwd_tgt_creds does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-831.
Builds on ADR-0171, which sends a KRB-CRED for `--delegation` only when the caller
supplies a forwarded ticket-granting ticket.

## Context

`--delegation` needs a forwarded ticket-granting ticket for
`KerberosGssContextOptions.ForwardedTicketGrantingTicket`. MIT's `gss_init_sec_context`
gets one with `krb5_fwd_tgt_creds(..., forwardable = 1, ...)`
(`src/lib/krb5/krb/fwd_tgt.c`): a TGS-REQ for `krbtgt/REALM@REALM` with the ticket's own
flags turned into options by `flags2options` (`forwardable`, `proxiable`, `renewable`),
`forwarded` added, and, for an addressless ticket (MIT's `noaddresses = true` default),
no addresses. RFC 4120 section 2.6 lets a KDC issue a forwarded ticket only from one
with `forwardable`; for any other MIT's KDC answers `KDC_ERR_BADOPTION`, and
`gss_init_sec_context` then drops the delegation flag.

## Decision

`KerberosKdcClient.GetForwardedTicketGrantingTicketAsync` sends one TGS-REQ to the KDCs
of the ticket's realm for `krbtgt/REALM@REALM` with `forwarded`, `forwardable` and the
ticket's own `proxiable` and `renewable`; `till` is the ticket's end time and, with
`renewable`, `rtime` its renew-until time; no addresses and no `canonicalize`. The reply
must name `krbtgt/REALM@REALM`, else `KerberosKdcError.UnexpectedReply`; no referral is
followed, since the ticket is for the client's own realm.

A ticket without `forwardable` fails with `KerberosKdcError.TicketNotForwardable` before
anything is sent, rather than asking the KDC to refuse it: the answer is known, and the
caller (BL-631) treats either failure the same way, sending no delegation, as MIT does.

## Consequences

- BL-631 can fill `ForwardedTicketGrantingTicket` for `--delegation` on the hand-built
  route and fall back to no delegation on any `KerberosKdcException`.
- A ticket bound to addresses is still forwarded addressless; MIT would ask for the target
  host's addresses, which needs a resolver this library does not take. Curl's tickets are
  addressless by MIT's default, so this is not expected to differ in practice.
