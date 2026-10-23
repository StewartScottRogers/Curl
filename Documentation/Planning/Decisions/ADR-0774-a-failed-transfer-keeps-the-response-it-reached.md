# ADR-0774: A failed transfer keeps the response it reached

- Status: Accepted
- Date: 2026-10-22
- Decided by Claude under Stewart's delegation (BL-3195)

## Context

curl 8.22.0 still reports `CURLINFO_RESPONSE_CODE`, `HEADER_SIZE`, `HTTP_VERSION`, `PRIMARY_IP` and
`PRIMARY_PORT` after the HTTP layer refuses a response head (a header line without a colon, a header line
past 100 KB, two `Location` headers), and answers `PRIMARY_PORT` -1 after a transfer that reached no peer.
Measured with `Record-CurlExchange.ps1` (BL-3195 Notes). Curl reported 0 and empty for all of them.

## Decision

- The HTTP head reader puts the status line it had read on the `HttpTransferException` it rethrows
  (`StatusLineBeforeFailure`), and the handler's failed outcome puts that line's code and version, and the
  head bytes accepted before the failure as the header size, on the failure's report when no whole head
  was read. The address and port were already on the report.
- `ManagedEasyTransfer.PrimaryPortOf` answers -1 for a failed transfer whose report gives no port, and
  `%{remote_port}`/`%{local_port}` read 0 or -1 alike as "no connection".
- Out of scope, measured and left as it is: upstream answers -1 for `PRIMARY_PORT` and `LOCAL_PORT` after a
  successful transfer with no connection (`file://`) too; the engine still answers 0 there.

## Consequences

`-w` and `curl_easy_getinfo` after these failures match curl 8.22.0. The fix sits where the information is
known (the HTTP handler's head reader) rather than re-parsing header lines in the engine.
