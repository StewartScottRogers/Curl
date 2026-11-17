# ADR-1279 — SASL EXTERNAL without a user name stays an empty line, as curl 8.22.0 sends it

- Status: Accepted
- Date: 2026-11-16
- Task: BL-5293
- Decided by Claude under Stewart's delegation.

## Context

Surl's Phase 7 measurement found that Curl answers the server's empty challenge to
`AUTH=EXTERNAL` with no user name by an empty line, where its oracle, curl 8.21.0, sends `=`
(upstream tests 838, 884, 943), and asked for `=`.

The platform's curl on this machine is curl 8.22.0 (Schannel), and Curl's reference build is
8.22.0 (ADR-0928). Curl 8.22.0 changed these three cases (Gap finding GF-0038 lists
test838, test884 and test943 as "changed in 8.22.0"): it sends an empty line. The port
deliberately does so (`ImapAuthentication`, `Pop3Login`, `SmtpSaslAuthentication`
`EncodeDeferredInitialResponse`, with tests pinning each case), and the ratchet's upstream
test data is 8.22.0's, which is why 838, 884 and 943 are listed as passing in
`PassingUpstreamCases.txt` while Surl, playing 8.21.0's data, sees them fail.

## Decision

Curl keeps the empty line. A drop-in replacement matches the curl the user has, and the
measured reference is 8.22.0. The mismatch is one of oracle version, not a defect. A response
that is an empty *initial response* sent on the AUTH line stays `=` (test840, 886, 945).

## Consequences

Surl's three cases fail against Curl until its oracle moves to 8.22.0's test data.
