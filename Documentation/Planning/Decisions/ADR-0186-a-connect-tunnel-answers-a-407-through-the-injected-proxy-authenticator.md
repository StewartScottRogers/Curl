# ADR-0186 — A CONNECT tunnel answers a 407 through the injected proxy authenticator

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-602.

## Context

`TcpConnector` opens CONNECT tunnels (ADR-0023). Until BL-602 `HttpProxyTunnel` built
`Proxy-Authorization: Basic` itself from the proxy credential, whatever `--proxy-digest` or
`--proxy-anyauth` said (BL-601 parses them into `CommandLineOptions.ProxyAuthSchemes`), and
every `407` ended the connect with exit 7.

Measured against curl 8.21.0 (Schannel, Windows) on 2026-09-29, with
`Record-CurlExchange.ps1 -Connections 3` as the proxy (bytes in BL-602's Notes):

- `--proxy-basic` (and no switch) sends `Basic dTpw` on the first CONNECT; a `407` then fails
  with `curl: (7) CONNECT tunnel failed, response 407`.
- `--proxy-digest` and `--proxy-anyauth` send no `Proxy-Authorization` first; after the `407`
  they send the answer for the scheme libcurl's ranking picks. When the `407` carries
  `Connection: close` curl connects again (`Connect me again please`) and sends it there; when
  it does not, curl sends it on the same connection after the `Content-Length` body.
- A `407` to a CONNECT that carried a credential ends it: `Digest authentication problem,
  ignoring.` and exit 7 with the same message.

`Curl.Networking.UnitLibrary` does not reference `Curl.Authentication.UnitLibrary`, where the
ranking (ADR-0028) and Digest (ADR-0025) live, and ADR-0059 already has the tunnel take what
it needs from the composition.

## Decision

1. **The tunnel asks an `IHttpAuthenticator`.** `HttpProxyTunnelOptions` gains
   `ProxyAuthSchemes` (Basic by default) and `ProxyAuthenticator`. `TcpConnector` asks it, with
   an `HttpAuthRequest` of method `CONNECT`, request target the CONNECT authority, the proxy's
   credential and `IsProxy`, for the first CONNECT's `Proxy-Authorization` with no challenges,
   and after a `407` to a CONNECT that sent none with that reply's `Proxy-Authenticate` values.
   `HttpProxyTunnel.BuildConnectRequest` takes the value to send rather than building it.
2. **`Curl.Console` injects the origin's authenticator.** `CreateProxyTunnelOptions` sets
   `ProxyAuthSchemes` from `CommandLineOptions.ProxyAuthSchemes` and `ProxyAuthenticator` to
   `CreateHttpAuthenticator`'s `RankedHttpAuthenticator`, so the proxy gets the same Basic, the
   same Digest and the same ranking as the origin. Its Negotiate and NTLM contexts come from a
   `SystemSecurityContextFactory`, which is never reached: the ranked authenticator answers
   neither for a proxy until BL-604.
3. **With no authenticator the tunnel answers Basic up front and nothing after a challenge**
   (`PreemptiveBasicProxyAuthenticator`), in the options' `CredentialEncoding`, so a
   `TcpConnector` built without the composition behaves as before.
4. **Same connection or a new one, as the reply says.** `HttpProxyTunnel.ReadReplyAsync` also
   reads `Proxy-Authenticate`, `Content-Length`, `Connection`/`Proxy-Connection: close` and a
   chunked `Transfer-Encoding`. A reply that closes, or has a chunked body, or whose body ends
   early, makes the connector dial the proxy again (TLS to an HTTPS proxy included) and send
   the answer there; otherwise it discards the body and sends it on the same connection.
5. **One answer per connect.** A `407` to a CONNECT that sent a credential is the tunnel's
   exit 7, as curl gives up on a credential sent and challenged again. A Digest `stale=true`
   retry is not made, as the stateless Digest authenticator (ADR-0025) has no nonce count to go
   on; curl would retry it.

## Consequences

- `-p -x ... -U u:p --proxy-digest` and `--proxy-anyauth` now open tunnels through proxies
  that challenge, and `--proxy-digest` no longer sends Basic.
- Digest answers are in curl's own format (ADR-0025), not WDigest's, on Windows too; the hash
  is the one the reference build sends for the same cnonce (checked in BL-602's tests).
- A chunked `407` body costs a new connection where curl would reuse the connection.
- The tunnel's `-v` lines for the retry (`Connect me again please`, `Proxy auth using ...`) are
  not reported.

## Alternatives considered

- **Reference `Curl.Authentication.UnitLibrary` from `Curl.Networking.UnitLibrary`.** Would
  make Networking build Digest itself, against ADR-0059's rule that the tunnel takes its
  authentication from the composition, and tie the transport library to the credential one.
- **Always connect again after a `407`.** Simpler, but curl reuses a kept-open connection and
  a proxy can tell the difference.
