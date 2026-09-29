# ADR-0017 — No HTTP/2 or HTTP/3 in Milestone 1: `--http2`, `--http2-prior-knowledge` and `--http3` are refused as the reference build refuses them

- **Status:** Superseded by ADR-0141 (HTTP/2) and ADR-0144 (HTTP/3)
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

`Documentation/Product/Product-Overview.md`, "Open questions", row 5 asked: "HTTP/2
and HTTP/3: BCL framing, or implemented over the seam like everything else?" The
Phase 1 HTTP plan (protocol-architect, 2026-09-26, item D2) needs the answer before
the option parser (BL-191) and the `-V` output (BL-155) can be written.

The forces:

- **The reference build has no HTTP/2.** The root `CLAUDE.md` says to match the
  platform's curl, the Schannel build on Windows. Neither Windows build of curl 8.21.0
  lists `HTTP2` or `HTTP3` under `Features:`, and both refuse the options. Measured on
  `/mingw64/bin/curl` (`curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel …`),
  each exiting 2, the exit code curl uses for a command-line option it cannot honour:

  ```
  curl: option --http2: the installed libcurl version does not support this
  curl: try 'curl --help' or 'curl --manual' for more information
  ```

  ```
  curl: option --http2-prior-knowledge: the installed libcurl version does not support this
  curl: try 'curl --help' or 'curl --manual' for more information
  ```

  ```
  curl: option --http3: the installed libcurl version does not support this
  curl: try 'curl --help' or 'curl --manual' for more information
  ```

  `C:\Windows\System32\curl.exe` (`curl 8.21.0 (Windows) libcurl/8.21.0 Schannel …`)
  prints the same first line for each option and exits 2, but its second line is
  `curl: try 'curl --help' for more information`, because that build has no built-in
  manual. `--http3-only` is refused the same way on both builds.
- **The BCL has no public HTTP/2 building blocks.** .NET 10's HPACK encoder and
  decoder and its HTTP/2 framing are internal to `System.Net.Http`. The only public
  route to HTTP/2 is `HttpClient`, which protocol handlers may not construct (they
  receive `IConnection`), and which would not reproduce curl's bytes, timings or
  `--write-out` values anyway. HTTP/2 here therefore means a hand-written HPACK and
  frame layer over `IConnection`; HTTP/3 additionally needs QUIC, which the BCL only
  offers through `System.Net.Quic` on platforms with `msquic`.
- **Milestone 1 proves HTTP/1.x.** Everything curl users send by default over
  `http://` is HTTP/1.1, and over `https://` the reference build negotiates HTTP/1.1
  as well, because it offers no `h2` in ALPN.

## Decision

1. Milestone 1 has no HTTP/2 and no HTTP/3. `Curl.Protocol.Http.UnitLibrary` speaks
   HTTP/1.0 and HTTP/1.1 only, and offers only `http/1.1` in ALPN.
2. On every platform, `--http2`, `--http2-prior-knowledge`, `--http3` and
   `--http3-only` are refused exactly as the Windows reference build refuses them:
   exit code 2, the line `curl: option <option>: the installed libcurl version does
   not support this`, then the build's "try" line, on stderr. The option is parsed
   and refused; it is not reported as unknown.
3. `curl -V` lists neither `HTTP2` nor `HTTP3` under `Features:`.
4. A hand-written HTTP/2 over `IConnection` (HPACK and framing) is filed under the
   Roadmap's "Later / unscheduled"; HTTP/3 waits behind it.

## Consequences

Good:

- On Windows the behaviour is byte-identical to the reference build, so scripts that
  probe for HTTP/2 support see the same answer they see today.
- Milestone 1 avoids a large, security-sensitive piece of work (HPACK, flow control,
  stream multiplexing) that no Milestone 1 exit criterion needs.
- BL-191 and BL-155 can be written now against a fixed, measured behaviour.

Costs:

- **Known divergence on Linux and macOS.** The OpenSSL builds of curl usually link
  nghttp2 (and sometimes a QUIC stack), accept `--http2`, list `HTTP2` under
  `Features:`, and negotiate `h2` over TLS by default. On those platforms this
  replacement refuses `--http2` with exit 2 where the platform's curl would succeed,
  and talks HTTP/1.1 where the platform's curl would talk HTTP/2. A script that passes
  `--http2` on Linux breaks; one that does not still works, with HTTP/1.1 on the wire.
  This divergence stands until the Roadmap entry is scheduled.
- A later HTTP/2 must be written by hand, since the BCL exposes none of it.

## Alternatives considered

- **Use `HttpClient` for HTTP/2 and HTTP/3.** Rejected: protocol handlers may not
  construct an `HttpClient`, it cannot run over `IConnection` so it cannot be tested
  off the network, and it would not reproduce curl's request bytes, verbose output or
  `--write-out` timings.
- **Write HTTP/2 over `IConnection` in Milestone 1.** Rejected: the Windows reference
  build has no HTTP/2, so on the platform Milestone 1 is measured against it would be
  a divergence, not a feature; and it would delay Milestone 1 for work no exit
  criterion needs.
- **Accept `--http2` and silently use HTTP/1.1.** Rejected: neither the Windows nor the
  Linux curl behaves that way, and a script would be told HTTP/2 worked when it did
  not.
- **Report `--http2` as an unknown option.** Rejected: curl knows the option and says
  the library does not support it; the message and exit code would differ.
- **Match the Linux build's acceptance of `--http2` on Linux and macOS only.**
  Rejected for now: there is no HTTP/2 to accept it with, so accepting it would be the
  silent-downgrade alternative on those platforms.
