# ADR-0226 — `--alt-svc` uses `h2` and `h3` alternatives as curl.se's build looks them up

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-733.
Widens ADR-0214 decision 2 and replaces its decision 4.

## Context

ADR-0214 used only `h1` alternatives because HTTP/2 and HTTP/3 did not run yet. Both do now
(ADR-0141, ADR-0144, ADR-0172). The Windows reference build (Git for Windows' curl 8.21.0,
Schannel) has neither, so it never uses an `h2` or `h3` alternative; ADR-0144 already made
curl.se's ngtcp2 build (WinGet `cURL.cURL`, curl 8.18.0 with nghttp2, ngtcp2 and nghttp3) the
reference for HTTP/3 on every platform, and the root `CLAUDE.md` says HTTP/3 includes the
Alt-Svc upgrade.

The rule is libcurl's `url_set_conn_peer` (`lib/url.c`) with `Curl_http_neg_init`
(`lib/http.c`) and `Curl_altsvc_lookup` (`lib/altsvc.c`), all at tag `curl-8_21_0`, and the
connection filter in `lib/cf-https-connect.c`. The measurements, with curl.se's build on
Windows on 2026-09-29, are in BL-733's Notes:

- Two runs sharing `--alt-svc f` against `https://www.google.com/`: the first over HTTP/2 writes
  `h2 www.google.com 443 h3 www.google.com 443 …`; the second connects over HTTP/3 with no
  `Alt-svc connecting` line (the entry names the origin itself) and adds
  `h3 www.google.com 443 h3 www.google.com 443 …`.
- An `h3` entry naming the origin itself, with nothing on UDP: the QUIC failure lines, then TCP,
  exit 0 over HTTP/1.1 - the `--http3` race and fallback.
- An `h3` entry naming another port, with nothing on UDP: `Alt-svc connecting from
  [h1]127.0.0.1:18733 to [h3]127.0.0.1:18734`, the QUIC failure, exit 56 - no TCP fallback.
- An `h2` entry naming another port: `ALPN: curl offers h2`; a server that agrees on nothing
  gets HTTP/1.1 with `Alt-Used`, exit 0.
- `--http3` with an `h1` entry naming another port: one `Alt-svc connecting` line, then QUIC to
  the alternative, then TCP to it.

## Decision

1. **Every platform uses `h2` and `h3` alternatives**, as curl.se's build with HTTP/2 and HTTP/3
   does. The version option chooses the origin versions an entry is looked up under, in order,
   and the destination versions it may name:

   | Option | Looked up under | May switch to |
   | --- | --- | --- |
   | none | `h2`, `h1` | `h1`, `h2`, `h3` |
   | `--http1.1` | `h1` | `h1` |
   | `--http2` | `h2`, `h1` | `h1`, `h2` |
   | `--http3` | `h3`, `h2`, `h1` | `h1`, `h2`, `h3` |
   | `--http3-only` | `h3` | `h3` |
   | `-0`, `--http2-prior-knowledge` | nothing | - |

   `AltSvcCache.FindForOrigin` (`Curl.Core.UnitLibrary`) runs the lookup and says whether the
   entry names the origin itself; `AltSvcTransferCache.ApplyTo` (`Curl.Console`) turns it into
   the transfer's `AltSvcRoute` and `Version`.
2. **An entry naming another host or port** is the route, as before. When its version differs
   from the one it was found under, the transfer switches: `h3` to `Http3Only` (QUIC alone,
   failing as measured), `h1` and `h2` to `Http11`, where TCP's ALPN chooses between HTTP/1.1 and
   HTTP/2. When the versions agree, the version option's own preference stays, so `--http3`
   still races QUIC against TCP to the alternative.
3. **An entry naming the origin itself** is no route. An `h3` one makes a transfer without a
   version option `Http3`, racing QUIC against TCP and falling back to TCP as measured; every
   other same-destination entry leaves the version as it is.
4. **Redirects look up again.** `RedirectFollower` takes a `HopAltSvcSelector`; the runner's
   calls `ApplyTo` with each hop's URL, so a hop to another origin gets its own route and
   version (the first hop's switch to HTTP/3 does not follow it), and a hop to the same origin
   sees entries the first hop's response added, as curl looks up for each connection.

## Consequences

Known differences, each filed:

- Headers are still learned under `h1` whatever version the response came over, where curl
  records `h2` or `h3`; so `--http3-only` never uses an entry its own responses taught (BL-943).
- ALPN is still the option group's list, where curl offers `h2` alone to an `h2` alternative and
  `http/1.1` alone after a switch to `h1`, and prefers a same-destination `h2` or `h1` entry's
  version (BL-944). On Windows without a version option, an `h2` alternative is therefore
  offered `http/1.1` and spoken to over HTTP/1.1.
- A `--http3` race to an alternative reports `Alt-svc connecting` twice, once per attempt (BL-945).

## Alternatives considered

- **Follow the Windows Schannel build and never use `h2` or `h3`.** Rejected: HTTP/2 and HTTP/3
  run on every platform (ADR-0141, ADR-0144), and the standing rule includes the Alt-Svc upgrade.
- **Map an `h2` alternative to `Http2PriorKnowledge`.** It would speak HTTP/2 even when the
  server's ALPN chose HTTP/1.1, which curl does not (measured); rejected for ALPN deciding.
