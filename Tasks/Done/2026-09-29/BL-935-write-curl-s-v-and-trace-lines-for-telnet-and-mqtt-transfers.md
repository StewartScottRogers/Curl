---
id: BL-935
title: Write curl's -v and --trace lines for TELNET and MQTT transfers after connect
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-935 — Write curl's -v and --trace lines for TELNET and MQTT transfers after connect

## Goal

`telnet://` and `mqtt://` (and `mqtts://`) transfers write curl 8.21.0's `-v` lines and `--trace`/`--trace-ascii` blocks after connect: TELNET's option negotiation and data, MQTT's packets, not only the connect lines they write today.

## Context

- Audit 2026-09-29, part B: an archived task made both handlers pass `context.Events` to their `ConnectTarget` (`TelnetProtocolHandler.cs` line ~111, `MqttProtocolHandler.cs` line ~149), so the connect lines appear; after connect neither reports any event.
- curl's `lib/telnet.c` writes option negotiation to the verbose output when `-v` is on; which lines, in which format, and whether curl 8.21.0 dumps TELNET and MQTT bytes as data or header blocks, must be measured, not assumed.
- Where: `Curl.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs`; `Curl.Protocol.Mqtt.UnitLibrary/MqttSession.cs`, `MqttPacketReader.cs`, `MqttPackets.cs`, `MqttTransferMessages.cs` (keep measured texts there).
- Measure first with `Record-CurlExchange.ps1` (default TCP mode with `-Response` holding the server's bytes, or `-Script` for a multi-step exchange, `-Tls` for `mqtts`): `-v`, `--trace-ascii -` and `--trace -` for a TELNET session where the server sends `IAC DO TERMINAL-TYPE` and `IAC WILL ECHO` then text and closes; `telnet://host -t TTYPE=vt100`; an MQTT subscribe (`mqtt://host/topic`) receiving one PUBLISH; an MQTT publish (`-d payload`); a CONNACK refusal. Copy output into Notes with curl's version and build before pinning text.

## Acceptance criteria

- [x] Measured output for the five cases is copied into Notes.
- [x] `Curl.Protocol.Telnet.UnitTests` and `Curl.Protocol.Mqtt.UnitTests` pin, through a recording `ITransferEvents`, every measured event after connect, in curl's order.
- [x] Existing tests in both projects pass unmodified.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Telnet.UnitLibrary` and `Curl.Protocol.Mqtt.UnitLibrary`.

## Notes

Measured 2026-09-29 with curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel, through
`Record-CurlExchange.ps1 -Script` (loopback, plain TCP). Connect lines (`*   Trying ...`,
`* Established connection ...`) are the connector's and omitted. `-v` gives the same
`* ` lines as `--trace-ascii -`, with `{ [7 bytes data]` for each data block; `--trace -`
gives the same blocks in hex.

**1. TELNET, server sends `IAC DO TTYPE`, `IAC WILL ECHO`, then `hello\r\n` and closes** (`--trace-ascii -`, exit 0):
```
* RCVD DO TERM TYPE
* SENT WONT TERM TYPE
* RCVD WILL ECHO
* SENT DO ECHO
* SENT WILL BINARY
* SENT DO BINARY
* SENT WILL SUPPRESS GO AHEAD
* SENT DO SUPPRESS GO AHEAD
<= Recv data, 7 bytes (0x7)
0000: hello
* shutting down connection #0
```
No `Send data` block for the negotiation bytes (nor for a `-T -` upload, also measured),
no zero-byte block at the close; the data block holds the output bytes, not the raw ones.

**2. The same with `-t TTYPE=vt100`, the server then sending `IAC SB TTYPE SEND IAC SE`**: as
above with `* SENT WILL TERM TYPE`, then
```
* RCVD IAC SB 
* TERM TYPE
*  SEND
*  ""
* SENT IAC SB 
* TERM TYPE
*  IS
*  "vt100"
<= Recv data, 4 bytes (0x4)
0000: hi
* shutting down connection #0
```
Each `printsub` piece is its own `* ` line (curl's `infof` per piece). Also measured:
`ab IAC WILL ECHO cd\r\0e IAC IAC z\r\n` in one read gives blocks `ab`, `cd\r`, `e`,
`\xFFz\r\n` around the negotiation lines (one block per run between commands);
`IAC NOP` -> `* RCVD IAC NOP`, `IAC 7` -> `* RCVD IAC 7`, `WILL 255` -> `RCVD WILL EXOPL`,
`WILL 40` -> `RCVD WILL 40`; NAWS sent -> `SENT IAC SB ` / `NAWS` / `Width: 300 ; Height: 24`;
NEW-ENVIRON IS sent -> ` IS`, ` `, then one line per byte after the first VAR with
`, ` for VAR and ` = ` for VALUE (`U`,`S`,`E`,`R`,` = `,`b`,`o`,`b`,`, `,...); STATUS
`SEND 02` -> `STATUS (unsupported)`, ` SEND`, ` 02`; option 200 -> `200 (unknown)`;
`SB TTYPE IAC SE` (one byte) -> `(Empty suboption?)`; `SB IAC SE` (none) -> nothing.
Endings: a missing `-t TTYPE` (exit 43) and a non-ASCII `-u` (exit 43) print no message,
then `* shutting down connection #0`; `IAC SB TTYPE SEND IAC 0x01` (exit 56) prints
`* telnet: suboption error` then shutting down; `-m 1` (exit 28) `* Time-out` then shutting
down; `-t FOO=1` (exit 48) `* Unknown telnet option FOO=1`; `-t TTYPE` (exit 49)
`* Syntax error in telnet option: TTYPE`; an output that cannot be opened (exit 23)
`* client returned ERROR on write of 7 bytes` then `* closing connection #0`.

**3. MQTT subscribe `mqtt://127.0.0.1:47954/t`, CONNACK, SUBACK, one PUBLISH of `hello`, then close** (exit 56; one-byte `Recv header` lines abridged to their byte):
```
* Using client id 'curlr2MpVt2J'
=> Send header, 26 bytes (0x1a)
0000: ....MQTT...<..curlr2MpVt2J
* mqtt_doing: state [0]
* mqtt_doing: state [0]
<= Recv header, 1 bytes (0x1)   [20]
<= Recv header, 1 bytes (0x1)   [02]
* mqtt_doing: state [2]
<= Recv header, 2 bytes (0x2)
=> Send header, 8 bytes (0x8)
0000: ......t.
* mqtt_doing: state [0]
<= Recv header, 1 bytes (0x1)   [90]
<= Recv header, 1 bytes (0x1)   [03]
* mqtt_doing: state [3]
<= Recv header, 3 bytes (0x3)
* mqtt_doing: state [0]
<= Recv header, 1 bytes (0x1)   [30]
<= Recv header, 1 bytes (0x1)   [08]
* mqtt_doing: state [5]
* Remaining length: 8 bytes
<= Recv data, 8 bytes (0x8)
0000: ..thello
* mqtt_doing: state [0]
* Connection disconnected
* shutting down connection #0
```
With a DISCONNECT after the PUBLISH: `state [0]`, `[E0]`, `[00]`, `* Got DISCONNECT`,
shutting down, exit 0.

**4. MQTT publish `-d payload`**: up to the CONNACK body as above, then
`=> Send header, 12 bytes` (`0...tpayload`), `=> Send header, 2 bytes`,
`* shutting down connection #0`, exit 0.

**5. CONNACK refusal `20 02 00 05`**: up to `<= Recv header, 2 bytes`, then
`* Expected 0000 but got 0005`, `* shutting down connection #0`, exit 8.

Also measured: CONNACK of length 3 -> `state [2]` then `* CONNACK expected Remaining Length 2, got 3`;
SUBACK of length 2 -> `state [3]` then `* SUBACK expected ...`; PINGRESP -> `* Received ping response.`
then the next packet in `state [5]`; a PUBLISH cut short after 4 of 8 bytes -> the 4-byte data
block, `state [6]`, `* server disconnected`, shutting down, exit 18 with no message line;
an empty `40 00` then `30 02 00 01` -> three `state [0]`, then `state [7]`,
`* State not handled yet`, shutting down, exit 0; `mqtt://host/` -> `* No MQTT topic found.
Forgot to URL encode it?`, shutting down; exit 23 -> the data block,
`* client returned ERROR on write of 8 bytes`, `* closing connection #0`.

Decisions (defaults taken, following the measurements; no ADR needed):
- The extra `mqtt_doing: state [0]` straight after the CONNECT is always reported: curl
  runs `mqtt_doing` once before any reply can arrive, and did so in every measured run.
- PUBLISH bodies are traced in the 4096-byte slices already used for output writes, with
  `state [6]` before each slice after the first (curl's buffer size); curl's own count
  depends on how the bytes arrive.
- TELNET exit 55 (`Send failure: Connection was reset`) reports the message then
  `shutting down`, like exits 28 and 56 which end through the same telnet loop; not
  measured, because the recorder cannot make a send fail.
- Failure message lines follow `failf`: the `curl_easy_strerror` texts (telnet's two,
  MQTT's five in `MqttTransferMessages.IsStrerrorText`) get none; the connection-ending
  line uses `ConnectResult.ConnectionNumber`.
- A TELNET read with several data runs whose output write fails still traces every run of
  that read (the handler writes a read's data at once); curl would stop at the first.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. telnet and mqtt(s) transfers report curl's -v/--trace negotiation, packet and data lines after connect
