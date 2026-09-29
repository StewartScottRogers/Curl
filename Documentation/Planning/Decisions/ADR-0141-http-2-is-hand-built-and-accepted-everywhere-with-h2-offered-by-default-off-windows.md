# ADR-0141 — HTTP/2 is hand-built and accepted on every platform; a plain `https://` offers `h2` by default on Linux and macOS and `http/1.1` alone on Windows

- **Status:** Accepted
- **Date:** 2026-09-28
- **Supersedes:** ADR-0017, for HTTP/2 only (its HTTP/3 half is superseded by BL-718's ADR)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-655.

## Context

ADR-0017 refused `--http2` and `--http2-prior-knowledge` on every platform, because
Milestone 1 had no HTTP/2 and the Windows reference build has none either. The
conformance audit of 2026-09-28 (row 32, Major) flagged the cost ADR-0017 named: on
Linux and macOS the platform's curl accepts `--http2` and negotiates `h2` by default,
and Curl did neither. Since then Stewart's standing rule (root `CLAUDE.md`, 2026-09-28)
is that Curl is a complete reimplementation: if any official curl build supports a
feature, Curl supports it on every platform, and what the BCL lacks is built by hand in
its own `Curl.<Area>.UnitLibrary`. The ADR decides how, not whether.

Measured on 2026-09-28:

| Build | `-V` | `Features:` has `HTTP2` |
| --- | --- | --- |
| `C:\Windows\System32\curl.exe` (Windows reference) | `curl 8.21.0 (Windows) libcurl/8.21.0 Schannel zlib/1.3.2 WinIDN WinLDAP` | no |
| Git for Windows' `mingw64\bin\curl.exe` | `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel … libssh2/1.11.1 WinLDAP` | no |
| curl.se's Windows build (curl-for-win, installed by WinGet) | `curl 8.18.0 (x86_64-w64-mingw32) libcurl/8.18.0 LibreSSL/4.2.1 … nghttp2/1.68.0 ngtcp2/1.21.0 nghttp3/1.15.0 WinLDAP` | yes (and `HTTP3`) |
| Ubuntu (WSL) | `curl 8.18.0 (x86_64-pc-linux-gnu) libcurl/8.18.0 OpenSSL/3.5.5 … nghttp2/1.68.0 …` | yes |

curl.se's current page lists 8.22.0 with nghttp2 1.70.0; the installed 8.18.0 is the
same build line.

What each build does, from `curl -v` against `https://www.google.com/` and
`http://www.google.com/`:

| Invocation | Windows reference (Schannel) | curl.se Windows build and Ubuntu (identical HTTP lines) |
| --- | --- | --- |
| `https://` with no version option | `* ALPN: curl offers http/1.1`, then `* using HTTP/1.x`, `> GET / HTTP/1.1` | `* ALPN: curl offers h2,http/1.1`, `* ALPN: server accepted h2`, `* using HTTP/2`, `* [HTTP/2] [1] OPENED stream for https://www.google.com/`, one `* [HTTP/2] [1] [<name>: <value>]` line per request header, `> GET / HTTP/2`, `< HTTP/2 200 ` |
| `--http2 https://` | exit 2: `curl: option --http2: the installed libcurl version does not support this` then `curl: try 'curl --help' for more information` | `* ALPN: curl offers h2,http/1.1`, then as above |
| `--http2-prior-knowledge https://` | exit 2, the same two lines naming `--http2-prior-knowledge` | `* ALPN: curl offers h2` (h2 alone), then as above |
| `--http1.1 https://` and `--http1.0 https://` | `* ALPN: curl offers http/1.1` | `* ALPN: curl offers http/1.1` |
| `--http2 http://` | exit 2, as above | HTTP/1.1 request carrying `Upgrade: h2c`, `HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA` and `Connection: Upgrade, HTTP2-Settings`; stays HTTP/1.1 when the server answers `200` |
| `--http2-prior-knowledge http://` | exit 2, as above | no upgrade: `* using HTTP/2` straight after connecting, the client preface and `SETTINGS`, then `> GET / HTTP/2` |

The HTTP/2 lines (`using HTTP/2`, `[HTTP/2] [n] …`, `> GET / HTTP/2`) come from
libcurl's own HTTP/2 layer and are byte-identical between curl.se's Windows build and
the Linux build; only the TLS lines differ, and those belong to the TLS backend.

## Decision

1. **HTTP/2 is hand-built.** HPACK (RFC 7541) and the frame layer, connection preface,
   `SETTINGS`, flow control and stream state (RFC 9113) live in
   `Curl.Http2.UnitLibrary` with `Curl.Http2.UnitTests`, under the same quality gates.
   `Curl.Protocol.Http.UnitLibrary` references it (ADR-0120) and drives it over
   `IConnection`; nothing uses `HttpClient` or the BCL's internal HTTP/2.
2. **`--http2`, `--http2-prior-knowledge`, `--http1.1` and `--http1.0` are accepted on
   every platform**, and each does what the measured nghttp2 builds do:
   - `--http2` on `https://` offers ALPN `h2,http/1.1` and speaks HTTP/2 if the server
     picks `h2`, HTTP/1.1 otherwise;
   - `--http2` on `http://` sends the HTTP/1.1 request with `Upgrade: h2c`,
     `HTTP2-Settings` and `Connection: Upgrade, HTTP2-Settings`, and switches to HTTP/2
     only on `101 Switching Protocols`;
   - `--http2-prior-knowledge` on `http://` sends the client preface straight away with
     no upgrade, and on `https://` offers ALPN `h2` alone;
   - `--http1.1` and `--http1.0` offer ALPN `http/1.1` alone.
3. **Default ALPN for a plain `https://`:**
   - **Linux and macOS: `h2,http/1.1`**, as the platform's curl (OpenSSL with nghttp2)
     offers.
   - **Windows: `http/1.1` alone**, as the Schannel reference build offers. The standing
     rule is to match the Windows reference wherever it does the same thing, and a
     plain `https://` transfer is something it does: a script that never asks for
     HTTP/2 sees the same ALPN offer, the same `-v` lines, the same `HTTP/1.1` status
     line in `-i` output and the same `%{http_version}` of `1.1` it sees today. HTTP/2
     on Windows is opt-in, through `--http2` or `--http2-prior-knowledge`, which the
     reference cannot do and where Curl therefore follows curl.se's Windows build.
4. **Whose text:** where both builds of a platform do the same thing, the platform's
   reference text is kept (Schannel on Windows, OpenSSL on Linux and macOS). HTTP/2 lines
   on every platform are libcurl's nghttp2-layer lines as measured above, which are the
   same on curl.se's Windows build and on Linux. TLS lines stay those of the platform's
   TLS backend, as today.
5. **`curl -V` lists `HTTP2` under `Features:` on every platform** once `--http2` is
   accepted (BL-659), because it is then true; a script that probes `-V` for `HTTP2` and
   then passes `--http2` must succeed. On Windows this is the one `-V` difference from
   the reference, and it matches curl.se's Windows build.
6. **Until BL-659 lands**, `--http2` and `--http2-prior-knowledge` keep ADR-0017's
   refusal (exit 2, `the installed libcurl version does not support this`), as ADR-0137
   does for every real option not yet implemented. The refusal goes in the same change
   that makes the options work; it is never replaced by a silent HTTP/1.1.
7. HTTP/3 (`--http3`, `--http3-only`) is outside this ADR; BL-718 decides it and
   supersedes the rest of ADR-0017.

## Consequences

Good:

- Linux and macOS scripts that pass `--http2`, or that rely on `h2` being negotiated by
  default, behave as they do with the platform's curl.
- Windows scripts that never ask for HTTP/2 see no change at all; those that do ask get
  HTTP/2 instead of exit 2.
- Everything is testable off the network: HPACK and frames are pure byte transforms, and
  the HTTP/2 transfer runs over `IConnection`.

Costs:

- A large, security-sensitive piece of hand-written code (HPACK with its Huffman table,
  flow control, stream state, `GOAWAY` and `RST_STREAM` handling).
- On Windows, `curl -V` lists `HTTP2` where the reference does not, and the default ALPN
  there differs from curl.se's Windows build (`h2,http/1.1`). The reference build wins
  the default because it is the build Windows ships.
- The default ALPN now depends on the platform, so HTTP tests that pin it need a test per
  platform (`[OSCondition]`), as the root `CLAUDE.md` requires.

The work, each its own task:

- BL-715 — create `Curl.Http2.UnitLibrary` and `Curl.Http2.UnitTests`.
- BL-656 — encode and decode HTTP/2 header blocks with HPACK.
- BL-657 — read and write HTTP/2 frames, with the connection preface and `SETTINGS`.
- BL-658 — send an HTTP/2 request and read its response on a stream.
- BL-659 — negotiate `h2` with ALPN (the per-platform default above) and accept
  `--http2` and `--http2-prior-knowledge`, listing `HTTP2` in `-V`.
- BL-660 — write curl's `-v`, `-i` and `%{http_version}` output for HTTP/2 transfers.
- BL-716 — upgrade a cleartext HTTP/1.1 request to `h2c` for `--http2`.
- BL-717 — multiplex `-Z` parallel transfers to one origin over one HTTP/2 connection.

## Alternatives considered

- **Offer `h2,http/1.1` by default on Windows too, as curl.se's build does.** Rejected:
  the Schannel reference is the curl Windows ships and the one scripts on Windows were
  written against; changing the protocol of every plain `https://` transfer there would
  change `-v`, `-i` and `%{http_version}` output for scripts that never asked for it.
- **Keep refusing `--http2` on Windows, as the reference does.** Rejected by the standing
  rule: an official curl build supports it, so Curl does on every platform.
- **Use `HttpClient` or the BCL's internal HTTP/2.** Rejected for ADR-0017's reasons:
  handlers may not construct `HttpClient`, it cannot run over `IConnection`, and it
  would not reproduce curl's bytes, `-v` lines or timings.
- **Leave `HTTP2` out of `-V` on Windows to match the reference.** Rejected: `-V` would
  then claim a missing feature that works, and a script that checks `-V` before passing
  `--http2` would skip HTTP/2 needlessly.
