# ADR-0114 — The TCP connector loads `--resolve` entries per transfer and reports what it resolved

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-482.
Amends ADR-0113, whose Decision listed these lines as a separate gap.

## Context

With `-v`, curl 8.21.0 prints three lines after every lookup it answers, from the DNS or
its cache, and a line for every `--resolve` entry it loads. Curl printed none of them.

Measured 2026-09-27 with curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`, `-s -v`,
each response `HTTP/1.1 200 OK`, `Connection: close`:

| Command line | Lines |
| --- | --- |
| `http://localhost:P/a http://localhost:P/b` | `Host localhost:P was resolved.`, `IPv6: ::1`, `IPv4: 127.0.0.1` before each transfer's `Trying`, the second after `Hostname localhost was found in DNS cache` |
| `http://127.0.0.1:P/a` | none of the three: the cached name is an IP address |
| `http://LocalHost:P/b` | `Host LocalHost:P was resolved.`: the name as looked up |
| `--resolve foo.example:P:127.0.0.1`, two URLs | `Added foo.example:P:127.0.0.1 to DNS cache` first in each transfer; the second transfer's is preceded by `RESOLVE foo.example:P - old addresses discarded` |
| `--resolve foo.example:P:127.0.0.1,127.0.0.2,[::1]` | the address list verbatim in `Added`; `IPv6: ::1`, `IPv4: 127.0.0.1, 127.0.0.2` |
| `--resolve +*:P:127.0.0.1`, URL `http://127.0.0.1:P/` | `Added *:P:127.0.0.1 to DNS cache (non-permanent)`, `RESOLVE *:P using wildcard`, then `Host *:P was resolved.` |
| `--resolve FOO.example:P:127.0.0.1`, URL `http://foo.EXAMPLE:P/` | `Host FOO.example:P was resolved.`: the entry's host as written |
| `--resolve FOO.example:P:A --resolve -foo.example:P --resolve bar.example:P:A`, two URLs | the removal prints nothing; the second transfer discards `bar.example` only |
| `--resolve Foo.Example:P:A --resolve bad` | `Added` for the first, then exit 49 |
| `-L --resolve foo.example:P:A`, a 302 to the same host on a fresh connection | `Added` once: a followed redirect does not load the entries again |

## Decision

- **`ResolveOverrides.Entries`** keeps the entries that parsed, in order, as `ResolveEntry`
  (host, port, the address list verbatim, addresses, removal, `+`).
- **`TcpConnector.LoadResolveEntries(events)` loads them into its DNS cache**, reporting
  the `Added` and `discarded` lines; it is for the console to call at the start of every
  transfer, not for a followed redirect. Nothing in a `ConnectTarget` marks a new transfer:
  the console hands every transfer of a run the same `ITransferEvents`, so the connector
  cannot tell the transfers apart by itself.
- **Until a transfer calls it, `ConnectAsync` loads them itself, once**, on its target's
  events, before an entry or a `--connect-to` mapping that did not parse fails the connect,
  as curl loads before it fails. So the first transfer prints curl's lines, and the
  entries answer lookups whether or not the console calls the method.
- Each cache entry keeps the name it was cached under, so `Host <name>:<port> was resolved.`
  names the entry's host as written, `*` for a wildcard, or the host as looked up.
  `IPv6:` and `IPv4:` list that family's addresses in order joined by `, `, or `(none)`.
  A name that parses as an IP address prints none of the three.

## Consequences

- Until the console calls `LoadResolveEntries` at each transfer's start (BL-483), only the
  first transfer prints the `Added` lines, and no transfer prints `discarded`.
- Called from the console, it reports for a transfer whose connection is reused from the
  pool too, as curl does.
- On a first transfer that the console has not preceded with the call, lines it prints
  before the connector runs come before `Added`, where curl prints `Added` first.

## Alternatives considered

- **Load on every connect.** Prints `Added` again for a followed redirect, which curl does not.
- **Load when a connect carries other `ITransferEvents` than the one before.** Would need no
  console change, but the console shares one events object across the run, so it loads once.
- **A per-transfer marker on `ConnectTarget`.** Changes the shared contract in
  `Curl.Protocol.Abstractions.UnitLibrary` for what one method call on the connector does.
