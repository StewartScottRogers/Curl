# ADR-0405 — A CRL is expired from its nextUpdate moment on

- **Status:** Accepted
- **Date:** 2026-10-03
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

`CertificateRevocationListFile.DateRefusal` refused a `--crlfile` list as expired only when its
`nextUpdate` was strictly before the moment checked (`list.NextUpdate < now`). The audit office's
quality auditor found that changing it to `<=` failed no test (AF-0029, BL-1371): the boundary
was never pinned, so which way it went was an accident.

The OpenSSL build of curl is the one that reads `--crlfile` (ADR-0197). OpenSSL's `check_crl_time`
calls `X509_cmp_time(nextUpdate, now)` and refuses with `X509_V_ERR_CRL_HAS_EXPIRED` when the result
is below zero, and `X509_cmp_time` is documented to return -1 when the time is "earlier than, or
equal to" the moment compared. The same function makes a `thisUpdate` equal to the moment valid,
which `DateRefusal` already did (`ThisUpdate > now`).

## Decision

A list whose `nextUpdate` equals the moment checked has expired: `list.NextUpdate <= now`.
`CertificateRevocationListFileTests` pins both sides of the boundary, the expiry moment itself
(`Refusal_AtTheListsExpiryMoment_HasExpired`) and one second before it
(`Refusal_OneSecondBeforeTheListsExpiry_Accepts`).

## Consequences

The only change a user could see is at the exact second a list expires, where Curl now refuses as
OpenSSL does (exit 60, verify result 12). The boundary is tested, so a mutant flipping it either way
fails a test.

## Alternatives considered

- **Keep `<` and add a test for it.** Kills the mutant just as well, but pins the opposite of what
  OpenSSL does at the boundary, and Curl matches the platform's curl.
