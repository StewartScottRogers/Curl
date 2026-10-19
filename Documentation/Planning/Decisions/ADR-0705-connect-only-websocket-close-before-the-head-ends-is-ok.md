# ADR-0705: A connect-only WebSocket perform whose server closes before the head ends answers Ok

- Status: Accepted
- Date: 2026-10-19
- Decided by Claude under Stewart's delegation
- Task: BL-2712

## Context

Upstream test2304 (lib2304) performs a `CURLOPT_CONNECT_ONLY` 2 WebSocket transfer against a server
that sends an LF-only `101` head with no blank line (`nonewline`) and closes. Upstream expects exit 0:
curl 8.22.0's `Curl_http_done` raises `CURLE_GOT_NOTHING` ("Empty reply from server") only when
`!data->set.connect_only`, so the perform succeeds with no upgraded connection, and the program's
`curl_ws_send` of a PING fails and ends its rounds. Curl's `WsUpgradeResponseReader` fails such a close
with 52, as measured for the curl tool (BL-580), and the connect-only handler passed that on.

## Decision

`Curl.Engine`'s `WebSocketUpgradeOnlyHandler` turns a `GotNothing` outcome of
`WsProtocolHandler.ConnectAndUpgradeOnlyAsync` into success with the outcome's report and no upgraded
connection, so `curl_ws_recv` and `curl_ws_send` then answer `CURLE_BAD_FUNCTION_ARGUMENT`. The curl
tool's transfer (`ExecuteAsync`) keeps its exit 52. The change sits in the engine, not the WebSocket
library, because connect-only is libcurl's notion and `WsProtocolHandler` reports the same outcome the
tool's transfer ends with.

Left as they are: the `-v` lines still include `Empty reply from server` and `closing connection`,
which upstream's verbose output for this case does not pin (test2304 checks only the request and the
exit code).
