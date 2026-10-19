# ADR-0709 — Digest stays available after a 401 that sets curl's authproblem

- Status: Accepted
- Date: 2026-10-19
- Task: BL-2823
- Amends: ADR-0700
- Decided by Claude under Stewart's delegation.

## Context

libcurl 8.22.0 keeps `authhost.avail` on the easy handle across performs. `Curl_http_input_auth`
adds `CURLAUTH_DIGEST` to it for any `Digest` challenge in a 401, and sets
`data->state.authproblem` ("Authentication problem. Ignoring this.") when a scheme's input
function fails. `Curl_http_auth_act` returns early while `authproblem` is set, so `pickoneauth`,
which clears `avail`, never runs, and Digest stays available into the next perform. There a 401
offering Digest is "Ignoring duplicate digest auth header." and the kept answer is sent once more.

BL-2820 modelled one source of `authproblem`: a non-stale Digest challenge after the transfer
already held a nonce (`Curl_input_digest` fails). Upstream case 2030's transfer 300 shows a second:
an NTLM Type 3 answered by a 401 with a bare `NTLM` header, which `Curl_input_ntlm` fails as
"NTLM handshake rejected" (and a bare `NTLM` after Type 1 fails as an internal handshake
failure). That 401 also offers `Digest`, so Digest stays available, and transfer 400 sends its
request twice. ADR-0700 keyed that resend on the previous perform's picked scheme being Digest,
which only covers case 2027.

## Decision

`HttpProtocolHandler` reports `TransferReport.DigestStaysAvailable` when the final response is a
401 offering Digest and that 401 sets `authproblem`: a non-stale Digest challenge while a Digest
nonce was held, or a bare `NTLM` challenge to a request sent with an NTLM `Authorization` value.
The order of the headers does not matter, since Digest joins `avail` whichever comes first.
`RepeatsKeptDigestAnswer` now keys on `HttpRequestOptions.DigestStaysAvailable`, the carried
state, instead of `AuthSchemePicked == Digest`, so cases 2024, 2027 and 2030 all follow from the
one model.

## Consequences

- Upstream cases 2023 to 2031 pass through the case runner.
- Other `authproblem` sources (Negotiate, Basic and Bearer refusals) are not modelled yet; none of
  the lib2023 cases reaches them.
