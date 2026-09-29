# ADR-0169 — The RTSP library writes its own `OPTIONS` request and reads its own reply, with CSeq checked per transfer

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-590.

## Context

RTSP/1.0 (RFC 2326) borrows HTTP/1.1's message syntax: a request line, header lines, a blank
line, an optional body sized by `Content-Length`. `Curl.Protocol.Rtsp.UnitLibrary` may reference
`Curl.Protocol.Abstractions.UnitLibrary` and never `Curl.Protocol.Http.UnitLibrary`, and takes an
`IConnection`. The conformance audit of 2026-09-28 (row 38) found that `rtsp://` does nothing and
that exits 85 (`CURLE_RTSP_CSEQ_ERROR`) and 86 (`CURLE_RTSP_SESSION_ERROR`) are never produced.
This ADR fixes where the request and reply code lives, which requests the curl tool makes and
which options reach them, and how `CSeq` and `Session` are tracked. BL-591 to BL-593 build on it.
ADR-0128 answered the same question for WebSocket; this follows it, and the one difference (no
request method is ever chosen by the user) comes from the measurements below.

### Measurements

curl 8.21.0 (Windows, Schannel build, Git for Windows' mingw64 — Windows' own `curl.exe` has no
`rtsp`), recorded with `Record-CurlExchange.ps1` in HTTP mode on ports 47950-47961. `OK` is the
reply `RTSP/1.0 200 OK\r\nCSeq: 1\r\nPublic: OPTIONS, DESCRIBE\r\n\r\n`; `OK1` is
`RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n`. The progress meter is left out of stderr.

The request curl sends for `curl rtsp://127.0.0.1:47950/media`:

```
OPTIONS * RTSP/1.0\r\n
CSeq: 1\r\n
User-Agent: curl/8.21.0\r\n
\r\n
```

| # | Arguments / reply | Request (differences from the one above) | stdout | stderr | Exit |
| --- | --- | --- | --- | --- | --- |
| 1 | `rtsp://…/media`; `OK` | — | empty | empty | 0 |
| 2 | `-X DESCRIBE`; reply with `Content-Type: application/sdp`, `Content-Length: 5`, body `v=0\r\n` | **none: still `OPTIONS *`** | empty (body not written) | empty | 0 |
| 3 | `-X SETUP`, `-X PLAY`, `-X FOO`, each with `OK` | none: still `OPTIONS *` | empty | empty | 0 |
| 4 | `-I`; `OK` | — | the reply head, byte for byte | empty | 0 |
| 5 | `-i`; `RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 2\r\n\r\nok` | — | the reply head only; **`ok` not written** | empty | 0 |
| 6 | `-o -`; the reply of row 2 | — | empty | empty | 0 |
| 7 | `-H 'X-Test: 1' -A agent/1`; `OK` | `User-Agent: agent/1`, then `X-Test: 1` | empty | empty | 0 |
| 8 | `-u u:p`; `OK` | `Authorization: Basic dTpw` after `User-Agent` | empty | empty | 0 |
| 9 | `-e http://r/ -b a=b --compressed --request-target /x`; `OK1` | `Referer: http://r/` between `CSeq` and `User-Agent`; no `Cookie`, no `Accept-Encoding`, target still `*` | empty | empty | 0 |
| 10 | `-v -s -X DESCRIBE -H 'Accept: application/sdp' -d abc`; `OK1` | `Accept: application/sdp` after `User-Agent`; method `OPTIONS`, no body | empty | see below | 0 |
| 11 | `-d abc`; `OK` | none: no body | empty | empty | 0 |
| 12 | `-v`; `OK` | — | empty | see below | 0 |
| 13 | `RTSP/1.0 200 OK\r\nCSeq: 7\r\n\r\n` | — | empty | `curl: (85) The CSeq of this request 1 did not match the response 7` | 85 |
| 14 | `RTSP/1.0 200 OK\r\n\r\n` (no `CSeq`) | — | empty | `curl: (85) The CSeq of this request 1 did not match the response 0` | 85 |
| 15 | `RTSP/1.0 200 OK\r\nCSeq: 1\r\nSession: 1234;timeout=60\r\n\r\n` | — | empty | empty | 0 |
| 16 | `RTSP/1.0 404 Not Found\r\nCSeq: 1\r\n\r\n` | — | empty | empty | 0 |
| 17 | `-sSf`; the reply of row 16 | — | empty | `curl: (22) The requested URL returned error: 404` | 22 |
| 18 | `-sS`; `HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n` | — | empty | `curl: (52) Empty reply from server` | 52 |
| 19 | `-s -w '%{http_code} %{response_code} %{size_header}'`; `OK1` | — | `200 200 28` | empty | 0 |
| 20 | `rtsp://…/a rtsp://…/b`, one connection answered with row 15's reply then held open 1.5 s | second request on the **same connection**: `OPTIONS * RTSP/1.0`, **`CSeq: 0`**, `User-Agent`; **no `Session`** | empty | `curl: (52) Empty reply from server` (the held connection never answers) | 52 |
| 21 | as 20 without holding: the server closes after the first reply | as 20 | empty | `curl: (56) Recv failure: Connection was aborted` | 56 |
| 22 | `-sS rtsp://127.0.0.1/media` (no port) | nothing: connects to port 554 | empty | `curl: (7) Failed to connect to 127.0.0.1:554 after 2041 ms: Could not connect to server` | 7 |

