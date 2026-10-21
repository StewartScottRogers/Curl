# ADR-0727 — `--doh-url` sends the HTTPS-RR query first for an `https://` origin

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2920
- Decided by Claude under Stewart's delegation.

## Context

Upstream case 2100 (`HTTP GET using DoH (with HTTPS RR)`, no `--ech`) expects, under
`%if HTTPSRR`, three DoH POSTs before the GET: the HTTPS (type 65) query for
`_<port>._https.foo.example.com` (`Content-Length: 47` for a five-digit port) first, then the A and
AAAA queries. Curl sent the HTTPS query only under `--ech` (ADR-0312), after the address queries.

What curl 8.22.0 sends, read at tag `curl-8_22_0`:

- `lib/cf-https-connect.c` `Curl_cf_https_setup`, the HTTPS-CONNECT filter set up for an
  `https://` origin, calls `Curl_conn_dns_add_https_resolve` under `USE_HTTPSRR`, for every such
  origin, `--ech` or not. That filter is added before the DNS filter starts the address resolve,
  so the HTTPS resolve starts first. `lib/vtls/vtls.c` adds the same resolve under ECH; a second
  request for the same destination is "already there" (`cf_dns_add_resolve`), so it is sent once.
- `lib/vdns/doh.c` `Curl_doh` refuses to mix the HTTPS query with A or AAAA in one resolve: it
  is its own resolve, named the host on port 443 and `_<port>._https.<host>` on any other.
- `cf_dns_add_resolve` drops the HTTPS query for an IP address literal.

Measured: the reference curl on this machine, Git for Windows' curl 8.22.0 (Schannel, `Debug`),
lists no `HTTPSRR` feature, so it sends no HTTPS query; that is why upstream guards the expected
protocol with `%if HTTPSRR`. curl.se's release builds and the OpenSSL builds Curl matches list
`HTTPSRR`, and Curl's `--version` lists `HTTPSRR` in both its Schannel and OpenSSL feature lines
(`CurlVersionText`), so Curl behaves as an HTTPSRR build on every platform.

## Decision

- `TcpConnector` asks an `IHttpsOriginDnsResolver` for `ResolveHttpsOriginAsync(host, port)` when
  it resolves the origin of a direct connect (no proxy) to an `https://` origin (`IsHttpsOrigin`);
  every other resolve, `http://` origins and proxy names included, stays `ResolveAsync`.
- `DohDnsResolver` implements it: it posts the HTTPS query first and waits for its answer, then
  sends A and AAAA as before. Waiting keeps the HTTPS POST first at the server every time, where
  curl's parallel probes are first only by start order; the extra round trip changes no output.
- The decoded record is kept per host and port, so `--ech`'s `ResolveHttpsRecordAsync` for the
  same origin asks nothing more, as curl reuses the one resolve.
- The record is not yet used for anything but `--ech` (curl also reads its ALPN and port hints),
  and the HTTPS query writes no `-v` or `--trace-config doh` lines yet; both are follow-up tasks.

## Consequences

Case 2100's DoH POSTs now come in curl's order. A connect through a proxy sends no HTTPS query,
as before; whether curl 8.22.0 asks the origin's HTTPS record there is not measured.
