---
id: BL-033
title: Author the dict, gopher, telnet, tftp and mqtt requirements
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-033 — Author the dict, gopher, telnet, tftp and mqtt requirements

## Goal

`Documentation/Product/Requirements.md` has a section for each of `dict://`,
`gopher://`/`gophers://`, `telnet://`, `tftp://` and `mqtt://`/`mqtts://`, each row a
numbered Draft functional requirement stating one measured curl 8.21.0 behaviour, so the
protocol tasks can cite them.

## Context

`Requirements.md` covers only `file://` today (FR-001 to FR-019). The protocol tasks for
these five scheme families must not each edit this shared file, so every row is written
here, once. Number new rows from the next free FR number, in the order below, and never
renumber an existing one.

Every behaviour below was measured on 2026-09-26 with the local curl 8.21.0
(`x86_64-w64-mingw32`, Release-Date 2026-06-24) against a loopback capture server, and is
supported by these upstream pages: <https://curl.se/docs/url-syntax.html> (default
ports), <https://curl.se/docs/manpage.html>, <https://curl.se/docs/mqtt.html>,
<https://curl.se/libcurl/c/CURLOPT_TELNETOPTIONS.html>,
<https://curl.se/libcurl/c/CURLOPT_TFTP_BLKSIZE.html>,
<https://curl.se/libcurl/c/CURLOPT_TFTP_NO_OPTIONS.html> and
<https://curl.se/libcurl/c/libcurl-errors.html>.

**dict** (default port 2628)

- curl sends its whole request without waiting for the greeting:
  `CLIENT libcurl 8.21.0\r\n`, one command line, then `QUIT\r\n`.
- `/d:word` and `/lookup:word` send `DEFINE ! word`; `/d:word:db` sends `DEFINE db word`.
- `/m:word:db:prefix` sends `MATCH db prefix word`; `/find:word` sends `MATCH ! . word`.
- Any other path is sent as the command after percent-decoding: `/word` sends `word`,
  `/show%20db` sends `show db`, and `/` sends an empty line.
- Every byte the server sends is written to the output unaltered; exit 0.

**gopher and gophers** (default port 70 for both; `gophers` is gopher over TLS)

- The selector is the path with its leading `/` and item-type character removed,
  percent-decoded, followed by CRLF: `/` and `/1` send `\r\n`; `/1/foo` sends `/foo\r\n`;
  `/0/a%09b` sends `/a\tb\r\n`; `/7/search%09term%20x` sends `/search\tterm x\r\n`.
- The response is written to the output unaltered; exit 0.

**telnet** (default port 23)

- The bytes read from standard input are sent unchanged (`a\nb\n` is sent as `a\nb\n`);
  with empty input nothing is sent.
- Received data is written to the output with telnet command sequences removed, and
  `IAC IAC` is written as a single `0xFF` byte. A server's `IAC WILL ECHO` is answered
  with `IAC DO ECHO`.
- `-t TTYPE=vt100` answers `IAC DO TTYPE` with `IAC WILL TTYPE`, and
  `IAC SB TTYPE SEND IAC SE` with `IAC SB TTYPE IS vt100 IAC SE`; option names are
  matched ignoring case.
- An unknown `-t` name is exit 48 (`CURLE_UNKNOWN_OPTION`),
  `An unknown option was passed in to libcurl`; a `-t` value with no `=` is exit 49
  (`CURLE_SETOPT_OPTION_SYNTAX`), `Syntax error in telnet option: TTYPE`.
- The session ends, exit 0, when the server closes the connection.

**tftp** (default port 69)

- A download sends the read request
  `00 01 <file> 00 "octet" 00 "tsize" 00 "0" 00 "blksize" 00 "512" 00 "timeout" 00 "6" 00`,
  acknowledges each DATA block with `00 04 <block>`, and ends at a block shorter than the
  block size.
- An option acknowledgement (OACK) is answered with an ACK of block 0, and an
  acknowledged `blksize` is used.
- An upload (`-T`) sends the write request with `tsize` set to the upload's length, then
  DATA blocks from block 1.