`-v` for row 12 writes, around the usual connect lines:

```
> OPTIONS * RTSP/1.0
> CSeq: 1
> User-Agent: curl/8.21.0
> 
* Request completely sent off
< RTSP/1.0 200 OK
< CSeq: 1
< Public: OPTIONS, DESCRIBE
< 
* Connection #0 to host 127.0.0.1:47950 left intact
```

With `-f` (row 17, `-v`), after `< CSeq: 1`: `* The requested URL returned error: 404`, `< `,
`* closing connection #0`. Row 20 with `-v` adds `* Reusing existing rtsp: connection with host
127.0.0.1` before the second request's `>` lines.

What the table shows:

- The tool makes exactly one request per URL, and it is always `OPTIONS *` (rows 1-3, 9, 10).
  The tool has no option that sets libcurl's `CURLOPT_RTSP_REQUEST`, and `-X` (libcurl's
  `CURLOPT_CUSTOMREQUEST`) does not change the RTSP method; `--request-target` does not change
  the `*` target. `DESCRIBE`, `SETUP`, `PLAY`, `PAUSE`, `TEARDOWN`, `GET_PARAMETER`,
  `SET_PARAMETER`, `ANNOUNCE`, `RECORD` and interleaved `RECEIVE` are libcurl-only and are never
  sent by the tool.
- Header order is the request line, `CSeq`, `Referer`, `User-Agent`, `Authorization`, then the
  `-H` headers (rows 7-10). This is libcurl's `rtsp.c` order (`CSeq`, `Session`, `Transport`,
  `Accept`, `Accept-Encoding`, `Range`, `Referer`, `User-Agent`, proxy then server
  authorization, custom headers); the entries before `Referer` are never produced by an
  `OPTIONS` from the tool.
- `-d`, `-b`, `--compressed` and `--request-target` have no effect (rows 9-11).
- The body of the reply is read and counted but never written, even with `-i` or `-o` (rows 2, 5,
  6). The head is written with `-i` or `-I` (rows 4, 5), so it goes to `HeaderOutput`.
- A reply `CSeq` that differs from the request's fails with 85, and a missing one counts as 0
  (rows 13, 14). A `Session` in the reply is accepted and not checked (row 15).
- The status code is not an error unless `-f` is given, which fails a status of 400 or more with
  22 and the HTTP text (rows 16, 17). A reply that does not start with `RTSP/` is an empty reply,
  52 (row 18). `%{http_code}` and `%{response_code}` are the RTSP status (row 19).
- Each URL is its own transfer with its own `CSeq` counter and session state, but the connection
  is reused: the second transfer's `CSeq` is `0` and it sends no `Session` even though the first
  reply carried one (rows 20, 21). A closed reused connection is not retried (row 21).
- The default port is 554 (row 22).

## Decision

**1. The request writer and the reply reader live in `Curl.Protocol.Rtsp.UnitLibrary`.**
`RtspRequestFormatter` writes a request head from the method, the target, the `CSeq`, an optional
session ID and the transfer's `HttpRequestOptions` (`Referer`, `UserAgent`, `Headers`,
`CommandLineTextEncoding`) and credentials, in libcurl's order above. `RtspReplyReader` reads the
status line and header lines up to the blank line from `IConnection`, keeps the raw head for
`HeaderOutput` and `-v`, takes the status code, `CSeq`, `Session` and `Content-Length`, and reads
and discards `Content-Length` body bytes. Nothing moves into Abstractions and nothing is shared
with the HTTP library, as in ADR-0128.

**2. The tool makes one `OPTIONS *` request per transfer.** The handler sends `OPTIONS * RTSP/1.0`
whatever `-X` and `--request-target` say. The method is still modelled as an `RtspMethod` value
that the formatter takes, so the libcurl-only requests can be added without reshaping the code
when the curl tool gains an option for them; until then no transfer produces any other value.

**3. Options that apply**, as measured:

