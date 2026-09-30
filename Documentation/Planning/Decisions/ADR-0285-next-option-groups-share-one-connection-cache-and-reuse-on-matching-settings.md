# ADR-0285 — `-:`/`--next` option groups share one connection cache and reuse a connection when their settings match

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-754.
Amends ADR-0126 (each option group had a pool of its own) and ADR-0050 (the pool key).

## Context

ADR-0126 gave every option group its own `TransferDispatch`, and with it its own
`PoolingConnector`, so no connection was ever reused across groups. curl 8.21.0 shares one
connection cache across all groups and reuses a connection only when its TLS configuration
(`Curl_ssl_config_matches`) and its other connection settings agree.

Measured 2026-09-30 with `Record-CurlExchange.ps1 -HoldOpenMilliseconds 3000
-AnswerHeldRequests 1` (the new switch answers a second request on a held connection) against
curl 8.21.0 (Schannel), BL-754 Notes:

- `-v -w '[%{num_connects} %{conn_id}]\n' http://127.0.0.1:P/a --next -v -w ... http://127.0.0.1:P/b`:
  `GET /b` on A's connection after `* Reusing existing http: connection with host 127.0.0.1`,
  printing `ok[1 0]` then `ok[0 0]`.
- The same over `https` with `-k` in both groups: reused (`Reusing existing https:`).
- `-k` in the first group only: the second group opens connection `#1`
  (`Hostname 127.0.0.1 was found in DNS cache`, a new handshake), failing verification with
  exit 60 and `[1 1]`.

## Decision

1. `Curl.Networking` has a `ConnectionCache`: the idle and leased connections, the ones being
   negotiated, the connection numbering and the clock that `PoolingConnector` kept. A
   `PoolingConnector` built with `(inner, timeProvider)` makes and owns one, as before; one built
   with `(inner, cache, configuration)` shares the given cache and leaves closing it to its owner.
2. `ConnectionPoolKey` gains `Configuration`, the asking connector's configuration compared with
   `Equals`, so a connection is reused only by a connector with an equal configuration.
3. `Curl.Console` builds one `ConnectionCache` per run in `CurlComposition.CreateRunner`, gives each
   group's `PoolingConnector` that cache and the group's `OptionGroupConnectionSettings`, and the
   runner closes the cache once the run's transfers end (`runConnectionCache`).
4. `OptionGroupConnectionSettings` is what curl's connection match compares that a `ConnectTarget`
   does not already carry: the origin's and the HTTPS proxy's `TlsClientOptions` (every TLS
   option, `-k` included), `--connect-to`, `--unix-socket`/`--abstract-unix-socket`,
   `--interface`/`--local-port`, `-4`/`-6`, the HAProxy PROXY line, `--preproxy` and the ALPN list
   the HTTP version asks for. The proxy and its credential are already in the key (ADR-0050).

Each group keeps its own inner `TcpConnector`, so a new connection is still opened with the
group's own resolver options, `--resolve` entries and timeouts. Connection numbers now count
across the whole run, as curl's `#N` does.

## Consequences

- A later group with the same settings sends on an earlier group's idle connection, `-v` prints
  curl's reuse line and `%{num_connects}` is `0`.
- The DNS cache is still per group, so the `Hostname ... was found in DNS cache` line curl prints
  for the second group is not printed; and the runner's `%{conn_id}` counts every transfer that
  connected rather than naming the reused connection, printing `1` where curl prints `0`, within
  one group as across groups. Both are follow-up tasks.
