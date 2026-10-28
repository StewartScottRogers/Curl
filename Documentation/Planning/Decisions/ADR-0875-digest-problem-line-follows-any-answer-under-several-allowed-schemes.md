# ADR-0875: The Digest problem line follows any answer sent under several allowed schemes

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3307
- Decided by Claude under Stewart's delegation.

## Context

libcurl's `Curl_http_input_auth` passes every Digest challenge to `Curl_input_digest`, even when
Digest is not the picked scheme. `Curl_auth_decode_digest_http_message` refuses a challenge
without `stale=true` when the transfer's Digest state already holds a nonce, and curl then writes
`* Digest authentication problem, ignoring.` before the header. So the line depends on whether
an earlier head of the transfer offered Digest, not on which scheme the request sent.

Real curl 8.22.0 shows it in the differential case auth-all-challenges-v
(`-sv --anyauth -u u:p` against a 401 offering Negotiate, NTLM, Digest and Basic): the second
401, answering the Negotiate retry, carries the line between `NTLM` and `Digest` (BL-3282 Notes).
`HttpAuthProblemLines` wrote it only when the request had sent a Digest answer.

## Decision

`HttpAuthProblemLines` takes libcurl's Digest state as holding a nonce when the request sent a
Digest answer, as before, or when it sent any answer while the request allows more than one
scheme (`--anyauth`, or several of `--basic`, `--digest`, `--ntlm`, `--negotiate`). With several
schemes libcurl sends its first request without an answer and picks one from a 401's
challenges, so an answer always follows a head that was read.

The rule stays inside the per-head reader rather than carrying a Digest state across heads in
`HttpProtocolHandler`: it is the simplest change that matches curl on every measured exchange.

## Consequences

- Curl's `-v` output for auth-all-challenges-v matches curl 8.22.0's on the Digest line.
- A transfer under several schemes whose first 401 offered no Digest but whose later 401 does
  would print the line where curl would not; no case or exchange is known to do this. Should one
  appear, the Digest state moves to the handler as a flag kept across the transfer's heads.
