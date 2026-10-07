---
id: BL-959
title: Print curl's Issue another request line before each authentication retry
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-844]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-959 — Print curl's Issue another request line before each authentication retry

## Goal

Under `-v`, every request `HttpProtocolHandler` sends again in answer to a 401 or 407 is preceded by curl 8.21.0's `* Issue another request to this URL: '<url>'` line, right after the challenge's `Connection #N to host ... left intact`, and followed by `Reusing existing http: connection with host ...` when the connection is reused.

## Context

- Measured 2026-09-29 (ADR-0232): `--anyauth -u : -v` against `401` + `WWW-Authenticate: Negotiate` writes, after the first 401's head, `* Connection #0 to host 127.0.0.1:48844 left intact`, `* Issue another request to this URL: 'http://127.0.0.1:48844/'`, `* Reusing existing http: connection with host 127.0.0.1`, then the second request's lines. Curl writes neither the `Issue another request` line nor (in the handler's events) the reuse line before an authentication retry, for any scheme; today only a request resent after its connection died writes the first (`HttpProtocolHandler.ReportConnectionEnd`, `HttpConnectionInfoLines.IssueAnotherRequest`).
- `HttpProtocolHandlerTests.NegotiateAnyAuth.cs` (BL-844) pins the `-v` lines without these two; add them there once written. Measure Digest and NTLM retries with `Record-CurlExchange.ps1` (`-HoldOpenMilliseconds` keeps the connection for reuse) before pinning them.
- Related: BL-907 does the same line for followed `-L` redirects in `Curl.Core.UnitLibrary`.

## Acceptance criteria

- [x] `HttpProtocolHandlerTests` pins `* Issue another request to this URL: '...'` before the retry of a Digest 401, an NTLM 401 and the `--anyauth` Negotiate 401 of BL-844, in the measured order.
- [x] The reuse line, however it is written for `-v` (handler info line or `ReportConnectionReused` rendered by the console), appears in the measured place for a retry on the same connection.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- **Measured 2026-09-29**, Windows reference curl 8.21.0 (Schannel), `Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500 -CurlArgs --digest|--ntlm,-u,u:p,-v,-s,-m,5,http://127.0.0.1:48853/a` against `401` + `WWW-Authenticate: Digest realm="r", nonce="abc"` (or `NTLM`) + `Content-Length: 4` + `deny`, the connection held open. After the 401's `< ` line, both schemes write, in this order: `* Connection #0 to host 127.0.0.1:48853 left intact`, `* Issue another request to this URL: 'http://127.0.0.1:48853/a'`, `* Reusing existing http: connection with host 127.0.0.1`, `* Server auth using Digest|NTLM with user 'u'`, then the retry's head, with no `using HTTP/1.x` line. (Curl then timed out waiting for the held connection's second response; that is the recorder's doing.)
- **Also measured:** a `417` answered while waiting for `100-continue` (`-H "Expect: 100-continue" --data-binary @2000-byte-file`, held open) writes the same three lines before the resend without `Expect`. The lines belong to curl's "another request on this connection" path, not to authentication, so `HttpProtocolHandler.ReportRetryOnSameConnection` writes them before every retry `ExchangeWithRetriesAsync` sends on the same connection: a 401, a 407, a 417. A 407 through a forward proxy was not measured; it takes the same libcurl path and is reported, as `PoolingConnector` reports a pooled proxy connection, `with proxy <proxy host>`.
- **Before this task** the handler wrote none of the three lines between an exchange and its same-connection retry: the `left intact` line only came once, when the connection was disposed. It now also comes before each such retry, as in curl.
- **How the reuse line is written:** as `ITransferEvents.ReportConnectionReused`, which the console already renders as `Reusing existing <scheme>: connection with host|proxy <name>` (ADR-0050), built exactly as `PoolingConnector.Reuse` builds it. No new text in the handler.
- **Tests:** `HttpProtocolHandlerTests.AuthRetryVerbose.cs` pins the Digest and NTLM order and the event's fields for a direct connection, a SOCKS tunnel and a forward proxy's 407. `HttpProtocolHandlerTests.NegotiateAnyAuth.cs` (BL-844) now pins its full `-v` sequence with the three lines and the closing `left intact`. The test fake `RecordingTransferEvents` now records reuse events (`Reused`, and as a `* Reusing existing ...` line in `Events`).
- No ADR: this matches measured curl and makes no new design choice.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. -v now writes left intact, Issue another request and Reusing existing before every same-connection retry (401, 407, 417), as curl 8.21.0 does
