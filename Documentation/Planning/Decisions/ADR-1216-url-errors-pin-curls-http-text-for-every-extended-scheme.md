# ADR-1216: URL errors pin curl's `http://` text for every extended scheme

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4612

Decided by Claude under Stewart's delegation.

## Context

BL-4612 checks that every extended scheme ends a malformed URL with exit 3, an unresolvable host with exit 6 and a
refused connection with exit 7 (curl's manual, "EXIT CODES", curl 8.22.0;
`Documentation/Conformance/Extended/CurlSurface-Transfer-UrlErrors.md`). Real curl does not serve these schemes, so
it has no text of its own for them, and some schemes do not name a server in their host.

## Decision

- The cases pin the exit code and the whole standard error. The text is what curl 8.22.0 writes for an `http://` URL
  of the same shape, measured with `Record-CurlExchange.ps1`: `URL rejected: Port number was not a decimal number
  between 0 and 65535`, `URL rejected: Bad hostname` (a `%` not followed by two hex digits in the host),
  `URL rejected: Malformed input to a URL function` (a space), `Could not resolve host: <host>` and
  `Failed to connect to <host>:<port> after 0 ms: Could not connect to server` on a clock that never moves. An
  extension that adds to that text is a defect (BL-4653 for `oci`), since a script reading curl's errors cannot tell
  the schemes apart otherwise.
- `s3` and `gs` reach exit 6 and 7 through the endpoint their variables name (`AWS_ENDPOINT_URL`,
  `STORAGE_EMULATOR_HOST`), since their host is a bucket. `az`, `abfs` and `abfss` reach exit 7 on the account's
  `*.core.windows.net` host; their exit 6 rows are `n/a`, since that host is never under `.invalid` (ADR-1214) and no
  variable names another endpoint.
- Over UDP (`coap`, `coaps`, `dns`, `webtransport`) a refused connection pins exit 7 with the harness's datagram
  connector's reason, since no socket exists to report one. A closed UDP port on a real Windows socket ends with exit 2
  today; BL-4654 measures and fixes that.
- `mcp+stdio` and a `curl-proto-<scheme>` helper scheme (checked as `demo`) check only exit 3: curl resolves and
  connects to nothing for them (ADR-1184, ADR-1142), so their exit 6 and 7 rows are `n/a`. Each case also asserts that
  no program starts.

## Consequences

Every scheme's URL errors read the same as curl's for `http://`. A new extended scheme adds one case file in
`CurlSurface/Transfer/UrlErrors/` and its rows in the matrix.
