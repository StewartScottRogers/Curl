# ADR-0225 — SCP downloads run `scp -pf` with `exec` and parse its header as libssh2 does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-574.

## Context

After authentication (ADR-0215) curl downloads an `scp://` file through libssh2's
`libssh2_scp_recv2`. SCP has no RFC: the wire protocol is whatever OpenSSH's `scp` and
libssh2 do. Which command curl runs, which bytes it sends back, how strictly it reads the
server's lines and what each failure prints had to be measured.

The Windows reference build (curl 8.21.0, libssh2 1.11.1, Schannel) was run with `-sS -k
--key <rsa key> -u <user>:` against OpenSSH 10.2 (the WSL setup of ADR-0220), with a
`ForceCommand` wrapper that logged the command line and the bytes curl wrote, and ran
either OpenSSH's own `scp` or a scripted one that answered by the file's name. The
parsing rules were then read from libssh2 1.11.1's `src/scp.c` (`scp_recv`,
`shell_quotearg`) and `src/channel.c`, and each measured case matched them.

| Case | Exit | stdout / stderr |
| --- | ---: | --- |
| 11-byte file | 0 | the bytes |
| empty file, and `-w '%{size_download}'` | 0 | `0` |
| `scp://h/~/file`, `scp://h/%7E/file`, `scp://h/a%20b.txt` | 0 | the bytes |
| 3,000,000 bytes, `-w '%{size_download}'` | 0 | `3000000` |
| missing file, unreadable file, directory (with or without `/`) | 78 | `curl: (78) Failed to recv file` |
| `\x02` error line, empty `\x01` line, a `C` or junk line first, no line end | 78 | `Failed to recv file` |
| error line or `D` line after the `T` line | 78 | `Invalid response from SCP server` |
| mode `06x4` or `abc` / size `5x` | 78 | `Invalid response from SCP server, invalid mode` / `invalid size` |
| no name, two spaces before the size | 78 | `Invalid response from SCP server, too short or malformed` |
| a tab in the `C` line, letters in the `T` line | 78 | `Invalid data in SCP response` |
| a `C` line of 300 bytes | 78 | `Unterminated response from SCP server` |
| channel ends with no line, or after `C0 5\n` | 78 | `Unexpected channel close` |
| `T1 0 1\n` (a line feed before 9 bytes) | 28 | curl reads on into the next line and waits until `-m` |
| size 10, 5 bytes sent | 18 | the 5 bytes; `end of response with 5 bytes missing` |
| size `-5` | 18 | nothing; `transfer closed with -5 bytes remaining to read` |
| size `-1` | 0 | every byte to the channel's end, the trailing zero included |
| bytes after the size, an error line after them, standard error between them | 0 | the file's bytes only |
| mode `107777` or `644`, size `+5` or `05`, `\r\n` line end | 0 | the bytes |
| connection killed inside the `T` line | 79 | `Failed reading SCP response` |
| connection killed after 5 of 10 bytes | 79 | the 5 bytes; `Error in the SSH layer` |
| `MaxSessions 0` (channel refused, reason 2) | 79 | `Channel open failure (connect failed)` |

Every download ran `exec` with `scp -pf '<path>'`, and curl wrote exactly three bytes into
the channel: a zero after the `exec` request succeeded, one after the `T` line and one
after the `C` line; none after the file's bytes. The path `/x/it's!''here` was sent as
`'/x/it'"'"'s'\!"''"'here'`; `/~/f` as `'f'`; `/~/` and `/~` as they are.

## Decision

- **`Scp.ScpCommand`** builds `scp -pf ` and the path quoted by libssh2's
  `shell_quotearg`: ordinary bytes in single quotes, apostrophes in double quotes, and
  `!` escaped with a backslash outside any quotes.
- **`Scp.ScpRemotePath`** percent-decodes the URL path (`SftpRemotePath.Decode`) and drops
  a leading `/~/` when anything follows it, as curl's `Curl_getworkingpath` does for SCP.
- **`Connection.SshSessionChannel`** gains `RequestExecAsync` (an `exec` request with a
  reply wanted, sharing the subsystem request's code) and keeps the reason code of a
  refused open in `OpenFailureReasonCode`.
- **`Scp.ScpFileHeaderReader`** sends the zero wakeup, then reads the `T` and `C` lines a
  byte at a time with libssh2's checks, in libssh2's order: the first byte (`T`, else
  `Failed to recv file`; `C`, else `Invalid response from SCP server`), each further byte
  (`T`: digits, space, CR, LF; `C`: no control byte but CR and LF; else `Invalid data in
  SCP response`), completion at a line feed once the line holds 9 (`T`) or 7 (`C`) bytes,
  the 256-byte buffer (`Unterminated response from SCP server`), the length once line
  ends are trimmed (8 or 6, else `..., too short`), then the fields: `malformed mtime`,
  `malformed mtime.usec`, `too short or malformed` for `T`; `malformed mode`, `invalid
  mode`, `too short or malformed`, `invalid size` for `C`. Mode and size are read as C's
  `strtol` reads them (`ScpHeaderNumber`); the times, mode and name are not used, as curl
  does not use them. Each refusal is exit 78 with libssh2's message; a channel that ends
  is `Unexpected channel close`; a broken connection is exit 79 `Failed reading SCP
  response`.
- **`Scp.ScpFileDownload`** opens the channel (refused: exit 79 with libssh2's text for
  the reason code), runs the command (refused: exit 79 `Unable to complete request for
  channel-process-startup`, from libssh2's source), reads the header, copies exactly the
  header's size to the output and reports progress, then closes the channel with
  `SshSessionChannel.CloseAsync` (EOF, the server's CLOSE, its own), ignoring a broken
  connection. Size -1 is unknown and read to the channel's end; any other negative size
  reads nothing and is exit 18 `transfer closed with <size> bytes remaining to read`; a
  channel that ends early is exit 18 `end of response with <n> bytes missing`; a
  connection that breaks during the copy is exit 79 `Error in the SSH layer`, each with
  the bytes so far.

## Consequences

- A header failure throws with the channel still open, as a failed SFTP open does
  (ADR-0220); libssh2 frees the channel there, but the handler (BL-576) ends the
  session either way and nothing observable differs.
- A connection that breaks before the `exec` answer is exit 79 with libssh2's message for
  the step: `Unexpected error` at the channel open, `Failed waiting for channel success`
  at the `exec` request, as measured in BL-1046 (ADR-0289).
- Where curl waits for more of a line that its length rule leaves open (`T1 0 1\n`), this
  library waits too, so `-m` ends the transfer as it ends curl's.
- Uploads (BL-577), the handler (BL-576) and the `-v` lines (BL-578) build on these
  classes.
