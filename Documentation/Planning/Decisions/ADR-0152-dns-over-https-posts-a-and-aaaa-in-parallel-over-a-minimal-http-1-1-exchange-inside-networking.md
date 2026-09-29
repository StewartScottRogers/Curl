# ADR-0152 — DNS-over-HTTPS POSTs A and AAAA in parallel over a minimal HTTP/1.1 exchange inside Networking

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-639.

## Context

`--doh-url` makes curl resolve the transfer's host names through an RFC 8484
DNS-over-HTTPS server. The resolver belongs beside `SystemDnsResolver` in
`Curl.Networking.UnitLibrary`, behind `IDnsResolver`, but the DoH query is itself an
HTTPS request: `Curl.Networking.UnitLibrary` cannot reference
`Curl.Protocol.Http.UnitLibrary` (it is not a protocol, and protocols are referenced by
nothing but `Curl.Console`), and `HttpClient` is barred from the solution's protocol code
and is a trim risk under native AOT.

curl 8.21.0 (Schannel, Windows 11, Git for Windows' `mingw64` build, the reference build of
ADR-0009 and ADR-0018) was measured with `Record-CurlExchange.ps1 -Tls` standing in for the
DoH server on 2026-09-28. `P` is the recorder's port; `D` is `--doh-url https://127.0.0.1:P/dns-query`.

| Command | Server answer | Result |
| --- | --- | --- |
| `-sS -v D --doh-insecure http://example.test/` | `500`, empty body, both connections | Two POSTs (below), stderr `* Could not resolve host: example.test` / `* Could not resolve: example.test:80` / `* closing connection #0` / `curl: (6) Could not resolve host: example.test`, exit 6 |
| the same, `--trace-config doh` | `500` | `DoH: Too small type A for example.test`, `DoH: Too small type AAAA for example.test`, exit 6 |
| `-sS -v -k D http://example.test/` (no `--doh-insecure`) | none: both handshakes fail | request.bin empty, the same four lines, exit 6: `-k` does not reach the DoH transfers |
| `-sS -v --trace-config doh D http://example.test/` | none | `schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - ...` and `DoH request SSL peer certificate or SSH remote key was not OK` per query, `curl: (6) Could not resolve host: example.test`, exit 6 |
| `-sS --doh-cert-status --doh-insecure D http://example.test/` | `500` | Both POSTs sent (306 bytes), exit 6: accepted, and with `--doh-insecure` no status failure |
| `-sS D --doh-insecure http://example.test/` with nothing listening on `P` | none | after about 2 s, `curl: (6) Could not resolve host: example.test`, exit 6 |
| `-sS -v D --doh-insecure http://example.test:48637/` | A: `200`, `application/dns-message`, one A record `127.0.0.1` TTL 60; AAAA: `200`, NOERROR, no answer | `* Host example.test:48637 was resolved.` / `* IPv6: (none)` / `* IPv4: 127.0.0.1` / `*   Trying 127.0.0.1:48637...`, then exit 7 (nothing listening on 48637) |
| the same with a 45-byte `Content-Length` on the 46-byte A answer | truncated A | `DoH: Out of range type A for example.test`, `DoH: No content type AAAA for example.test` (AAAA answer had no records), exit 6 |
| the same with RCODE 3 (NXDOMAIN) in both answers | NXDOMAIN | `DoH: Bad RCODE type A ...`, `DoH: Bad RCODE type AAAA ...`, exit 6 |

The POST curl sent for the A query, byte for byte (the AAAA query differs only in its
QTYPE, `00 1C`):

```
POST /dns-query HTTP/1.1\r\n
Host: 127.0.0.1:P\r\n
Accept: */*\r\n
Content-Type: application/dns-message\r\n
Content-Length: 30\r\n
\r\n
00 00 01 00 00 01 00 00 00 00 00 00 07 65 78 61 6D 70 6C 65 04 74 65 73 74 00 00 01 00 01
```

That is ID 0, flags `0x0100` (RD only), one question, no other records, QTYPE A (1) then
AAAA (28), QCLASS IN. There is no `User-Agent` line, even without `-A`. The `--trace-config`
trace shows both TLS handshakes begun before either query was answered: the two queries
run in parallel on two connections, A written first. Each DoH connection offered ALPN
`http/1.1` only and spoke HTTP/1.1. Plain `-v` shows none of the DoH exchange: only the
resolved lines, or the two `Could not resolve` lines, of the transfer itself.

Two places could own the HTTP exchange:

1. A minimal HTTP/1.1 POST writer and response reader in `Curl.Networking.UnitLibrary`,
   next to `HttpProxyTunnel`, which already writes an HTTP request (CONNECT) and reads a
   reply's head there for the same reason.
2. A resolver in `Curl.Console` that composes `Curl.Protocol.Http.UnitLibrary`'s handler.

