# ADR-0341 — Negotiate's channel bindings come from the origin's TLS only

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-966.

## Context

BL-915 added `HttpAuthRequest.ServerCertificate`, which hand-built Negotiate turns into
`tls-server-end-point` channel bindings (ADR-0234), but nothing filled it. The open
question was which connection's certificate a request carries: the origin's, or an HTTPS
proxy's for the `Proxy-Authorization` request.

curl 8.18.0 with MIT GSS-API takes the bindings with
`Curl_ssl_get_channel_binding(data, FIRSTSOCKET, ...)`, which walks the connection's
filter chain for the `Curl_cft_ssl` filter, the TLS to the origin; the HTTPS proxy's TLS is
the separate `Curl_cft_ssl_proxy` filter and is never read. An `http://` URL through an
HTTPS proxy, the only case where a forward proxy request goes over TLS at all, therefore
sends zero bindings to both the proxy and the origin. Measuring it would need an MIT build
and a Kerberos realm behind an HTTPS proxy, which the loopback harness does not have, so
this follows curl's source.

## Decision

1. Once connected, `HttpProtocolHandler` sets the origin `HttpAuthRequest.ServerCertificate`
   to the first of `ConnectResult.PeerCertificates` (the server's own certificate) when the
   URL is `https`, through a tunnel or not.
2. An `http` URL leaves it empty, even when the connection is to an HTTPS proxy that
   reported certificates.
3. The proxy's `HttpAuthRequest` never carries one.
4. The value made before connecting (the first request's `Authorization`) has none; curl
   sends Negotiate only in answer to a challenge, by which time the connection is known.

## Alternatives considered

- **Pass the HTTPS proxy's certificate in the proxy's request**, as BL-966's Context
  suggested: curl never reads the proxy's TLS for bindings, so Curl would send bindings
  real curl does not, and a server checking them would treat the two differently.
