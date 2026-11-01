# ADR-1153 — --masque runs QUIC and HTTP/3 through a CONNECT-UDP or CONNECT-IP proxy

- **Status:** Accepted
- **Date:** 2026-10-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4144.

## Context

BL-4144 gives `Curl.Extension.Masque.UnitLibrary` (ADR-0999) its option. Its wire format (ADR-1115) and client flow
(ADR-1122, `MasqueDatagramConnector`) are in place, and the engine opens QUIC's datagram channels through a
connection hook's datagram connector (BL-4318, ADR-1010). curl has no MASQUE support; the extension is tested against
RFC 9298 (Proxying UDP in HTTP, August 2022) and RFC 9484 (Proxying IP in HTTP, October 2023) and changes nothing
without its option.

## Decision

- **Option.** One `--masque <uri-template>`, description name `masque`. The template is the proxy's RFC 6570 URI
  template, `http` or `https`. It names `target_host` and `target_port` for CONNECT-UDP (RFC 9298 §2), or `target`
  and `ipproto` for CONNECT-IP (RFC 9484 §3); naming either of the first pair picks CONNECT-UDP, anything else
  CONNECT-IP, so one option covers both and the template, which the proxy's operator publishes, decides. A template
  that does not expand (an unclosed expression, a CONNECT-UDP pair half named, no variable at all, a scheme other than
  `http` or `https`) is refused at activation as an extension option conflict, with `--masque needs a CONNECT-UDP or
  CONNECT-IP URI template: <why>`.
- **Hook.** `MasqueConnectionHook` keeps curl's stream connector for every TCP transfer and replaces curl's datagram
  connector, so QUIC (`--http3`, `--http3-only`) and TFTP open their datagram channels through the proxy. The proxy
  is reached through curl's own connector, so `-k`, `--cacert` and the other TLS options apply to an `https` proxy.
  The engine takes the datagram connector while it builds the transports, before it hands the hook curl's stream
  connector, so the datagram connector reaches the proxy through a `LateBoundConnector` that the stream connector
  binds; an open before that fails with exit 7, which the composition's order never lets happen.
- **Registration.** No URL scheme, so no line in `ExtensionCatalogue`; one line each in the option, description and
  connection hook catalogues.

## Left out

- HTTP/2 and HTTP/3 extended CONNECT to the proxy (RFC 9298 §3.3, §3.4): the proxy is reached over HTTP/1.1 Upgrade,
  as ADR-1115 records, since the extension may not reference `Curl.Original.Protocol.Http`.
- CONNECT-IP routes and several assigned addresses: the tunnel asks for one address and carries UDP to one target,
  which is all QUIC needs (ADR-1122).
- A separate option per tunnel kind: the template already says which tunnel the proxy offers.
