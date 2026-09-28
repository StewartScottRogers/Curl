# ADR-0072 — Failure messages naming a host are cut to curl's 255-byte error buffer

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

libcurl formats a failure message into an error buffer of `CURL_ERROR_SIZE` (256) bytes, and
the tool prints `curl: (<code>) ` and that buffer. A message longer than 255 bytes is cut.
Measured against curl 8.21.0 (mingw, Schannel, the Windows reference, ADR-0018), BL-377:

| Command | curl 8.21.0 |
| --- | --- |
| `curl -sS http://<300 a's>/` | exit 6, `curl: (6) Could not resolve host: ` and the first 231 `a`s |
| `curl -sS http://<250 a's>/` | the same 231 `a`s: the cut is on the whole message, not the host |
| `curl -sS -x http://<300 a's>:3128 http://example.com/` | exit 5, `curl: (5) Could not resolve proxy: ` and the first 230 `a`s |

`Could not resolve host: ` is 24 bytes and `Could not resolve proxy: ` 25, so each message is 255.
Hosts of up to 65535 bytes reach the resolver; `Dns.GetHostAddressesAsync` throws
`ArgumentOutOfRangeException` for a name over 255 characters, which escaped `Program.Main`.

## Decision

- `SystemDnsResolver` reports a name `Dns` refuses as too long (`ArgumentOutOfRangeException`)
  as not resolved, an empty list, so the caller's exit 6 or 5 follows as for any other name.
- `Curl.Networking`'s resolve failures (`TcpConnector` for host and proxy, `UdpDatagramConnector`,
  and the SOCKS handshakes through `SocksProxyTunnel.CouldNotResolve`) cut their message to 255
  characters through `CurlErrorBuffer.Truncate`, where the message is made.
- The cut counts characters. A host that reaches the resolver is ASCII (IDN hosts are
  converted to punycode first), so characters are bytes there.

## Consequences

The messages that can realistically pass 255 bytes, the ones naming a host, now match curl
byte for byte.

**The general cut now lives in `Curl.Console` (BL-380, 2026-09-27).** `CurlCommandRunner`
formats every failed transfer's `curl: (<code>) ` line through `FormatTransferErrorLine`,
which cuts the message with `Curl.Console`'s own `CurlErrorBuffer.Truncate` to 255 UTF-8
bytes, whichever library produced it. Measured on curl 8.21.0 (Schannel):

| Command | curl 8.21.0 |
| --- | --- |
| `curl -sS file:///nodir/<300 a's>` | exit 37, `curl: (37) Could not open file /nodir/` and the first 228 `a`s (255 message bytes) |
| `curl -sS http://x/<300 a's>[1-` | exit 3, `curl: (3) bad range in position 313:` and the whole URL: not cut |

- A bad glob's lines are formatted by curl's tool, not into libcurl's error buffer, so they
  are printed whole; only the transfer-failure line is cut.
- The cut counts UTF-8 bytes. Non-ASCII text on the command line reaches curl 8.21.0's
  messages percent-encoded (`file:///nodir/xé` prints `/nodir/x%E9`, and an IDN host
  prints as punycode), so no measured message holds a multi-byte character. Where one
  would, a character the 255th byte falls inside is dropped whole, because a .NET string
  cannot hold part of one; curl would print its leading bytes.
- `Curl.Networking`'s cut at the source stays: cutting twice gives the same bytes, and its
  own tests pin the message a connector reports.

## Alternatives considered

- **Cut in `Curl.Console`, once, for every message.** The right final home, but `Curl.Console`
  was in use by another task (BL-329) when this was decided; filed as BL-380.
- **Check `host.Length > 255` before the lookup.** Duplicates the BCL's own limit; catching the
  exception it throws keeps the resolver in step with whatever `Dns` refuses.
