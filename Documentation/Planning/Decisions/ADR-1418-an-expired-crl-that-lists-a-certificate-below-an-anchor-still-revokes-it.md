# ADR-1418: An expired CRL that lists a certificate below an anchor still revokes it

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6009, fitness finding STD-216)

## Context

Below a non-self-signed `--cacert` or `--proxy-cacert` anchor Curl reads each certificate's CRL
itself (BL-5384). It discarded an expired base list, so a certificate listed on one had an unknown
revocation status, which `--ssl-revoke-best-effort` tolerates: the transfer succeeded where curl
fails. Delta lists already revoke when expired (STD-189).

curl 8.21.0 (Schannel, Windows) and Curl were run on 2026-11-26 against loopback TLS and CRL
servers, with and without `--ssl-revoke-best-effort`, with a CRL whose `nextUpdate` was three
days past:

| Distribution points | curl's answer, which Curl now matches |
| --- | --- |
| One: expired list naming the leaf | exit 60, revoked, both ways |
| One: expired list not naming the leaf | exit 0 with best effort; exit 60, status unknown, without |
| Expired list naming the leaf, then a current list not naming it | exit 0, both ways |
| Expired list naming the leaf, then a 404 | exit 60, revoked, both ways |

## Decision

An expired list that is otherwise the certificate's (its issuer's name and signature, in scope) does
not end the search: later distribution points are still asked, and the first unexpired list answers
alone. When no unexpired list is found and any such expired list names the certificate, it is
revoked; otherwise its status is unknown.

## Consequences

`--ssl-revoke-best-effort` no longer accepts a certificate that an expired list names, unless a
later distribution point serves a current list.
