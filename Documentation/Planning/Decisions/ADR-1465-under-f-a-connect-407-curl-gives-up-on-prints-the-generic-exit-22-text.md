# ADR-1465 — Under -f a CONNECT 407 curl gives up on prints the generic exit 22 text

- **Status:** Accepted
- **Date:** 2026-11-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6024.

## Context

STD-227: `curl -sS -f --proxy-user a:b -x http://P https://H/` against a proxy that answers every
CONNECT with `407` and `Proxy-Authenticate: Basic` ends with exit 22 and
`curl: (22) HTTP response code said error` in curl 8.22.0 (Schannel) and 8.18.0 (OpenSSL). Curl
printed `The requested URL returned error: 407`, the text BL-5443 gave every refused CONNECT under
`-f`.

curl's `Curl_http_input_auth` sets `authproblem` when the challenge offers the scheme it already
sent (`-v`: `Basic authentication problem, ignoring.`), and `Curl_http_auth_act` then returns
`CURLE_HTTP_RETURNED_ERROR` under `-f` without a `failf`, so only the generic text of exit 22 is
printed. Measured (BL-6024 Notes): a 407 with no challenge, or a challenge to no credential, and a
403 still print `The requested URL returned error: N`; the forward-proxy path is unchanged.

## Decision

- `ConnectResult.IsProxyAuthenticationProblem` carries curl's `authproblem` out of the connector.
  `TcpConnector`'s tunnel failure sets it when the final CONNECT reply draws the problem line
  (`ConnectTunnelVerboseLines.DrawsAuthenticationProblem`, the same rule the `-v` line follows),
  and the places that rebuild a failed result (`NumberedConnectFailure`, the timings rebuild) keep it.
- Under `-f` the HTTP handler gives such a failure `HTTP response code said error`; every other
  refused CONNECT keeps `The requested URL returned error: N`.

## Consequences

- The one rule decides both the `-v` problem line and the exit 22 text, so they cannot disagree.
- CONNECT-UDP refusals are not marked; curl's HTTP/3 proxy path was not measured for this.
