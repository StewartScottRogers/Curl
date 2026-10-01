# ADR-0300 — A KDC proxy is verified against the realm's http_anchors with its own TLS, never the transfer's

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1063.

## Context

BL-882 made `KerberosKdcProxyHttpsTransport` post MS-KKDCP messages to an `https://` KDC by
asking the run's connector for `ConnectTarget(UseTls: true)`. That connector carries the
transfer's TLS options, so `curl -k --negotiate https://...` also accepted any certificate from
the KDC proxy, and `--cacert` and `--capath` replaced the trust it was checked against.

MIT never lets the application decide. `sendto_kdc.c` hands the connection to the `k5tls`
module, which reads the realm's `http_anchors` relation (`[realms] REALM { http_anchors = ... }`;
`profile_get_values` with `KRB5_CONF_REALMS`, realm, `KRB5_CONF_HTTP_ANCHORS`) and, in
`load_anchor`, takes `FILE:` as a PEM file, `DIR:` as a directory whose files not starting with
a dot are each loaded as a PEM file (at least one must load), and `ENV:` as an environment
variable holding another value; any other value is `EINVAL`. With no `http_anchors` OpenSSL's
default verify paths are used. The host name is always checked, and a load failure fails the
connection to that KDC.

## Decision

1. `KerberosConfiguration.HttpAnchors(realm)` returns the realm's `http_anchors` values in file
   order. Like MIT it reads the realm only; a `[libdefaults]` `http_anchors` is ignored.
   `KerberosHttpAnchor.Parse` reads one value by its case-sensitive `FILE:`, `DIR:` or `ENV:`
   prefix, `null` for any other.
2. `IKerberosKdcProxyTransport` gains an overload taking the anchors. `KerberosKdcSender` always
   calls it. Its default implementation runs the five-argument exchange when there are no
   anchors and fails with an `IOException` when there are any, so a transport that cannot honour
   them never falls back to another trust store. A default keeps the existing implementations
   (the console's test fake among them) compiling unchanged.
3. `KerberosKdcProxyHttpsTransport` asks its connector for plain TCP (`UseTls: false`) and runs
   its own handshake through `IKerberosKdcProxyTlsClient`: `KerberosKdcProxyTlsClient`, an
   `SslStream` with no ALPN and no client certificate, checking the host name, with a
   `CustomRootTrust` chain policy of the loaded anchors and no revocation check (OpenSSL's
   default) when there are anchors, and the system's trust store otherwise. The transfer's `-k`,
   `--cacert`, `--capath`, `--crlfile` and `--pinnedpubkey` therefore never reach a KDC proxy.
4. `KerberosHttpAnchorLoader` loads the anchors as `load_anchor` does. Any anchor that cannot be
   loaded fails the whole set with an `IOException` before connecting, so the sender moves to the
   next KDC, as MIT does. An `ENV:` variable whose value is itself `ENV:` is refused rather than
   followed: MIT recurses without a limit, and a variable naming itself would never end.

## Consequences

- A KDC proxy with a private CA works only when `krb5.conf` names it in `http_anchors`, as with
  MIT's `kinit`; `-k` no longer hides an untrusted proxy.
- The connector still routes the TCP connection, so `--connect-to`, `--resolve` and a SOCKS or
  HTTP proxy still apply to it, as they did since BL-882.
- `KerberosKdcProxyTlsClient` is the second type that constructs an `SslStream`, beside
  `SslStreamTlsProvider`; both say so.
