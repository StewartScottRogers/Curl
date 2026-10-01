# ADR-0270 — Proxy NTLM and Negotiate go on over the same proxy connection, as the origin's do

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-604.

## Context

BL-602 (ADR-0186) answers a CONNECT tunnel's `407` and BL-603 (ADR-0239) a forward proxy's,
but only once, for Basic and Digest: `RankedHttpAuthenticator` refused NTLM and Negotiate for a
proxy, the tunnel never went on after a CONNECT that sent a credential, and the forward proxy's
first `Proxy-Authorization` was made synchronously, so no Type 1 could go up front. curl
8.21.0 (mingw, Schannel, SSPI) was measured on 2026-09-30 with `Record-CurlExchange.ps1
-Script` (BL-604 Notes): `--proxy-ntlm -U u:p` sends Type 1 on the first CONNECT or request,
the Type 3 for the proxy's Type 2 on the same connection, for the SPN `HTTP/<proxy host>`, and a
bare `NTLM` 407 after Type 3 ends the handshake ("NTLM handshake rejected": exit 7 for a tunnel,
exit 0 with the 407 for a forward request). `--proxy-negotiate -U :` without a ticket steps
SSPI before the first request and after the 407, sends nothing, and writes
`InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS ...` before `Proxy auth using Negotiate
with user ''`.

## Decision

1. **One ranking for both parties.** `RankedHttpAuthenticator` answers a proxy's request on
   the origin's terms: `--proxy-ntlm` alone sends Type 1 before any challenge and NTLM goes on
   through `ContinueAuthorizationAsync`; `--proxy-negotiate` alone steps a context before the
   first request, steps one without answering when `-U` is absent, and goes on from the
   proxy's token (ADR-0227). `NegotiateHttpAuthenticator` already names
   `--proxy-service-name` for a proxy request.
2. **The proxy's own URL.** The `HttpAuthRequest` for a proxy carries the proxy's URL
   (`http[s]://host:port/`), so NTLM and Negotiate ask for `HTTP` on the proxy's host, as
   curl's Type 3 names `HTTP/127.0.0.1`. Digest keeps hashing the request target, which is
   unchanged (the origin form forward, the CONNECT authority for a tunnel).
3. **The tunnel goes on.** `TcpConnector` asks `ContinueAuthorizationAsync` for a `407` to a
   CONNECT that sent a credential, telling it whether that value answered a challenge
   (tracked across a redial too), and sends the answer on the same connection when the reply
   leaves it reusable, else on a new one, as ADR-0186 does. Basic and Digest still answer
   nothing there, so their failure stays exit 7. An `HttpAuthenticationFailedException` (SSPI
   cannot answer a Type 2, exit 94) is the tunnel's failure with its exit code and message.
   An empty answer (ADR-0232) sends no second CONNECT. When the tunnel opens, or the reply is
   not a `407`, `EndAuthorization` disposes of a Negotiate context kept for its next leg
   (ADR-0248).
4. **The forward proxy's first value is asynchronous** (`CreateAuthorizationAsync`), and what
   the authenticator reports while making it is written just before `Proxy auth using ...`,
   as measured.
5. **The tunnel's contexts come from ADR-0142's router.** `CurlComposition.CreateTransports`
   builds the tunnel's authenticator over a `LateBoundSecurityContextFactory`, bound to
   `CreateSecurityContextFactory`'s router once the connectors it needs for KDC exchanges exist,
   so off Windows proxy NTLM is curl's own NTLM as the origin's is, not the system GSS-API.

## Consequences

- Choices with a sensible default taken, not measured: a tunnel's second CONNECT for
  `--proxy-anyauth` picking a Negotiate context that makes no token is not sent (curl sends it
  without the header and fails on its 407 with the same exit 7); a Type 3 answering a Type 2
  whose 407 closed the connection goes out on a new connection, where NTLM's connection binding
  will fail it, as curl's would.
- Not done here, filed as follow-ups: curl's stderr for a tunnel whose `--proxy-negotiate`
  context failed is that failure line (`curl: (7) InitializeSecurityContext failed: ...`), not
  `CONNECT tunnel failed, response 407`; and the tunnel's `-v` lines for the handshake
  (`Proxy auth using NTLM with user 'u'`, `NTLM handshake rejected`).
