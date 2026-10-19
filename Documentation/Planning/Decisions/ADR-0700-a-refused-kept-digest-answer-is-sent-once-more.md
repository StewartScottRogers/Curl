# ADR-0700: A refused kept Digest answer is sent once more after a perform that picked Digest

- Status: Accepted
- Date: 2026-10-18
- Task: BL-2745
- Decided by Claude under Stewart's delegation.

## Context

libcurl 8.22.0 keeps `data->state.authhost.avail` on the easy handle. A perform whose Digest answer
is refused ends on a 401 whose `WWW-Authenticate: Digest` set `avail` and stored the new nonce, with
`authproblem` stopping `pickoneauth` from clearing `avail` again. On the next perform with Digest
allowed, the first request answers that nonce up front; when it draws a 401, `Curl_http_input_auth`
finds Digest already in `avail`, writes `Ignoring duplicate digest auth header.` and leaves the old
nonce in place, and `Curl_http_auth_act` retries. The retry sends the same answer, and its 401 is
parsed as a refusal and ends the transfer (upstream test2027, lib2023's second round). After a
perform that picked another scheme, `pickoneauth` cleared `avail`, so the first 401 ends the transfer
(upstream test2024).

## Decision

`HttpProtocolHandler` marks a plan whose first `Authorization` answers the handle's kept challenges
(`HttpRequestPlan.AuthorizationAnswersKeptDigestChallenges`). A 401 to that request is answered by
sending the same value again through `IHttpAuthenticator.RepeatAuthorization` when the previous
perform's server picked Digest (`HttpRequestOptions.AuthSchemePicked`, kept on the handle by
ADR-0699) and the 401 offers Digest. Every retry's plan clears the mark, so the next 401 ends the
transfer as before. The `Ignoring duplicate digest auth header.` verbose line is not written.

## Consequences

Upstream case 2027 passes and holds the ratchet; 2024 still passes. The curl tool's runner passes no
kept challenges, so the tool is unchanged.
