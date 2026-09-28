# ADR-0131 — The WebSocket frame reader streams payloads, answers the last ping per read, and fails violations with 56

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-581.

## Context

ADR-0128 fixed what the curl tool does after a `101`: it writes data and close payloads,
answers a ping with a masked pong, neither answers nor stops at a close, and fails a masked
server frame with 56. BL-581 builds the frame reader and writer, which needs more: every length
form, fragments, the other RFC 6455 violations (reserved bits, unknown opcodes, fragmented or
oversized control frames, fragment order), a frame cut short, and when the pong goes out.

### Measurements

curl 8.21.0 (Windows, Schannel build), `curl -s -S ws://127.0.0.1:<port>/`, recorded with
`Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500`, ports 47951-47973 and 48011-48025. Every
reply starts with the `101` head of ADR-0128; frames are in hex, text in quotes. "Sent" is what
curl wrote after its upgrade request. `a×126` is 126 bytes of `a`.

| Frames after the `101` | Sent | stdout | stderr | Exit |
| --- | --- | --- | --- | --- |
| `89 02 "hi"` `81 05 "hello"` `88 02 03 e8` | `8a 82 44 22 90 af 2c 4b` | `hello` `03 e8` | | 0 |
| `89 00` `81 05 "hello"` | `8a 80 8a 44 48 0d` | `hello` | | 0 |
| `89 7d` `b×125` `81 02 "ok"` | `8a fd 11 95 60 cb` + 125 masked bytes | `ok` | | 0 |
| `89 01 "a"` `89 01 "b"` (one read) | `8a 81 64 20 c9 42 06`: one pong, for `b` | | | 0 |
| `88 00` `89 02 "hi"` | a pong for `hi` | | | 0 |
| `01 03 "hel"` `89 02 "hi"` `80 02 "lo"` | a pong for `hi` | `hello` | | 0 |
| `81 05 "hello"` `88 02 03 e8` | nothing | `hello` `03 e8` | | 0 |
| `81 05 "hello"` `88 00` | nothing | `hello` | | 0 |
| `88 05 03 e8 "bye"` | nothing | `03 e8 bye` | | 0 |
| `88 01 "x"` `81 02 "ok"` | nothing | `xok` | | 0 |
| `8a 02 "hi"` `81 02 "ok"` | nothing | `hiok` | | 0 |
| `82 7e 00 7e` `a×126` / `82 7f 00…00 7e` `a×126` | nothing | 126 bytes | | 0 |
| `82 7e 00 02 "ok"` / `82 7e 00 00` `81 02 "ok"` | nothing | `ok` | | 0 |
| `01 03 "hel"` `00 01 "l"` `80 01 "o"` | nothing | `hello` | | 0 |
| `81 05 "hel"` then the server closes | nothing | `hel` | | 0 |
| `81 85 01 02 03 04 …` / `89 82 …` | nothing | | `curl: (56) [WS] masked input frame` | 56 |
| `c1 05 …` / `a1 05 …` / `91 05 …` / `c1 85 …` | nothing | | `curl: (56) [WS] invalid reserved bits: c1` (`a1`, `91`, `c1`) | 56 |
| `83 01 …` / `8b 01 …` / `03 01 …` | nothing | | `curl: (56) [WS] invalid opcode: 83` (`8b`, `03`) | 56 |
| `80 02 "lo"` | nothing | | `curl: (56) [WS] no ongoing fragmented message to resume` | 56 |
| `01 03 "hel"` `81 02 "ok"` | nothing | `hel` | `curl: (56) [WS] fragmented message interrupted by new TEXT msg` | 56 |
| `02 03 "hel"` `82 02 "ok"` | nothing | `hel` | `… interrupted by new BINARY msg` | 56 |
| `01 01 "h"` `c0 01 "i"` | nothing | `h` | `curl: (56) [WS] invalid reserved bits: c0` | 56 |
| `09 02 "hi"` / `0a 02` / `08 02` | nothing | | `curl: (56) [WS] invalid fragmented PING frame` (`PONG`, `CLOSE`) | 56 |
| `89 7e 00 7e` `a×126` / `8a …` / `88 …` | nothing | | `curl: (56) [WS] received PING frame is too big` (`PONG`, `CLOSE`) | 56 |
| `82 7f 80 00 00 00 00 00 00 02 "ok"` | nothing | | `curl: (56) [WS] frame length longer than 63 bits not supported` | 56 |

## Decision

1. **Payloads stream.** `WsFrameDecoder` keeps its place between reads and hands on payload
   bytes as they arrive, so a frame cut short still reaches the output (`hel`) and a violation
   after a fragment fails only after the fragment is written. Text, binary, continuation,
   close **and pong** payloads are written; only a ping's payload is held back, to be echoed.
2. **Pongs.** Each ping is answered with a FIN, masked pong carrying its payload, the mask from
   `IWebSocketRandomSource`. When several pings complete in the bytes of one read only the last
   is answered, as curl replaces a pong it has not yet sent; pings in different reads each get
   one. The pong is sent after that read's payload is handed on. When a read ends in a
   violation, no pong is sent for it (curl's order there was not observable; the transfer
   fails either way).
3. **Close.** A close frame is written and neither answered nor treated as the end, with any
   payload length up to 125, unchecked (a 1-byte payload is accepted). `WsFrameReceiver` reads
   until the connection closes and returns the frame bytes received, heads included.
4. **Violations** fail with 56 and the messages above, checked in curl's order: reserved bits,
   then the opcode and fragment order on the first byte; the mask bit, then an oversized
   control frame on the second; the 64-bit length's top bit once the length is read. A
   non-minimal length form is accepted.
5. **Client frames.** `WsFrameEncoder` writes FIN set, mask bit set, the shortest length form,
   a fresh 4-byte mask, payload masked. It serves pongs here and `-T` in BL-582.

## Consequences

- BL-582 wires `WsFrameReceiver` into `WsProtocolHandler` after the `101`: output writes,
  52 when no frame byte ever arrives, `-m`, the download size from its return value, and `-T`
  through `WsFrameEncoder`.
- BL-584's `-v` lines per frame will need the decoder to report frame boundaries; it reports
  only payload bytes today.

## Alternatives considered

- **Buffer whole frames and hand on complete messages.** Simpler to trace, but curl writes a
  partial frame before the connection drops and writes a fragment before a later violation;
  buffering would lose those bytes.
- **Answer every ping.** RFC 6455 allows answering only the latest, and curl 8.21.0 does exactly
  that within one read, so a pong per ping would send bytes curl does not.
