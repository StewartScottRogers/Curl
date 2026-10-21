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

## Amendment (BL-2954, 2026-10-21): the HTTPS-RR query's `--trace-config doh` lines

Decided by Claude under Stewart's delegation.

**How the lines were found.** Derived from curl 8.22.0's source at tag `curl-8_22_0`, not
measured: the reference curl on this machine (Git for Windows 8.22.0, Schannel) lists no
`HTTPSRR` feature and never sends the query. The prefixes follow from `lib/curl_trc.c`: `infof`
on a DoH sub-transfer writes its feature name, `[DoH] ` (`doh.c` sets `doh->state.feat` to
`Curl_trc_feat_doh`), and `Curl_trc_dns` writes `[DNS] `. BL-2973's measurement of the A and AAAA
lines on the same build (`* [DoH] [DoH] [AAAA] error decoding response: No such name`,
`* [DoH] DoH request complete, 0 to go`) confirms the doubled `[DoH] ` and the form.

**The lines,** from `doh_probe_done` and `doh_resp_decode_httpsrr` (`lib/vdns/doh.c`) and
`Curl_httpsrr_trace` and `Curl_httpsrr_print` (`lib/vdns/httpsrr.c`), each after the query's own
sub-transfer lines:

- a connection or exchange that failed: `[DoH] [DoH] [HTTPS] error: <curl_easy_strerror>`, and
  nothing more;
- an answer that did not decode: `[DoH] [DoH] [HTTPS] error decoding response: <text>`, an answer
  with no content reading `No such name` (curl turns `DOH_NO_CONTENT` into `DOH_DNS_NXDOMAIN` for
  the HTTPS type), and nothing more;
- an answer holding records: `[DNS] [HTTPS] got N records` (at most 4 kept), then for the first
  record a `[DNS] [HTTPS] record: <record so far>` line after each parameter read, the record
  printed as `<priority> <target>.` (`.` for the root) with ` mandatory-keys(ignored)`,
  ` alpn=` (only `http/1.1`, `h2`, `h3`, each once), ` port=`, ` no-default-alpn`, ` ipv6hint=`,
  ` ipv4hint=` and ` ech=<N bytes>` where present; a record curl refuses ends with
  `[DoH] [DoH] error decoding HTTPS RR: <curl_easy_strerror>` of the code curl returns
  (`Out of memory` for a target name that does not read, as the code is never reassigned;
  `A libcurl function was given a bad argument` for two bytes or a bad `no-default-alpn`, `port`,
  hint or `ech` value; `Unrecognized or bad HTTP Content or Transfer-Encoding` for a bad `alpn`
  list; `Weird server reply` otherwise), and nothing more;
- otherwise `[DoH] DoH request complete, 0 to go`: the HTTPS query is a resolve of its own, so
  its count never includes A and AAAA.

**Decision.** `DohDnsResolver` runs the HTTPS query on `SubTransferEvents`, as the A and AAAA
queries run, numbered connection #1 since it opens first, and `DohTraceLines.ReportHttpsRecord`
writes the lines above on the `--trace-config doh` sink, verbatim, prefixes included, once the
query has finished and before the A and AAAA queries start. The lines are written whenever that
sink is real; curl 8.22.0 writes the `[DNS] [HTTPS]` ones only when the `dns` feature is on as
well, and refuses a non-2xx DoH answer with `[DoH] [DoH] [HTTPS] error: HTTP status N`: both are
BL-2979, after BL-2973 settles 8.22.0's sub-transfer prefix and gating. The `-v` line
`HTTPS-RR <host>:<port>: ...` that `lib/vdns/cf-dns.c` writes on the main transfer is BL-2980.
