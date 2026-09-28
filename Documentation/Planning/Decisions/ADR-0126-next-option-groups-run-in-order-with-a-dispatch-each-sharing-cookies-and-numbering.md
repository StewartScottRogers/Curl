# ADR-0126 — `--next` option groups run in order, each with its own dispatch, sharing the cookie store and the transfer numbering

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-509.

## Context

ADR-0125 made the parser return the `-:`/`--next` option groups; BL-509 runs them. BL-509
measured curl 8.21.0 (Windows, `Record-CurlExchange.ps1 -Connections 3`; the bytes are in
BL-509's Notes) and found:

- Each group's URLs go out with that group's options: `-d a A --next B` posts A and gets B.
- The exit code is the last transfer's: a failing first group followed by a good one exits 0.
  `--fail-early`, being global, stops the whole run at the first failure wherever it is given.
- `%{urlnum}`, `%{xfer_id}` and `%{conn_id}` count on across groups (`A B --next C` gives
  `0 0 0`, `1 1 1`, `2 2 2`).
- The cookie list is shared, but each group's cookie engine is its own: `-c j1 A --next -c j2 B`
  sends A's cookie to B and writes both to `j2`; `-c j A --next B` sends B nothing; a group with
  only `-b name=value` sends only its string.
- Each group opens its own `-D` file: `-D h A --next -D h B` leaves only B's head in `h`.
- On Windows, a `-w` line feed stays LF when a later group writes its body to standard output.
- A request-method conflict (`-I -d`) in a later group is reported after the earlier groups run,
  and nothing after it runs.
- curl tries to reuse a connection an earlier group left open.

The runner's transports (TLS settings, `--resolve`, `--connect-to`, the proxy tunnel, the
socket options) are built from one group's options, and `ConnectionPoolKey` leaves the TLS
settings out because one run had one set.

## Decision

1. `CurlCommandRunner` runs `CommandLineParseResult.Groups` in order, up to and including the
   first group with an output option left over. Each group gets its own `TransferDispatch` from
   the factory, built from its own options and disposed when the group ends. So each group's TLS,
   proxy and `--resolve` settings are exactly its own.
2. The run's `%{xfer_id}` and `%{conn_id}` counters are fields of the runner and carry on.
   `UrlTransfer.UrlNumber` is the run-wide `%{urlnum}`, and `UrlIndex` stays the position in the
   group that pairs a URL with its output. The `-v`/trace output opens once, at the run's first
   URL, and closes after the last group.
3. `CurlComposition.SharingRunCookies` gives the factory one `CookieStore` per run. Each group's
   `CookieEngine` keeps its own `-b` files, `-b` strings, `-c` jar and `-j` over that store.
   `CookieStore.GetCookieHeader` gained an overload that takes the strings to send, so a group's
   strings never reach another group.
4. A group's request-method conflict writes its lines and exits 2 once the groups before it
   have run.

## Consequences

- A single-group command line runs exactly as before: one dispatch, one pool.
- Connections are not reused across groups yet, so a later group to the same server opens a new
  connection where curl 8.21.0 may reuse one: `%{num_connects}` and the `-v` lines differ in
  that case. BL-754 shares the pool once its key carries the TLS and proxy configuration.

## Alternatives considered

- **One dispatch for the whole run, built from the first group.** It would share the pool but
  apply the first group's TLS, proxy and `--resolve` settings to every group, so `-k` in one group
  would leak into another. Correct TLS behaviour matters more than connection reuse.
- **Rebuild the dispatch only when a group's transport options differ.** Deciding "differ" means
  listing every transport-affecting option by hand, and a missed option silently shares the
  wrong settings. BL-754 does it properly in the pool key instead.
