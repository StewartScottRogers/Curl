# ADR-0128 — The WebSocket library writes its own upgrade request and reads its own `101` head

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-579.

## Context

In curl, WebSocket lives inside the HTTP code: a `ws://` transfer is an HTTP/1.1 `GET` with
`Upgrade: websocket`, and after the `101` reply the connection carries RFC 6455 frames. Here
`ws` and `wss` have their own library, `Curl.Protocol.Ws.UnitLibrary`, which may reference
`Curl.Protocol.Abstractions.UnitLibrary` and the hand-built libraries (ADR-0120) and never
`Curl.Protocol.Http.UnitLibrary`. The handler still has to send curl's upgrade request byte for
byte and read the reply head. This ADR fixes where that code lives, which HTTP options apply to
a WebSocket transfer, what the curl tool does once the connection is upgraded, and how tests
fix the random key and masks. BL-580 to BL-584 build on it.

### Measurements

curl 8.21.0 (Windows, Schannel build, Git for Windows' mingw64), recorded with
`Record-CurlExchange.ps1` in HTTP mode, `-Port 47901`/`47902`/`47903`. `H101` is
`HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: <value>\r\n\r\n`;
frames are written in hex. `T` is the text frame `81 05 "hello"`, `B` the binary frame
`82 03 01 02 03`, `C` the close frame `88 02 03 e8` (status 1000), `C0` the empty close frame
`88 00`, `P` the ping `89 02 "hi"`. "Held" means `-HoldOpenMilliseconds` kept the connection open
after the canned bytes, so anything curl sent back is recorded and the run lasts until the hold
ends. The key and the progress meter vary from run to run and are left out.

The request curl sends for `curl ws://127.0.0.1:47901/chat`:

```
GET /chat HTTP/1.1\r\n
Host: 127.0.0.1:47901\r\n
User-Agent: curl/8.21.0\r\n
Accept: */*\r\n
Upgrade: websocket\r\n
Sec-WebSocket-Version: 13\r\n
Sec-WebSocket-Key: <base64 of 16 random bytes>\r\n
Connection: Upgrade\r\n
\r\n
```

| # | Arguments / reply | Sent by curl (after its request) | stdout | stderr (without meter) | Exit |
| --- | --- | --- | --- | --- | --- |
| 1 | `ws://…/chat`; `H101 T C` | nothing | `hello` `03 e8` | empty | 0 |
| 2 | `ws://…/`; `H101 T`, held 3 s | nothing | `hello` | empty; run 3037 ms | 0 |
| 3 | `-H 'X-Test: 1' -A agent/1 -u user:pw -b a=b ws://…/p?q=1`; `H101 T C` | request is `GET /p?q=1`, `Host`, `Authorization: Basic dXNlcjpwdw==`, `User-Agent: agent/1`, `Accept`, `Upgrade`, `Sec-WebSocket-Version`, `Sec-WebSocket-Key`, `X-Test: 1`, `Connection: Upgrade`; **no `Cookie`** | `hello` `03 e8` | empty | 0 |
| 4 | `-e http://ref/ -X POST --compressed -H 'Connection: keep-alive'`; `H101` + `81 02 "ok"` | request is `POST /`, `Host`, `User-Agent`, `Accept`, `Referer: http://ref/`, `Upgrade`, `Sec-WebSocket-Version`, `Sec-WebSocket-Key`, `Connection: keep-alive, Upgrade`; **no `Accept-Encoding`** | `ok` | empty | 0 |
| 5 | `-i`; `H101 T C` | nothing | `hello` `03 e8` — **no head** | empty | 0 |
| 6 | `-D -`; `H101 T C0`, held 1 s | nothing | the `101` head, byte for byte, then `hello` | empty | 0 |
| 7 | `-v`; `H101 T C` | nothing | `hello` `03 e8` | see below | 0 |
| 8 | `H101 B C` | nothing | `01 02 03 03 e8` | empty | 0 |
| 9 | `H101` + `01 03 "hel"` + `80 02 "lo"` + `C0`, held 1 s | nothing | `hello` | empty | 0 |
| 10 | `H101 T C`, held 2 s | **nothing: no close reply** | `hello` `03 e8` | empty; run 2102 ms | 0 |
| 11 | `H101 T C0`, held 2 s | nothing | `hello` | empty; run 2032 ms | 0 |
| 12 | `H101 P T C`, held 2 s | `8a 82 <mask> <"hi" masked>`: a masked pong echoing the ping | `hello` `03 e8` (ping payload not written) | empty | 0 |
| 13 | `-T -` with standard input `typed\n`; `H101 T`, held 3 s | `82 86 <mask> <"typed\n" masked>`: all of standard input as one masked binary frame | `hello` | empty; meter's upload column 12 | 0 |
| 14 | `-v -T -` with standard input `abc`; `H101` + `81 02 "ok"`, held 1.5 s | `82 83 <mask> <"abc" masked>` | `ok` | `-v` adds `* upload completely sent off: 9 bytes` | 0 |
| 15 | `-d payload`; `H101 T`, held 3 s | nothing: method stays `GET`, no body, no frame | `hello` | empty | 0 |
| 16 | `-m 1`; `H101` + `81 02 "ok"`, held 3 s | nothing | `ok` | `curl: (28) Operation timed out after 1016 milliseconds with 4 bytes received` | 28 |
| 17 | `-w '%{size_download} %{http_code} %{response_code}'`; `H101 T C` | nothing | `hello` `03 e8` `11 101 101` | empty | 0 |
| 18 | `HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok` | nothing | empty (body not written) | `curl: (22) Refused WebSocket upgrade: 200` | 22 |
| 19 | as 18 with `-i` | nothing | empty (no head either) | as 18 | 22 |
| 20 | `HTTP/1.1 401 Unauthorized\r\nContent-Length: 0\r\n\r\n` | nothing | empty | `curl: (22) Refused WebSocket upgrade: 401` | 22 |
| 21 | `H101` with `Sec-WebSocket-Accept: wrong`, then `T C` | nothing | `hello` `03 e8` | empty | 0 |
| 22 | `101` with no `Upgrade` header, then `T C` | nothing | `hello` `03 e8` | empty | 0 |
| 23 | `H101` + masked text frame `81 85 01 02 03 04 …` | nothing | empty | `curl: (56) [WS] masked input frame` | 56 |
| 24 | `-v -x http://127.0.0.1:47901 ws://example.invalid/`; proxy answers `200 Connection established` then nothing, held 1.5 s | `CONNECT example.invalid:80 HTTP/1.1` (`Host: example.invalid:80`, `User-Agent`, `Proxy-Connection: Keep-Alive`), then the upgrade request with `Host: example.invalid` through the tunnel | empty | `* Empty reply from server`, `curl: (52) Empty reply from server` | 52 |

`-v` for row 7 writes, around the usual connect lines:

```
* using HTTP/1.x
> GET / HTTP/1.1
> Host: 127.0.0.1:47901
> User-Agent: curl/8.21.0
> Accept: */*
> Upgrade: websocket
> Sec-WebSocket-Version: 13
> Sec-WebSocket-Key: <key>
> Connection: Upgrade
>
* Request completely sent off
< HTTP/1.1 101 Switching Protocols
< Upgrade: websocket
< Connection: Upgrade
< Sec-WebSocket-Accept: <accept>
<
* Received 101, Switching to WebSocket
* [WS] Received 101, switch to WebSocket
{ [11 bytes data]
* shutting down connection #0
```

and for row 18 (`-v`), after the two response header lines: `* Refused WebSocket upgrade: 200`,
`< `, an empty line, `* closing connection #0`, then the `curl: (22)` line.

What the table shows:

- The upgrade request is curl's HTTP/1.1 request head with `Upgrade: websocket`,
  `Sec-WebSocket-Version: 13` and `Sec-WebSocket-Key` after `Accept` (and after `Referer`), the
  `-H` headers after the key, and `Connection: Upgrade` last; a `-H 'Connection: …'` value is
  kept and gets `, Upgrade` appended (rows 1, 3, 4).
- `-H`, `-A`, `-e`, `-X` and `-u` (pre-emptive Basic) reach the request; `-b` (no `Cookie`),
  `--compressed` (no `Accept-Encoding`) and `-d` (no body, method unchanged) do not (rows 3, 4, 15).
- Any status but `101` fails with exit 22, `Refused WebSocket upgrade: <code>`, and no body
  (rows 18-20). A `101` is accepted without checking `Sec-WebSocket-Accept` or `Upgrade`
  (rows 21, 22).
- After the upgrade the tool writes the payload of every text, binary, continuation **and close**
  frame to the output, and nothing of a ping (rows 1, 8, 9, 12). It answers a ping with a masked
  pong carrying the ping's payload (row 12). It does not answer a close frame and does not stop
  at one: the transfer ends when the server closes the connection, with exit 0 (rows 2, 10, 11),
  or at `-m` with 28 (row 16).
- With `-T`, the upload is sent as one masked binary frame after the upgrade (rows 13, 14).
- `-i` writes no head for a WebSocket transfer; `-D` does (rows 5, 6, 19).
- `%{size_download}` and the meter's received count are frame bytes including frame headers
  (7 + 4 = 11 in row 17); `%{http_code}` is `101`.
- A server frame with the mask bit set fails with exit 56, `[WS] masked input frame` (row 23).
- An HTTP proxy is always tunnelled with `CONNECT`, even for `ws://` (row 24).

## Decision

**1. The upgrade request writer and the `101` head reader live in `Curl.Protocol.Ws.UnitLibrary`.**
The library gets a small `WsUpgradeRequestFormatter` that writes the head in the measured order
from the transfer's `CurlUrl`, `HttpRequestOptions` and credentials, and a `WsUpgradeResponseReader`
that reads the status line and header lines up to the blank line from `IConnection` (enough to
take the status code, keep the raw head for `-D` and `-v`, and leave any bytes after the head for
the frame reader). Nothing moves into Abstractions and nothing is shared with the HTTP library.

**2. Options that apply**, as measured:

| Option | On a WebSocket transfer |
| --- | --- |
| `-H` | Sent after `Sec-WebSocket-Key`; a `Connection` value gets `, Upgrade` appended in place of curl's own `Connection: Upgrade`. |
| `-A`, `-e`, `-X` | Sent as for HTTP (`User-Agent`, `Referer`, the method). |
| `-u`, `--basic`, `--oauth2-bearer` | Pre-emptive `Authorization` from the injected `IHttpAuthenticator` with no challenges. A `401` is refused (exit 22), so no challenge-driven scheme ever runs. |
| `-x` and the proxy options | Handed to the connector as `ConnectTarget.Proxy`; an HTTP proxy is always a `CONNECT` tunnel, which `Curl.Networking.UnitLibrary` already builds. |
| `-D` | The `101` head, byte for byte, to `HeaderOutput`. |
| `-v`, `--trace`, `--trace-ascii` | Through `ITransferEvents` (ADR-0046), as measured above (BL-584). |
| `-T` | The whole upload is sent as one masked binary frame once the upgrade succeeds. |
| `-m`, `--connect-timeout` | As for every transfer (ADR-0117). |
| `-b`, `-c`, `--compressed`, `-d` and the other body options, `-i` | No effect. |

Options not named here (`-L`, `-f`, `-r`, `-z` and the like) are not mapped for `ws`/`wss` in
BL-583; a later measurement that shows curl honours one is a new task.

**3. Post-upgrade behaviour.** After a `101` the handler reads frames until the connection closes
or the transfer is cancelled. The payload of every text, binary, continuation and close frame is
written to `ITransferContext.Output` as it arrives; ping payloads are not written, and each ping
is answered with a masked pong echoing its payload. A close frame is neither answered nor treated
as the end. A connection closed by the server ends the transfer with exit 0; `-m` ends it with 28
and curl's timeout message; a masked server frame fails with 56 `[WS] masked input frame`. The
download size is the count of frame bytes read, headers included; the response code is `101`.
`Sec-WebSocket-Accept` and `Upgrade` in the reply are not checked, and any other status fails with
22 `Refused WebSocket upgrade: <code>` without writing the body.

**4. The randomness seam.** `Curl.Protocol.Ws.UnitLibrary` defines `IWebSocketRandomSource` with
`void Fill(Span<byte> destination)`, and `SystemWebSocketRandomSource` fills it from
`RandomNumberGenerator`, as `ISshRandomSource` does for SSH. The handler takes it by constructor
injection and draws 16 bytes per upgrade for `Sec-WebSocket-Key` and 4 bytes per client frame
for the mask; tests pass a source that returns fixed bytes, so request and frame bytes are pinned
exactly.

**5. Abstractions change: none.** `ITransferContext` already carries `Url`, `Output`, `Upload`,
`HeaderOutput`, `Credentials`, `Proxy`, `Http` (`Headers`, `UserAgent`, `Referer`, `CustomMethod`,
`BearerToken`, `AuthSchemes`, `CommandLineTextEncoding`), `MaxTime`, `Events` and `Progress`;
`IConnector`, `ConnectTarget` (`UseTls` for `wss`, `Proxy` for the tunnel) and
`IHttpAuthenticator` are already contracts. No task is needed before BL-580.

## Consequences

- BL-580 builds the formatter, the head reader, the random source and the `101`/refusal outcome
  in `Curl.Protocol.Ws.UnitLibrary`, pinned with a fixed key; it does not verify
  `Sec-WebSocket-Accept`, because curl does not (rows 21, 22), which replaces BL-580's goal of
  checking it.
- BL-581 builds the frame reader and writer: every length form, fragments, the masked pong, no
  close reply, and 56 for a masked server frame.
- BL-582 writes data and close payloads to the output, sends `-T` as one binary frame, ends at the
  server's close of the connection (exit 0) or at `-m` (28), and reports frame bytes as the
  download size and `101` as the code.
- BL-583 registers the handler for `ws` (port 80) and `wss` (port 443) in `Curl.Console`, maps the
  options in the table above and no others, and lists `ws` and `wss` in `-V`.
- BL-584 writes the `-v` and trace lines above, including the two `Received 101` lines and
  `{ [n bytes data]`.
- The request head is written twice in the solution, once by the HTTP library and once here. The
  WebSocket head is a fixed shape with no body, redirects, cookies, compression or
  challenge-driven auth, so the copy is small; a change to how curl orders common HTTP headers has
  to be made in both.
- `-i` doing nothing on a WebSocket transfer is surprising but measured; tests pin it so no one
  "fixes" it.

## Alternatives considered

- **Move the HTTP head reader (and a request writer) into Abstractions.** Abstractions holds only
  contracts and would grow HTTP parsing code shared by one extra caller; the HTTP library's reader
  also carries redirects, continue handling, content codings and trailers that a WebSocket never
  needs. Lost on size and on keeping Abstractions contract-only.
- **A request-writer contract in Abstractions that `Curl.Console` fills from the HTTP library.**
  Keeps one writer, but the WebSocket head differs from HTTP's (header order around `Upgrade`, the
  `Connection` merge, options that do not apply), so the contract would need WebSocket-specific
  parameters that leak `ws` into the HTTP library, and every Ws test would need a fake writer
  that then decides the bytes under test. Lost on coupling and testability.
- **Verify `Sec-WebSocket-Accept` as RFC 6455 requires.** Correct by the RFC, but curl 8.21.0
  accepts a wrong value (row 21), and a drop-in replacement must not fail where curl succeeds.
- **Stop at the server's close frame and answer it.** What a WebSocket client library does, but
  the curl tool neither answers nor stops (rows 10, 11), and ending early would change timings
  and output.
- **Use `ISshRandomSource` or a shared random contract in Abstractions.** A protocol library may
  not reference another, and a one-method interface is cheaper to own than a new shared contract.
