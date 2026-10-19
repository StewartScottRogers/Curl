# ADR-0692: A Libcurl handle keeps its last 401's challenges for its next transfer's Digest answer

- Status: Accepted
- Date: 2026-10-18
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0's `Curl_http_input_auth` reads every `WWW-Authenticate: Digest` challenge into
`data->state.digest`, even when `CURLOPT_HTTPAUTH` does not allow Digest, and that state lives on the
easy handle across transfers. On the next transfer `Curl_http_output_auth` picks the one wanted
scheme, and when that is Digest, `output_auth_headers` answers the stored nonce before any new
challenge. Upstream case 2024 (`lib2023`) depends on it: request 100 goes with Basic and gets a 401
offering Digest `nonce="1"`; request 200, on the same handle under `CURLAUTH_DIGEST`, must send the
Digest `Authorization` built from that nonce. Curl's `DigestAuthenticator` is stateless (ADR-0014)
and answered only challenges of the current transfer, so request 200 went without one (BL-2747).

## Decision

1. `ManagedEasyTransfer` (the engine's easy handle, which `Libcurl` and `Curl.Api` handles wrap)
   keeps the `WWW-Authenticate` values of the 401 that ended its last perform
   (`ChallengesOfLast401`); any other outcome keeps none, and `Reset` forgets them, as
   `curl_easy_reset` cleans up the Digest state. `Duplicate` starts with none. They are answered
   only to the same `host:port` as the perform that kept them: curl sends no kept Digest answer to
   another host (upstream test1985, which went red when they were answered everywhere).
2. It hands them to its own `TransferContextFactory` (`KeptDigestChallenges`), which puts them on
   `HttpRequestOptions.KeptDigestChallenges`. The curl tool's runner (`ToolDispatch`) builds its own
   factory and passes none: the tool makes a fresh easy handle per URL.
3. `HttpProtocolHandler` answers them on the first request, through
   `IHttpAuthenticator.CreateAuthorizationAsync`, when there is a credential, Digest is the one
   scheme allowed (curl's `picked == CURLAUTH_DIGEST`), and no kept Digest value of an earlier
   redirect hop (`KeptDigestAuthorization`, BL-2252) is sent instead.

The authenticator stays stateless: the state is the handle's, carried in the request options, which
amends ADR-0014's no-state rule for this one case.

## Consequences

Case 2024's request 200 sends curl's Digest answer. A transfer that answered a Digest challenge and
ended 200 keeps nothing, so the handle's next Digest transfer probes again where curl would count
the old nonce on (`nc=00000002`); no upstream case needs that yet.