| Option | On an RTSP transfer |
| --- | --- |
| `-A`, `-e`, `-H` | `User-Agent`, `Referer` and the custom headers, in the order above. |
| `-u`, `--basic`, `--oauth2-bearer` | Pre-emptive `Authorization` from the injected `IHttpAuthenticator` with no challenges, after `User-Agent`. |
| `-i`, `-I`, `-D` | The reply head, byte for byte, to `HeaderOutput`. `-I` changes nothing else. |
| `-f` | A status of 400 or more fails with 22, `The requested URL returned error: <code>`. |
| `-v`, `--trace`, `--trace-ascii` | Through `ITransferEvents` (ADR-0046), as measured above (BL-593). |
| `-w` | `%{http_code}` and `%{response_code}` are the RTSP status; `%{size_header}` the head's bytes. |
| `-m`, `--connect-timeout` | As for every transfer (ADR-0117, BL-498). |
| `%{local_ip}` and the other endpoint variables | From the connection, as for every transfer (ADR-0119, BL-515). |
| `-X`, `--request-target`, `-d` and the other body options, `-b`, `-c`, `--compressed`, `-T`, `-L` | No effect. |

**4. `CSeq` and `Session` are per transfer.** A transfer holds an `RtspSessionState`: the next
`CSeq` starts at 1 for the transfer's first request and goes up by one per request, and the
session ID starts empty. After each reply the handler compares the reply's `CSeq` (0 when
missing) with the one sent and fails a mismatch with exit 85 and
`The CSeq of this request <sent> did not match the response <received>`. A reply's `Session`
value (up to the first `;`) is kept when none is held, sent as `Session: <id>` after `CSeq` on
the transfer's later requests, and a later `Session` header naming a different ID fails with
exit 86 (RFC 2326 section 12.37). Because the tool makes one request per transfer, a later
request is not reachable from the command line, and BL-592 pins it at the handler level with a
transfer that makes two requests. Exit 86 is reachable: BL-592 measured that a single reply
carrying two `Session` headers with different IDs fails with
`Got RTSP Session ID Line [<rest of the line>], but wanted ID [<first ID>]`, and that a `-H`
header naming `Session` fails with 43, `Session ID cannot be set as a custom header.`; the
measurements are in BL-592's Notes.

**5. Connection reuse across URLs is matched.** When one command line names several `rtsp` URLs
on the same host and port, the second and later transfers reuse the connection (the generic
connection cache, as for HTTP) and send `CSeq: 0`, as measured in row 20: the counter of a
transfer that starts on a reused RTSP connection begins at 0, not 1. A reused connection the
server has closed fails with 56 and is not retried (row 21). If the connection cache is not yet
shared by protocol handlers, BL-593 files that as its own task rather than widening itself.

**6. Replies that are not RTSP** (no `RTSP/` status line) end the transfer with 52
`Empty reply from server`, matching row 18.

**7. Abstractions change: none.** `ITransferContext` already carries `Url`, `HeaderOutput`,
`Credentials`, `Http`, `MaxTime`, `Events` and `Progress`, and `CurlExitCode` already has
`RtspCseqError` (85) and `RtspSessionError` (86).

## Consequences

- BL-591 builds `RtspRequestFormatter`, `RtspReplyReader` and the handler's single `OPTIONS *`
  exchange: the measured request bytes and header order, the head to `HeaderOutput`, the body read
  and discarded, 85 for a wrong or missing `CSeq`, 22 under `-f`, 52 for a non-RTSP reply.
- BL-592 builds `RtspSessionState` (the `Session` header on later requests and 86 for a mismatch),
  pinning later requests at the handler level because the tool never sends a second request in
  one transfer.
- BL-593 registers the handler for `rtsp` with port 554 in `Curl.Console`, maps the options in the
  table above and no others, lists `rtsp` in `-V`, writes the `-v` lines above, and reuses the
  connection across URLs with `CSeq: 0` on the reused one.
- The request head is written in two places in the solution (HTTP and RTSP), as the WebSocket head
  already is (ADR-0128); a change to curl's shared header order has to be made in each.
- `-X DESCRIBE` doing nothing, and the body of a reply never reaching the output, are surprising
  but measured; tests pin them so no one "fixes" them.

## Alternatives considered

- **Honour `-X` as the RTSP method.** What a reader of the man page might expect, and what
  libcurl's `CURLOPT_RTSP_REQUEST` offers, but the curl 8.21.0 tool sends `OPTIONS *` whatever
  `-X` says (rows 2, 3, 10); a drop-in replacement must send the same bytes.
- **Share an HTTP head reader or writer through Abstractions.** Lost for the reasons in ADR-0128:
  Abstractions stays contract-only, and RTSP's head differs (the `RTSP/1.0` version, `CSeq` first,
  `Referer` before `User-Agent`, no `Host` or `Accept`).
- **Keep `CSeq` and `Session` per connection.** Would send `CSeq: 2` and the session on a reused
  connection, where curl sends `CSeq: 0` and no session (row 20).
- **Leave exit 86 out because the tool cannot reach it.** The handler would then be incomplete as
  an RTSP client and the audit's finding would stand; the check costs a few lines and is pinned by
  handler tests.
