# ADR-1148: The DNS query extension adds DNS over HTTPS as --dns-transport https

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4339
- Decided by Claude under Stewart's delegation

## Context

ADR-1140 gave `dns:` URLs a transport selection and ADR-1143 added DNS over TLS and DNS over QUIC to it. BL-4339 adds
DNS over HTTPS (RFC 8484, October 2018). Upstream curl has no `dns:` scheme, so DoH here is tested against its RFC, not
against curl. The extension may not reference `Curl.Original.Protocol.Http` (or any `Curl.Protocol.*` library), so it cannot use
curl's HTTP stack, and must say how the DoH URI is given when the `dns:` URL only names a server (RFC 4501).

## Decision

- **Name.** `--dns-transport https`, in either case, listed after `quic`: it names the layer it adds, as `tls` and
  `quic` do.
- **The DoH URI.** The `dns:` URL's server and port give the authority and the path is always `/dns-query`, the path of
  RFC 8484 section 4.1's example URI template that public DoH servers serve. So
  `curl --dns-transport https dns://dns.google/example.com` queries `https://dns.google/dns-query`. The default port is
  443 (section 5: DoH is HTTPS). No option for another path: an extra option for a rare case, and adding one later is
  compatible. A URL naming no server asks the system's first DNS server on 443, as the other transports do on theirs.
- **POST, not GET.** The query is POSTed as the body with `Content-Type: application/dns-message` and `Accept:
  application/dns-message` (sections 4.1 and 6). POST needs no base64url coding and its body has no URI length limit;
  GET exists for HTTP caches, which one query per run does not use. For the same reason the query keeps its random
  Message ID instead of the 0 section 4.1 suggests for caching, so the handler checks every transport's answer alike.
- **A minimal HTTP/1.1 client inside the extension.** One connection per query, through curl's connector and the TLS
  provider's handshake with the URL's host as the certificate's name (section 5, as DoT does), sending
  `Connection: close`. HTTP/1.1 rather than HTTP/2 (which section 5.2 recommends but does not require): DoH servers
  accept HTTP/1.1, and HTTP/2's framing and HPACK would be a second HTTP stack in an extension. The response reader
  skips 1xx responses and takes the body by `Transfer-Encoding: chunked`, `Content-Length` or the connection's end.
- **Failures.** A final status other than 200 is exit 22 (`The DoH server answered HTTP <code>`), as curl's `--fail`
  makes an HTTP error. A content type other than `application/dns-message` (parameters ignored, any case), a malformed
  status line, header, `Content-Length` or chunk, a head line over 8192 octets or more than 100 header lines, or a body
  longer than a DNS message's 65535 octets is exit 8, as a malformed answer is on the other transports. A connection
  closed before the whole response arrived is exit 56; connect and handshake failures pass their exit codes through.

## Consequences

- `curl --dns-transport https dns://dns.google/example.com` works, with `--dns-json` as before; `--dns-transport`'s help
  and its exit 2 message list `udp, tcp, tls, quic, https`.
- A DoH server whose URI path is not `/dns-query`, or that serves only HTTP/2, cannot be queried; either can be added as
  a later choice without changing this one.
