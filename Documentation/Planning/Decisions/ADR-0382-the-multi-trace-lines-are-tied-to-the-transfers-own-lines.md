# ADR-0382: The [MULTI] trace lines are tied to the transfer's own lines

- Status: Accepted
- Date: 2026-10-02
- Task: BL-1188
- Decided by Claude under Stewart's delegation.

## Context

Under `-v --trace-config multi` (and `network`, `all`, `-vvvv`) curl 8.21.0 writes `[MULTI]` lines
from its multi state machine as a transfer moves from `[INIT]` through `[CONNECT]`, `[CONNECTING]`,
`[PROTOCONNECT]`, `[DO]`, `[DID]`, `[PERFORMING]` and `[DONE]` to `[COMPLETED]` and `[MSGSENT]`
(measured 2026-10-02, BL-1188 Notes). Curl has no multi state machine: a handler runs a transfer
from start to end in one call. Three of the lines' values are volatile: the `[PGRS-*] added <n>ns`
stamps, the poll lines' socket descriptor, and the connection number of `[CPOOL] added connection`.

## Decision

1. `Curl.Console`'s `MultiStateTraceEvents` writes the lines, one decorator over the transfer's
   events like the `[READ]` and `[WRITE]` ones (ADR-0357's amendments). The runner writes the seven
   lines up to `[SETUP] -> [CONNECT]` itself as the transfer starts, before `[READ] client_reset`,
   with `[<xfer>-x]` under `--trace-ids`, as curl does.
2. Each later group of lines is tied to the line or event curl writes it beside: the connection's
   `[SETUP] added` or `[DNS] created`, `[DNS] cf_dns_start`, `[SETUP] happy eyeballing`,
   `[HAPPY-EYEBALLS] init ip ballers`, `Trying`, `[TCP] connected on fd=`, the connection opened
   event (`Established connection`), `[TCP] query ALPN` or `using HTTP/`, `Request completely sent
   off`, the first response header, `[WRITE] [OUT] done` or `[READ] client_reset`, and the
   connection's `left intact` or `shutting down connection` line. A group whose line is not traced is
   written before the next group whose line comes, so the order matches curl's with `multi` alone
   and with every other component on. The decorator sits inside the `[READ]` and `[WRITE]` ones so it
   sees their lines.
3. The `[PGRS-*] added` numbers are the microseconds since the transfer's events were set up, on the
   runner's `TimeProvider`: curl's numbers are microseconds too, despite the `ns`. They are not curl's
   values and cannot be; tests pin them with a clock that stands still.
4. The poll lines name the descriptor of the `[TCP] connected on fd=` line when `[TCP]` is traced,
   so the two agree, and `3` otherwise, the first descriptor `[TCP]` hands out.
5. `[CPOOL] added connection N` takes the transfer's connection number (`%{conn_id}`) as the first
   connect line comes. A transfer whose connection is reused writes none of the connect groups.
6. `mid=1`, `total=2` and `transfer credentials: -` are written as measured: curl's tool reuses one
   multi handle per serial transfer and measured `mid=1` for each of two URLs.

## Consequences

- A plain HTTP transfer writes curl's `[MULTI]` lines in curl's order. A failed connect (curl's
  `connect failed -> 7`, `multi_done: status: 7 prem: 1` and `terminating conn` lines), a reused
  connection's own lines, a retried connection and other schemes' state changes are follow-up work
  (filed from BL-1188); until then they write the groups their lines reach and no closing lines.
- `[MULTI] [DONE] multi_done_locked` carries the connection's id under `--trace-ids`, where curl
  writes `[<xfer>-x]`.
