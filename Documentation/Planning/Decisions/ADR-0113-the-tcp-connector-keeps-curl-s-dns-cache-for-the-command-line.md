# ADR-0113 — The TCP connector keeps curl's DNS cache for the command line

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-481.

## Context

curl 8.21.0 keeps a DNS cache for the whole command line and prints
`Hostname <host> was found in DNS cache` whenever a connect is answered from it. Curl printed
nothing, so every second connection to a host lacked the line (ADR-0112, Consequences).

Measured 2026-09-27 with curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`, `-s -v`,
two URLs, each response `HTTP/1.1 200 OK`, `Connection: close`, `Content-Length: 2`:

| Command line | Cache line |
| --- | --- |
| `http://127.0.0.1:P/a http://127.0.0.1:P/b` | before the second `Trying` only: an IP literal is cached too |
| `http://localhost:P/a http://localhost:P/b` | `Hostname localhost was found in DNS cache` before the second transfer's resolve |
| `http://127.0.0.1:P/a http://localhost:P/b` | none: another host |
| `http://127.0.0.1:1/a http://127.0.0.1:P/b` | none: another port |
| `http://127.0.0.1:1/a http://127.0.0.1:1/b`, both refused | before the second `Trying`: a failed dial still leaves the host cached |
| `http://nonexistent.invalid:P/a` twice | none: a host that did not resolve is not cached |
| `--resolve foo.example:P:127.0.0.1`, two URLs | before every `Trying`, the first included: `--resolve` fills the cache |
| `--connect-to 127.0.0.1:P2:127.0.0.1:P` with `/a` on P and `/b` on P2 | before the second: the key is the mapped host and port |
| `--connect-to foo.example:P:localhost:P`, two URLs | names `localhost`, the mapped host |
| `-x http://127.0.0.1:P`, two URLs | names the proxy host |

## Decision

- **`TcpConnector` keeps the cache**, one per connector (one per command line), keyed on
  the host and port being resolved, host compared ignoring case as DNS names are. It holds
  the addresses of every lookup that returned any; a lookup that returned none is not kept.
- **A `--resolve` entry or a cached key answers without `IDnsResolver`** and reports the line
  on the target's events before any `Trying`. The cached addresses are dialled again, as
  curl dials its cached entry rather than resolving afresh.
- **Every lookup goes through it**: the target (or its `--connect-to` destination), the
  proxy, and the target a SOCKS4 or SOCKS5 proxy resolves locally. The SOCKS case was not
  measured; curl resolves it through the same `Curl_resolv` that prints the line, so it is
  reported the same way.
- Curl does not print curl's `Host H:P was resolved.`, `IPv6:` and `IPv4:` lines, nor
  `Added H:P:A to DNS cache` for `--resolve`, so with a host name the cache line is
  followed directly by `Trying`; those lines are a separate gap.
- No time-out: curl's 60-second DNS cache lifetime is longer than a command line runs
  between two connects in practice, so entries are kept for the connector's life.

## Consequences

- A second fresh connection to a host on one command line prints curl's cache line, after
  the pool's `shutting down connection #N` or `seems to be dead` lines.
- A command line that connects to a name twice resolves it once.

## Alternatives considered

- **Track only whether the key was seen, and resolve again.** Prints the same line but
  re-resolves, which curl does not do, and could dial other addresses than curl.
- **Keep the cache in the DNS resolver.** `IDnsResolver` has no events to report on, and
  `--resolve` entries never reach it.