- TFTP ERROR packets map to exit codes and messages: code 1 to exit 68
  `TFTP: File Not Found`; 2 to 69 `TFTP: Access Violation`; 3 to 70
  `Disk full or allocation exceeded`; 0 and 4 to 71 `TFTP: Illegal operation`; 5 to 72
  `TFTP: Unknown transfer ID`; 6 to 73 `Remote file already exists`; 7 to 74
  `TFTP: No such user`; 8 to 42 `Operation was aborted by an application callback`.
- A URL with no file name is exit 71, `Missing filename`, and no packet is sent.
- `--tftp-no-options` sends the request with no options; `--tftp-blksize` is clamped to
  8-65464 (5 is sent as 8, 70000 as 65464).

**mqtt and mqtts** (default port 1883 for `mqtt`; `mqtts` is MQTT over TLS)

- curl connects with an MQTT 3.1.1 CONNECT: protocol level 4, clean session, keep-alive
  60 seconds, and a client identifier of `curl` followed by eight random alphanumeric
  characters.
- Without `-d` it subscribes to the URL path as the topic at QoS 0 with packet
  identifier 1, and writes each received PUBLISH as a two-byte big-endian topic length,
  the topic, then the payload. When the server closes the connection: exit 56
  (`CURLE_RECV_ERROR`), `Connection disconnected`.
- The topic is the URL path without its leading `/`, percent-decoded: `/a%2Fb`
  subscribes to `a/b`.
- With `-d` it publishes the data to the topic at QoS 0, writes nothing to the output,
  and exits 0. When the server keeps the connection open, curl then sends DISCONNECT
  (`E0 00`).
- With `-u user:password`, split at the first colon (`bob:se:cret` gives password
  `se:cret`), the CONNECT carries the user name and password (flags `0xC2`); user
  information in the URL (`mqtt://al:pw@host/t`) does the same.
- A CONNACK with a non-zero return code is exit 8 (`CURLE_WEIRD_SERVER_REPLY`); return
  code 5 gives `Expected 0000 but got 0005`.
- An empty topic is exit 3 (`CURLE_URL_MALFORMAT`),
  `No MQTT topic found. Forgot to URL encode it?`, reported after the CONNECT is sent.
- Only QoS 0 is implemented for publish, and the retain flag cannot be set
  (<https://curl.se/docs/mqtt.html>).

## Acceptance criteria

- [x] `Documentation/Product/Requirements.md` has one `###` section per scheme family
      above, placed after the `file://` section, each opening with a paragraph in the
      style of the `file://` one: checked against curl 8.21.0, the sources used, and the
      `Curl.Protocol.<Name>.UnitLibrary` that will hold the handler.
- [x] Each bullet in `Context` becomes one row - one behaviour - with a new FR ID, an
      upstream reference link, a MoSCoW priority, Status `Draft` and Checked against
      `curl 8.21.0`. A row that quotes an exit code names its `CURLE_*` constant, as the
      `file://` rows do.
- [x] Each new row says the behaviour is not yet implemented, without naming a task ID,
      so the row stays true while tasks are split or renumbered.
- [x] No existing row (FR-001 to FR-019) changes, and the `TODO` note at the top of the
      file is updated to list which schemes now have requirements.
- [x] No file other than `Documentation/Product/Requirements.md` is changed.

## Notes

The measurements were made by pointing the curl binary at a Python loopback listener that
recorded what curl sent and replied with canned bytes. They are recorded here so the
requirements carry measured facts rather than recollections of the C source.

Delivered as FR-020 to FR-045 in five `###` sections after the `file://` section.
Choices made unattended:
- Written in-session rather than through `align-and-document`: the task is one
  table-shaped file whose every row is already specified in `Context`.
- Each row states "Not yet implemented." rather than naming a task, and each section's
  opening paragraph says the same, so the rows stay true while tasks are re-planned.
- Default ports are stated in each section's opening paragraph, not as rows: the
  `Context` bullets did not list them as behaviours to cite.
- Exit 0 is named `CURLE_OK`, so every quoted exit code carries its constant.
- MoSCoW: the core request/response bytes and error mapping are Must; `-t`, uploads,
  authentication and edge-case errors are Should; `--tftp-no-options`/`--tftp-blksize`
  clamping and the QoS 0/no-retain limit are Could.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Requirements.md has FR-020 to FR-045 for dict, gopher, telnet, tftp and mqtt