## Decision

1. **Where the exchange lives.** `DohDnsResolver` (BL-641) lives in
   `Curl.Networking.UnitLibrary` and implements `IDnsResolver`. It opens each DoH connection
   with a `TcpConnector` of its own, built by the composition root with the system resolver
   (the DoH server's own name is resolved as any host is; an IP literal is dialled as
   written) and a TLS provider built from the DoH TLS options only, and does its own
   minimal HTTP/1.1 exchange: it writes the measured POST and reads the status line,
   the header block and a `Content-Length` or chunked body (or a body delimited by the
   close). The DNS message bytes come from BL-640's codec. No HTTP/2, no redirects, no
   authentication, no cookies, no proxy: none was measured, and the DoH answer does not
   depend on them.
2. **The query sequence.** For each name, two POSTs in parallel on two connections, A
   first and then AAAA, each built as measured: request line `POST <path> HTTP/1.1`,
   `Host` (with the port only when the DoH URL has one), `Accept: */*`,
   `Content-Type: application/dns-message`, `Content-Length`, and no `User-Agent`. The
   resolver returns the AAAA answer's addresses, then the A answer's, the order curl's
   `-v` lists them in (`IPv6:` before `IPv4:`); `-4` and `-6` keep filtering the returned
   addresses as ADR-0143 decides, and whether they also stop the other query from being
   sent is measured by BL-642 before it is pinned.
   `--resolve` entries and the DNS cache are consulted before the resolver, as they are
   for the system resolver, so DoH changes only what the resolver is asked; BL-642 measures
   the combination before pinning it.
3. **The TLS options.** The DoH connections verify the DoH server's certificate unless
   `--doh-insecure` is given; the transfer's own `-k`/`--insecure` does not reach them
   (measured). `--doh-cert-status` asks for the DoH server's stapled status exactly as
   `--cert-status` does for the transfer (BL-610), and is skipped when `--doh-insecure` is
   given, as measured. ALPN offers `http/1.1` only.
