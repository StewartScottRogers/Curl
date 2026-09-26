# Requirements

> **TODO** — functional requirements are authored for the `file://`, `dict://`,
> `gopher://`/`gophers://`, `telnet://`, `tftp://` and `mqtt://`/`mqtts://` schemes,
> and for the command line's refusals (2026-09-26). The other Phase 1 option groups —
> HTTP, the rest of the command-line layer, and output formatting — are not yet.

Each requirement gets a stable identifier so planning, commits and tests can cite
it. Identifiers are never reused or renumbered, even after a requirement is
dropped — mark it `Withdrawn` instead.

## Functional

### The `file://` scheme

Every requirement below was checked against curl 8.21.0 (2026-06-24), the version
`Documentation/Product/Product-Overview.md` measures the compatibility surface
against, using the manpage, `docs/URL-SYNTAX.md` and `libcurl-errors` on
[curl.se](https://curl.se). A requirement marked with an open task describes
upstream behaviour this project has not yet built; it is not a claim that the
behaviour works today. Source code for the current handler lives in
`Curl.Protocol.File.UnitLibrary`.

| ID | Requirement | Upstream reference | Priority | Status | Checked against |
| --- | --- | --- | --- | --- | --- |
| FR-001 | A `file://` URL is accepted with an empty authority (`file:///path`), or an authority of `localhost` (compared ignoring case) or the literal `127.0.0.1`. Any other authority is rejected as exit 3 (`CURLE_URL_MALFORMAT`). | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-002 | An authority that is exactly one ASCII letter followed by `:` or `\|` is not a host at all; it is the head of a Windows drive path, so `file://C:/dir/x` and `file:///C:/dir/x` resolve to the same path and the `\|` spelling is kept, not rewritten to `:`. | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-003 | `file:////server/share` — an empty authority immediately followed by two more slashes — is accepted as the one Universal Naming Convention spelling curl recognises for `file://`, and both leading slashes of `//server/share` are preserved. | [URL syntax](https://curl.se/docs/url-syntax.html) | Should | Draft | curl 8.21.0 |
| FR-004 | Downloading a local file: a plain `file://` request reads the named file and writes its bytes, unaltered, to the transfer's output. A source that cannot be opened for reading is exit 37 (`CURLE_FILE_COULDNT_READ_FILE`), reported the same way whatever the operating-system reason. | [URL syntax](https://curl.se/docs/url-syntax.html); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |
| FR-005 | Uploading to a local file with `-T`/`--upload-file` truncates and (re)writes the destination, unless a positive `-C`/`--continue-at` offset is also given (FR-006). A destination that cannot be opened for writing is exit 23 (`CURLE_WRITE_ERROR`). Resolving a destination URL whose path names no file (it ends in `/` or is absent) by appending the source file's percent-encoded base name exists as `UploadUrl.AppendLocalFileNameWhenUrlNamesNoFile` in `Curl.Cli.UnitLibrary` (encoding: ADR-0004), but nothing calls it yet, because there is no option parser or transfer dispatcher; wiring it in is an open gap for a follow-up task. Until then this requirement covers only a `file://` URL that already names a file. | [`-T`/`--upload-file`](https://curl.se/docs/manpage.html#-T) | Must | Draft | curl 8.21.0 |
| FR-006 | `-C`/`--continue-at`: on download, a positive offset seeks the source and appends to the destination; on upload, a positive offset skips that many bytes of the local source before writing, and the destination is opened in append mode rather than truncated. An offset past the end of a download source is exit 36 (`CURLE_BAD_DOWNLOAD_RESUME`); the same offset past the end of an upload source is not an error. The option parser refuses a `-C` value that is not unsigned digits fitting a 64-bit offset (`-1`, `-0`, `abc`, `1e3`) with exit 2, `expected a proper numerical parameter`, and records `-C -` as resume-from-output-size; it does so before any URL is looked at, so `-C -5` with a malformed URL is exit 2, not exit 3. `FileProtocolHandler` still answers a negative `ResumeFrom` in a hand-built context with exit 36 as an unreachable defensive default (ADR-0007). | [`-C`/`--continue-at`](https://curl.se/docs/manpage.html#-C) | Must | Draft | curl 8.21.0 |
| FR-007 | `-r`/`--range` on download selects a byte window of a local file in any of curl's three forms (`first-last`, `first-`, `-suffix`). A first byte position past the end of the file is exit 36 (`CURLE_BAD_DOWNLOAD_RESUME`); a suffix asking for more than one byte more than the file holds is exit 36 with a distinct message from the `-C` case. `-r` combined with `-C` is refused by the option parser with exit 2 (`CURLE_FAILED_INIT`): `curl: --continue-at is mutually exclusive with --range` (hidden by an earlier `-s` without `-S`), `curl: option <whichever came second>: is badly used here`, and the try-help line. The range text is parsed once, by `ByteRangeParser` in `Curl.Core.UnitLibrary`, as libcurl's `Curl_range` reads it: text naming no range (`3-1`, `abc`, `-0`) is exit 33 (`CURLE_RANGE_ERROR`), `Requested range was not delivered by the server`. `Curl.Console` parses `-r` but does not yet pass the range into the transfer (task BL-095). | [`-r`/`--range`](https://curl.se/docs/manpage.html#-r) | Must | Draft | curl 8.21.0 |
| FR-008 | `-I`/`--head` suppresses the body of a `file://` transfer but still opens the resource — a directory still fails exactly as it would for a body request — and, when header output was asked for, still writes the pseudo-header block. | [`-I`/`--head`](https://curl.se/docs/manpage.html#-I) | Must | Draft | curl 8.21.0 |
| FR-009 | `-z`/`--time-cond` transfers the body of a local file only when its last-write time meets the given condition (newer than, for a plain date; older than, for a date prefixed with `-`); an unmet condition is a success with no body, exit 0. Both timestamps are compared at whole-second resolution and strictly in both directions, so a date equal to the file's timestamp transfers in neither direction (task BL-017), and an unmet condition writes no header block either (task BL-016). Treating an unknown source timestamp as "transfers" rather than as the year 0001 (task BL-018) is an open gap. | [`-z`/`--time-cond`](https://curl.se/docs/manpage.html#-z) | Should | Draft | curl 8.21.0 |
| FR-010 | `-i`/`--include` and `-D`/`--dump-header`: a `file://` transfer emits a synthesised header block — `Content-Length`, `Accept-ranges: bytes` and, when the opened handle has a known modification time, `Last-Modified` — before any body, to whichever stream the caller designated for headers. | [`-i`/`--include`](https://curl.se/docs/manpage.html#-i); [`-D`/`--dump-header`](https://curl.se/docs/manpage.html#-D) | Should | Draft | curl 8.21.0 |
| FR-011 | `-R`/`--remote-time`: the modification time of a downloaded local file, truncated to whole seconds, is carried back on `TransferResult.SourceLastWriteTimeUtc` (BL-019), and `Curl.Console` applies it to the `-o` file after a successful transfer, once the file is closed, through `IFileTimeSetter` (BL-079). As in curl, it is applied even when no body was written (an unmet `-z`), and a transfer to standard output has no file to stamp. curl's `Warning: Failed to set filetime` lines when the time cannot be set are not printed yet (BL-135). | [`-R`/`--remote-time`](https://curl.se/docs/manpage.html#-R) | Should | Draft | curl 8.21.0 |
| FR-012 | `--path-as-is`: a `file://` path has every backslash converted to a forward slash and then, unless `--path-as-is` is given, its `.` and `..` segments removed per RFC 3986 section 5.2.4, before the operating-system path is formed. A drive specification followed by `/` (or by nothing) is the root `..` cannot climb above, and a dot may be spelled `%2e`. `FileUrlPath.TryParse` does all of this, with a `pathAsIs` parameter that skips the dot-segment removal only (task BL-015); the exit 37 message quotes the path after both steps. Mapping the `--path-as-is` option onto that parameter is an open gap: there is no option parser yet, and nothing on `ITransferContext` carries the flag, so a transfer always removes dot segments. | [`--path-as-is`](https://curl.se/docs/manpage.html#--path-as-is); [URL syntax](https://curl.se/docs/url-syntax.html) | Should | Draft | curl 8.21.0 |
| FR-013 | `--crlf` on upload: uploading to a `file://` destination with `--crlf` inserts a carriage return before each line feed that does not already follow one, on the way to the destination, remembering the byte before across 16384-byte chunks; a lone carriage return is left alone, the reported byte count is the converted count, and a download ignores the option. `ITransferContext.ConvertLineEndings` carries it and `FileProtocolHandler` applies it (task BL-020); parsing `--crlf` on the command line is not yet done. | [`--crlf`](https://curl.se/docs/manpage.html#--crlf) | Could | Draft | curl 8.21.0 |
| FR-014 | `--create-file-mode` on POSIX: a file created by a `file://` upload receives the given octal mode on POSIX platforms, narrowed by the process umask as `open(2)` does; a file that already exists keeps its mode, the default is `0644`, and the option has no effect on Windows. As upstream documents, it applies to files an upload creates (`file://`, SFTP, SCP), not to `-o`/`--output`. The value is octal digits only, at most `0777`: anything else (a sign, whitespace, `8`, `0x7`, trailing text) is exit 2 with "expected a proper numerical parameter", and a value past `0777` is exit 2 with "too large number" (measured on curl 8.21.0). `CommandLineOptions.CreateFileMode` parses it, `ITransferContext.CreateFileMode` carries it, `FileProtocolHandler` passes it to `IFileSystem.OpenForWriteAsync`, and `PhysicalFileSystem` applies it (task BL-011); copying the parsed value onto the transfer context waits on the transfer dispatcher, like the other options. | [`--create-file-mode`](https://curl.se/docs/manpage.html#--create-file-mode) | Could | Draft | curl 8.21.0 |
| FR-015 | `--max-filesize`: a `file://` download with more body bytes than the limit writes exactly the limit, then fails with exit 63 (`CURLE_FILESIZE_EXCEEDED`), `Exceeded the maximum allowed file size (<limit>) with <bytes> bytes`; headers do not count, `0` means no limit, and an upload ignores it. The value takes curl's `b`/`k`/`m`/`g`/`t`/`p` units and fractions. `Curl.Console` parses it but does not yet pass it into the transfer (task BL-095). | [`--max-filesize`](https://curl.se/docs/manpage.html#--max-filesize); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Should | Draft | curl 8.21.0 |
| FR-016 | A `file://` URL whose authority is not empty, `localhost`, `127.0.0.1`, or a drive spelling is rejected before any file-system access, as exit 3 (`CURLE_URL_MALFORMAT`). | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-017 | A write failure to a download's destination is reported through `%{errormsg}` as `Failure writing output to destination, passed <n> returned 0`, where `<n>` is the size of the chunk offered to the destination, with exit 23 (`CURLE_WRITE_ERROR`). | [`--write-out`](https://curl.se/docs/manpage.html#-w); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Should | Draft | curl 8.21.0 |
| FR-018 | A failed `file://` transfer reports the number of bytes that reached the destination before the failure, not zero, for `%{size_download}` and `%{size_upload}` once `--write-out` exists. `TransferResult.Failure` carries the count, and the `file` handler reports it on its write failure (exit 23) and its upload read failure (exit 26) (task BL-022); nothing consumes it until `--write-out` exists. | [`--write-out`](https://curl.se/docs/manpage.html#-w) | Should | Draft | curl 8.21.0 |
| FR-019 | A `file://` path is handed to the operating system exactly as parsed, with no sandboxing or confinement between the URL and the local file system: a path such as `file:///C:/Windows/win.ini` is reachable like any other. | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |

### The `dict://` scheme

Every requirement below was checked against curl 8.21.0 (2026-06-24) on 2026-09-26,
by pointing the curl binary at a loopback listener that recorded what curl sent and
replied with canned bytes, and against the manpage, `docs/URL-SYNTAX.md` and
`libcurl-errors` on [curl.se](https://curl.se). The default port is 2628. None of
these behaviours is implemented yet; each row describes upstream behaviour, not a
claim that it works today. The handler will live in `Curl.Protocol.Dict.UnitLibrary`.

| ID | Requirement | Upstream reference | Priority | Status | Checked against |
| --- | --- | --- | --- | --- | --- |
| FR-020 | A `dict://` transfer sends its whole request without waiting for the server's greeting: `CLIENT libcurl 8.21.0\r\n`, then one command line, then `QUIT\r\n`. Not yet implemented. | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-021 | A path of `/d:word` or `/lookup:word` sends `DEFINE ! word`; `/d:word:db` sends `DEFINE db word`. Not yet implemented. | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-022 | A path of `/m:word:db:prefix` sends `MATCH db prefix word`; `/find:word` sends `MATCH ! . word`. Not yet implemented. | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-023 | Any other path is sent as the command after percent-decoding: `/word` sends `word`, `/show%20db` sends `show db`, and `/` sends an empty line. Not yet implemented. | [URL syntax](https://curl.se/docs/url-syntax.html) | Should | Draft | curl 8.21.0 |
| FR-024 | Every byte the `dict://` server sends is written to the transfer's output unaltered, and the transfer ends with exit 0 (`CURLE_OK`). Not yet implemented. | [URL syntax](https://curl.se/docs/url-syntax.html); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |

### The `gopher://` and `gophers://` schemes

Every requirement below was checked against curl 8.21.0 (2026-06-24) on 2026-09-26,
by pointing the curl binary at a loopback listener that recorded what curl sent and
replied with canned bytes, and against the manpage, `docs/URL-SYNTAX.md` and
`libcurl-errors` on [curl.se](https://curl.se). The default port is 70 for both
schemes; `gophers://` is gopher over TLS. None of these behaviours is implemented yet;
each row describes upstream behaviour, not a claim that it works today. The handler
will live in `Curl.Protocol.Gopher.UnitLibrary`.

| ID | Requirement | Upstream reference | Priority | Status | Checked against |
| --- | --- | --- | --- | --- | --- |
| FR-025 | The selector sent is the URL path with its leading `/` and item-type character removed, percent-decoded, followed by CRLF: `/` and `/1` send `\r\n`; `/1/foo` sends `/foo\r\n`; `/0/a%09b` sends `/a\tb\r\n`; `/7/search%09term%20x` sends `/search\tterm x\r\n`. Not yet implemented. | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-026 | The gopher response is written to the transfer's output unaltered, and the transfer ends with exit 0 (`CURLE_OK`). Not yet implemented. | [URL syntax](https://curl.se/docs/url-syntax.html); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |

### The `telnet://` scheme

Every requirement below was checked against curl 8.21.0 (2026-06-24) on 2026-09-26,
by pointing the curl binary at a loopback listener that recorded what curl sent and
replied with canned bytes, and against the manpage, `docs/URL-SYNTAX.md`,
`CURLOPT_TELNETOPTIONS` and `libcurl-errors` on [curl.se](https://curl.se). The
default port is 23. None of these behaviours is implemented yet; each row describes
upstream behaviour, not a claim that it works today. The handler will live in
`Curl.Protocol.Telnet.UnitLibrary`.

| ID | Requirement | Upstream reference | Priority | Status | Checked against |
| --- | --- | --- | --- | --- | --- |
| FR-027 | The bytes read from standard input are sent to the server unchanged (`a\nb\n` is sent as `a\nb\n`); with empty input nothing is sent. Not yet implemented. | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-028 | Received data is written to the transfer's output with telnet command sequences removed, `IAC IAC` is written as a single `0xFF` byte, and a server's `IAC WILL ECHO` is answered with `IAC DO ECHO`. Not yet implemented. | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-029 | `-t TTYPE=vt100` answers `IAC DO TTYPE` with `IAC WILL TTYPE`, and `IAC SB TTYPE SEND IAC SE` with `IAC SB TTYPE IS vt100 IAC SE`; `-t` option names are matched ignoring case. Not yet implemented. | [`-t`/`--telnet-option`](https://curl.se/docs/manpage.html#-t); [`CURLOPT_TELNETOPTIONS`](https://curl.se/libcurl/c/CURLOPT_TELNETOPTIONS.html) | Should | Draft | curl 8.21.0 |
| FR-030 | An unknown `-t` option name is exit 48 (`CURLE_UNKNOWN_OPTION`), `An unknown option was passed in to libcurl`; a `-t` value with no `=` is exit 49 (`CURLE_SETOPT_OPTION_SYNTAX`), `Syntax error in telnet option: TTYPE`. Not yet implemented. | [`CURLOPT_TELNETOPTIONS`](https://curl.se/libcurl/c/CURLOPT_TELNETOPTIONS.html); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Should | Draft | curl 8.21.0 |
| FR-031 | A telnet session ends with exit 0 (`CURLE_OK`) when the server closes the connection. Not yet implemented. | [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |

### The `tftp://` scheme

Every requirement below was checked against curl 8.21.0 (2026-06-24) on 2026-09-26,
by pointing the curl binary at a loopback listener that recorded what curl sent and
replied with canned bytes, and against the manpage, `docs/URL-SYNTAX.md`,
`CURLOPT_TFTP_BLKSIZE`, `CURLOPT_TFTP_NO_OPTIONS` and `libcurl-errors` on
[curl.se](https://curl.se). The default port is 69. None of these behaviours is
implemented yet; each row describes upstream behaviour, not a claim that it works
today. The handler will live in `Curl.Protocol.Tftp.UnitLibrary`.

| ID | Requirement | Upstream reference | Priority | Status | Checked against |
| --- | --- | --- | --- | --- | --- |
| FR-032 | A download sends the read request `00 01 <file> 00 "octet" 00 "tsize" 00 "0" 00 "blksize" 00 "512" 00 "timeout" 00 "6" 00`, acknowledges each DATA block with `00 04 <block>`, and ends at a block shorter than the block size. Not yet implemented. | [URL syntax](https://curl.se/docs/url-syntax.html) | Must | Draft | curl 8.21.0 |
| FR-033 | An option acknowledgement (OACK) is answered with an ACK of block 0, and an acknowledged `blksize` is used for the rest of the transfer. Not yet implemented. | [`CURLOPT_TFTP_BLKSIZE`](https://curl.se/libcurl/c/CURLOPT_TFTP_BLKSIZE.html) | Must | Draft | curl 8.21.0 |
| FR-034 | An upload with `-T`/`--upload-file` sends the write request with `tsize` set to the upload's length, then DATA blocks from block 1. Not yet implemented. | [`-T`/`--upload-file`](https://curl.se/docs/manpage.html#-T) | Should | Draft | curl 8.21.0 |
| FR-035 | A TFTP ERROR packet maps to an exit code and message: code 1 to exit 68 (`CURLE_TFTP_NOTFOUND`), `TFTP: File Not Found`; 2 to exit 69 (`CURLE_TFTP_PERM`), `TFTP: Access Violation`; 3 to exit 70 (`CURLE_REMOTE_DISK_FULL`), `Disk full or allocation exceeded`; 0 and 4 to exit 71 (`CURLE_TFTP_ILLEGAL`), `TFTP: Illegal operation`; 5 to exit 72 (`CURLE_TFTP_UNKNOWNID`), `TFTP: Unknown transfer ID`; 6 to exit 73 (`CURLE_REMOTE_FILE_EXISTS`), `Remote file already exists`; 7 to exit 74 (`CURLE_TFTP_NOSUCHUSER`), `TFTP: No such user`; 8 to exit 42 (`CURLE_ABORTED_BY_CALLBACK`), `Operation was aborted by an application callback`. Not yet implemented. | [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |
| FR-036 | A `tftp://` URL with no file name is exit 71 (`CURLE_TFTP_ILLEGAL`), `Missing filename`, and no packet is sent. Not yet implemented. | [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Should | Draft | curl 8.21.0 |
| FR-037 | `--tftp-no-options` sends the request with no options; `--tftp-blksize` is clamped to 8-65464 (5 is sent as 8, 70000 as 65464). Not yet implemented. | [`--tftp-no-options`](https://curl.se/docs/manpage.html#--tftp-no-options); [`--tftp-blksize`](https://curl.se/docs/manpage.html#--tftp-blksize); [`CURLOPT_TFTP_NO_OPTIONS`](https://curl.se/libcurl/c/CURLOPT_TFTP_NO_OPTIONS.html); [`CURLOPT_TFTP_BLKSIZE`](https://curl.se/libcurl/c/CURLOPT_TFTP_BLKSIZE.html) | Could | Draft | curl 8.21.0 |

### The `mqtt://` and `mqtts://` schemes

Every requirement below was checked against curl 8.21.0 (2026-06-24) on 2026-09-26,
by pointing the curl binary at a loopback listener that recorded what curl sent and
replied with canned bytes, and against the manpage, `docs/URL-SYNTAX.md`,
`docs/MQTT.md` and `libcurl-errors` on [curl.se](https://curl.se). The default port
for `mqtt://` is 1883; `mqtts://` is MQTT over TLS. None of these behaviours is
implemented yet; each row describes upstream behaviour, not a claim that it works
today. The handler will live in `Curl.Protocol.Mqtt.UnitLibrary`.

| ID | Requirement | Upstream reference | Priority | Status | Checked against |
| --- | --- | --- | --- | --- | --- |
| FR-038 | curl connects with an MQTT 3.1.1 CONNECT: protocol level 4, clean session, keep-alive 60 seconds, and a client identifier of `curl` followed by eight random alphanumeric characters. Not yet implemented. | [MQTT](https://curl.se/docs/mqtt.html) | Must | Draft | curl 8.21.0 |
| FR-039 | Without `-d`, curl subscribes to the topic at QoS 0 with packet identifier 1 and writes each received PUBLISH to the transfer's output as a two-byte big-endian topic length, the topic, then the payload. When the server closes the connection the transfer is exit 56 (`CURLE_RECV_ERROR`), `Connection disconnected`. Not yet implemented. | [MQTT](https://curl.se/docs/mqtt.html); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |
| FR-040 | The topic is the URL path without its leading `/`, percent-decoded: `/a%2Fb` subscribes to `a/b`. Not yet implemented. | [MQTT](https://curl.se/docs/mqtt.html) | Must | Draft | curl 8.21.0 |
| FR-041 | With `-d`, curl publishes the data to the topic at QoS 0, writes nothing to the output, and exits 0 (`CURLE_OK`); when the server keeps the connection open, curl then sends DISCONNECT (`E0 00`). Not yet implemented. | [MQTT](https://curl.se/docs/mqtt.html); [`-d`/`--data`](https://curl.se/docs/manpage.html#-d) | Must | Draft | curl 8.21.0 |
| FR-042 | With `-u user:password`, split at the first colon (`bob:se:cret` gives password `se:cret`), the CONNECT carries the user name and password (connect flags `0xC2`); user information in the URL (`mqtt://al:pw@host/t`) does the same. Not yet implemented. | [`-u`/`--user`](https://curl.se/docs/manpage.html#-u); [MQTT](https://curl.se/docs/mqtt.html) | Should | Draft | curl 8.21.0 |
| FR-043 | A CONNACK with a non-zero return code is exit 8 (`CURLE_WEIRD_SERVER_REPLY`); return code 5 gives `Expected 0000 but got 0005`. Not yet implemented. | [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |
| FR-044 | An empty topic is exit 3 (`CURLE_URL_MALFORMAT`), `No MQTT topic found. Forgot to URL encode it?`, reported after the CONNECT is sent. Not yet implemented. | [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Should | Draft | curl 8.21.0 |
| FR-045 | Publishing uses QoS 0 only, and the retain flag cannot be set. Not yet implemented. | [MQTT](https://curl.se/docs/mqtt.html) | Could | Draft | curl 8.21.0 |

### The command line

Every requirement below was checked against curl 8.21.0 (2026-06-24) on 2026-09-26, by
running the local curl binary with each command line and recording its standard error
and exit code, and against the manpage and `libcurl-errors` on
[curl.se](https://curl.se). Each refusal writes exactly two lines to standard error and
nothing to standard output, and exits 2 (`CURLE_FAILED_INIT`); `<spelled>` is the whole
argument as typed. `CommandLineParser` and `CommandLineRefusal` in
`Curl.Cli.UnitLibrary` implement these rows, and `Curl.Cli.UnitTests` asserts the exact
lines; `Curl.Console` does not call the parser yet, so the executable does not refuse
anything this way today. How the parser reads a command line is in
`Documentation/Wiki/Command-Line-Parsing.md`.

| ID | Requirement | Upstream reference | Priority | Status | Checked against |
| --- | --- | --- | --- | --- | --- |
| FR-046 | An option whose long name (matched exactly and case-sensitively, with no prefix matching) or short letter curl does not know, or a lone `-`, is refused with `curl: option <spelled>: is unknown` then `curl: try 'curl --help' or 'curl --manual' for more information`, exit 2 (`CURLE_FAILED_INIT`); `--bogus=x` and a bundle such as `-s!x` are named whole. `--no-<name>` turns off a negatable flag (`--silent`, `--show-error`, `--insecure`, `--tftp-no-options`), the last spelling winning; the `--no-` spelling of any other known option (`--no-tlsv1.2`, `--no-output`) is refused with `curl: option <spelled>: the given option cannot be reversed with a --no- prefix` then the try-help line, exit 2; `--no-bogus`, `--no-no-silent` and `--no-Silent` are unknown. | [manpage](https://curl.se/docs/manpage.html); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |
| FR-047 | An option that takes a value but is the last argument, with no attached value, is refused with `curl: option <spelled>: requires parameter` then `curl: try 'curl --help' or 'curl --manual' for more information`, exit 2 (`CURLE_FAILED_INIT`); a bundle is named whole (`-so` gives `curl: option -so: requires parameter`). | [manpage](https://curl.se/docs/manpage.html); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |
| FR-048 | An empty value for a text option (`-o`/`--output`, `--url`), attached or separate, is refused with `curl: option <spelled>: blank argument where content is expected` then `curl: try 'curl --help' or 'curl --manual' for more information`, exit 2 (`CURLE_FAILED_INIT`); an empty positional argument gives the same reason with nothing spelled, `curl: option : blank argument where content is expected`. `-d`/`--data`, `-u`/`--user` and `-t`/`--telnet-option` accept an empty value, and a numeric option refuses it as `expected a proper numerical parameter` (FR-049, FR-014). | [manpage](https://curl.se/docs/manpage.html); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |
| FR-049 | A decimal numeric option value (`--tftp-blksize`) that is not an optional `-` followed by ASCII digits only (empty, whitespace, `+`, hexadecimal, fractions, trailing text) is refused with `curl: option <spelled>: expected a proper numerical parameter` then `curl: try 'curl --help' or 'curl --manual' for more information`, exit 2 (`CURLE_FAILED_INIT`); for example `--tftp-blksize abc`. `--tftp-blksize 99999999999` is refused the same way; which ceiling Curl should apply is an open decision (task BL-053). | [manpage](https://curl.se/docs/manpage.html); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |
| FR-050 | A negative decimal numeric option value is refused with `curl: option <spelled>: expected a positive numerical parameter` then `curl: try 'curl --help' or 'curl --manual' for more information`, exit 2 (`CURLE_FAILED_INIT`); for example `--tftp-blksize -1`. `-0` is accepted as zero. | [manpage](https://curl.se/docs/manpage.html); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |
| FR-051 | A non-empty command line read without any other refusal that names no URL (`-s`, `--`, `-o file`) is refused with `curl: (2) no URL specified` then `curl: try 'curl --help' or 'curl --manual' for more information`, exit 2 (`CURLE_FAILED_INIT`). A refusal met while reading the arguments wins over it. A command line with no arguments at all prints only the second line, exit 2; matching that is an open gap (task BL-082), and today it is accepted. | [manpage](https://curl.se/docs/manpage.html); [exit codes](https://curl.se/libcurl/c/libcurl-errors.html) | Must | Draft | curl 8.21.0 |

Priorities use MoSCoW (Must / Should / Could / Won't). "Must" means the release is
not shippable without it — if everything is a Must, nothing is.

## Non-functional

Qualities rather than behaviours. Each one needs a number, or it is not a
requirement but a wish.

| ID | Quality | Target | Status |
| --- | --- | --- | --- |
| NFR-001 | > **TODO** (e.g. throughput, latency, footprint) | measurable target | Draft |

## Out of scope

Requirements considered and explicitly rejected, with the reason. Keeping them
here stops them being re-proposed every few months.

## Open questions

Unresolved points that block requirements from leaving `Draft`. Each should name
who can answer it.
