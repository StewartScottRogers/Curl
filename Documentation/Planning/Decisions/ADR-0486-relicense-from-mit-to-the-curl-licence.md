# ADR-0486 — Relicense from MIT to the curl licence

- **Status:** Accepted
- **Date:** 2026-10-13
- **Supersedes:** [ADR-0012](ADR-0012-relicense-from-gpl-3-0-to-mit.md)

Decided by Stewart on 2026-10-13: "Make sure the same open source license in the original
curl is used in this one."

## Context

ADR-0012 moved the repository from GPL-3.0 to MIT, because curl's own licence is a
permissive MIT-style licence and a drop-in replacement has to go everywhere curl goes. MIT
met that goal, but it is still a different licence from curl's. A user or packager who has
already cleared curl's licence (<https://curl.se/docs/copyright.html>, SPDX identifier
`curl`) would have to clear a second one before shipping Curl in its place.

## Decision

1. `LICENSE.txt` holds the curl licence word for word, as curl's `COPYING` file at release
   tag `curl-8_22_0` has it. The text is the one vendored in
   `Curl.Conformance.UnitTests/CurlTestCases/Original/COPYING`. Only the copyright line names this
   project's holder: "Copyright (c) 2026, Stewart Scott Rogers, and many contributors."
2. The SPDX identifier of the repository is `curl`.
3. The vendored upstream test data keeps its own `COPYING` beside it, unchanged, as before.

## Consequences

- Curl ships under exactly the terms curl ships under, so anyone who can use curl can use
  Curl on the same terms, with no second licence to review.
- Both licences are permissive, so nothing already granted under MIT is taken back. Copies
  taken while the repository was MIT stay under MIT.
- GitHub's licence detection may list the repository as "Other" rather than naming the curl
  licence; the text in `LICENSE.txt` is what counts.
- The clean-room rule of the product overview (build from RFCs, the man page and observable
  behaviour, never by translating curl's C) stays. Sharing curl's licence does not make
  copying curl's code a goal.