4. **The failure mapping.** Every DoH failure - no connection, a failed handshake or
   certificate check, a non-`200` status, a missing or wrong `Content-Type`, a malformed or
   truncated DNS message, a bad RCODE, or an answer with no address of the family asked -
   leaves that query without addresses. When both queries end without an address the
   resolver returns none, and the transfer fails as any unresolved name does: exit 6
   `Could not resolve host: <host>`, with `-v` also writing `Could not resolve: <host>:<port>`
   (exit 5 `Could not resolve proxy: <host>` when the name was a proxy's). One query
   answering is enough. The DoH trace lines (`DoH: Too small type A for <host>` and the rest)
   are written only for `--trace-config doh`, not for `-v`, as measured.

## Amendment (BL-641, 2026-09-28)

Decided by Claude under Stewart's delegation. Point 4 guessed that a non-`200` status or a
missing or wrong `Content-Type` fails the query; measuring it with `Record-CurlExchange.ps1 -Tls`
(`-sS -v --trace-config doh D --doh-insecure http://example.test:P2/`, every connection answered
with the 46-byte A answer for `127.0.0.1`) showed curl 8.21.0 looks at neither:

| Response framing the A answer | Result |
| --- | --- |
| `500 Internal Server Error`, `application/dns-message`, `Content-Length: 46` | `IPv4: 127.0.0.1`, then exit 7: resolved |
| `200 OK`, `Content-Type: text/plain`, `Content-Length: 46` | resolved |
| `200 OK`, no `Content-Type`, `Content-Length: 46` | resolved |
| `200 OK`, `Transfer-Encoding: chunked` | resolved |
| `200 OK`, neither `Content-Length` nor chunked (body ends at the close) | `DoH request Failure when receiving data from the peer` per query, exit 6 |
| `localhost`, `a.localhost`, `127.0.0.2`, `[::1]` as the transfer's host, nothing listening on the DoH port | resolved without asking the DoH server (`::1`, `127.0.0.1` for the first two), exit 7 |

So `DohDnsResolver` decodes the body whatever the status and `Content-Type`; a body framed by
neither `Content-Length` nor chunked coding, cut short, or over curl's 3000-byte DoH buffer
(`DYN_DOH_RESPONSE`) is a receive failure; and IP literals and `localhost` never reach the
server. The `500` with an empty body of the table above still fails, because the empty body
decodes as `Too small`. The rest of point 4 stands.

## Amendment (BL-642, 2026-09-29)

Decided by Claude under Stewart's delegation. BL-642 measured the rest of the design with
`Record-CurlExchange.ps1 -Tls` as the DoH server on `P1`, answering every connection with the
46-byte A answer for `127.0.0.1`, and a second, plain recorder on `P2` as the transfer's server
(curl 8.21.0 Schannel; every case is in BL-642's Notes):

| Command | Result |
| --- | --- |
| `-sS -v --doh-url https://127.0.0.1:P1/dns-query --doh-insecure http://example.test:P2/` | the two POSTs above, then `Host example.test:P2 was resolved.`, `IPv6: (none)`, `IPv4: 127.0.0.1`, `Trying 127.0.0.1:P2...` and the GET to `P2`, exit 0 |
| the same with `--resolve example.test:P2:127.0.0.1` | `Added ... to DNS cache`, `Hostname example.test was found in DNS cache`; nothing reached the DoH server |
| `--doh-url bogus` | exit 6 after about 7 s (curl asked `http://bogus/`, which does not resolve) |
| `--doh-url ftp://127.0.0.1:P1/` | exit 6, nothing reached the DoH server |
| `--doh-url 127.0.0.1:P1/dns-query` (no scheme) | plain HTTP to the TLS server, exit 6: the scheme is guessed as `http` |
| `--ssl-no-revoke --cacert root.pem` (the recorder's root), no `--doh-insecure` | resolved, exit 0: `--cacert` and `--ssl-no-revoke` reach the DoH connections |
| `-4` / `-6` | only the A / only the AAAA POST is sent (153 bytes) |
| `--doh-url ''` | accepted, DoH off; `--no-doh-url` cannot be reversed; `--no-doh-insecure` and `--no-doh-cert-status` are accepted |

1. **`--resolve` wins**, as point 2 expected: the DoH resolver is asked only for a name no
   `--resolve` entry answers.
2. **The DoH URL.** A value without `://` gets `http://` in front; a value that then is not an
   absolute `http` or `https` URL resolves every name to nothing (`UnusableDohUrlResolver`), so each
   transfer fails with exit 6. An empty value turns DoH off. `--doh-url` wins over the c-ares
   options: curl asks the DoH server whichever resolver it was built with.
3. **The DoH TLS options.** The DoH connections take the options curl's `lib/doh.c` copies onto each
   DoH transfer: `--cacert`, `--capath`, `--crlfile`, `--curves`, `--ssl-no-revoke`,
   `--ssl-revoke-best-effort` and `--ssl-auto-client-cert`, beside `--doh-insecure` (as `Insecure`) and
   `--doh-cert-status` (as `RequireCertificateStatus`). `-k` and `--cert-status` stay out.
4. **`--doh-cert-status` replaces point 3's "skipped when `--doh-insecure` is given".** That was
   measured on the Schannel build, which ignores `--cert-status` altogether. ADR-0191 decided
   `--cert-status` checks on every platform as the OpenSSL build does, and the OpenSSL build checks
   the stapled status under `-k` too (BL-610). So `--doh-cert-status` reaches the DoH handshakes as it
   is, and the TLS provider applies ADR-0191 to them exactly as to the transfer's.
5. **`-4` and `-6`** keep filtering the returned addresses, so the transfer's output is the same;
   sending only that family's query is a follow-up task in `Curl.Networking.UnitLibrary`.

## Consequences

- BL-640 builds the DNS message codec in `Curl.Networking.UnitLibrary` and pins the query
  bytes above.
- BL-641 builds `DohDnsResolver` with the HTTP/1.1 exchange above, over injected connector
  and TLS provider, and pins the POST bytes and the exit-6 failures.
- BL-642 parses `--doh-url`, `--doh-insecure` and `--doh-cert-status`, composes the
  resolver in `Curl.Console`, and measures `--resolve` with `--doh-url` and a bogus DoH URL
  before pinning them.
- `Curl.Networking.UnitLibrary` gains a second, smaller HTTP writer beside
  `HttpProxyTunnel`. Both stay minimal; neither grows into a general HTTP client.
- Off Windows, the OpenSSL build of curl may offer `h2` to the DoH server. The DoH answer
  and the transfer's output are the same either way, so HTTP/1.1 is used everywhere.

## Alternatives considered

- **Compose `Curl.Protocol.Http.UnitLibrary`'s handler in `Curl.Console`.** Reuses the full
  HTTP stack, but puts a resolver in the executable, drags every transfer option (`-A`,
  `-H`, `-b`, `--proxy`, `-k`) into a request curl builds from none of them, and makes the
  resolver untestable without the composition root. The measured POST is five fixed
  header lines; a minimal writer is smaller than the plumbing needed to keep the transfer's
  options out.
- **`HttpClient`.** Barred from protocol code by the root `CLAUDE.md`, not AOT-friendly, and
  it adds a `User-Agent`-free request only with care; the connector and TLS provider the
  solution already has do the job.
- **Ask A and AAAA in turn.** Simpler, but curl asks in parallel (measured), and the
  resolve time `-w %{time_namelookup}` reports would differ.
