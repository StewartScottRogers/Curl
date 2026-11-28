# ADR-1466: The connect target tells the connector that -f fails on a refused CONNECT's status

- Status: Accepted
- Date: 2026-11-27
- Decided by Claude under Stewart's delegation (BL-6049)

## Context

After a refused CONNECT's reply head, `curl -v` writes `* CONNECT tunnel failed, response N`.
Under `-f` curl 8.22.0 writes something else (measured with `Record-CurlExchange.ps1`, BL-6049
Notes): `* The requested URL returned error: N` for a status of 400 or above, and neither line
when the `407` drew `<scheme> authentication problem, ignoring.`. `--fail-with-body` keeps
`CONNECT tunnel failed`. The connector writes these lines, but only the handler knew the fail mode.

## Decision

`ConnectTarget` carries `FailsOnHttpErrorStatus` (libcurl's `CURLOPT_FAILONERROR`), `false` by
default, which the HTTP handler sets for `-f` only. `TcpConnector` picks the `-v` line from it and
from curl's authentication problem. The failed `ConnectResult` is unchanged: its message stays
`CONNECT tunnel failed, response N` and the handler still decides the exit 22 text (ADR-1465).

## Why

The line belongs where the reply head is reported, in the connector, so a flag on the request to
connect is the smallest seam. Moving the line to the handler would make every other protocol
handler that tunnels write it too.

## Consequences

A handler that does not set the flag gets the lines as before. The HTTP/2 proxy tunnel writes no
line for a refused CONNECT, with or without `-f`, as before.
