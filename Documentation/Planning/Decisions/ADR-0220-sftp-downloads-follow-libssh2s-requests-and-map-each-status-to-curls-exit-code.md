# ADR-0220 — SFTP downloads follow libssh2's requests and map each status to curl's exit code

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-569.

## Context

After authentication (ADR-0215) curl downloads an `sftp://` file through libssh2's SFTP
layer. BL-569 builds the session channel (RFC 4254) and the SFTP version 3 client
(draft-ietf-secsh-filexfer-02) for that download. Which requests curl sends, in which
order, and what each failure prints had to be measured.

The Windows reference build (curl 8.21.0, libssh2 1.11.1, Schannel) was run with `-sS -k
--key <rsa key> -u <user>:` against OpenSSH 10.2 (Ubuntu's `openssh-server` package,
unpacked in WSL, public-key authentication), once with OpenSSH's own `sftp-server` and
once with a scripted SFTP subsystem that logged every request and answered by path. The
channel's parameters, window adjustments and teardown came from OpenSSH's `DEBUG3` log.

| Case | Requests | Exit | stderr / stdout |
| --- | --- | ---: | --- |
| 11-byte file (`sftp-server`) | | 0 | the bytes |
| empty file, and `-w '%{size_download}'` | | 0 | `0` |
| missing file | | 78 | `curl: (78) Could not open remote file for reading: No such file or directory` |
| file mode 000 | | 9 | `curl: (9) Could not open remote file for reading: Permission denied` |
| `sftp://h/~/file`, `sftp://h/a%20b.txt` | | 0 | the bytes |
| 3,000,000 bytes, `-w '%{size_download}'` | | 0 | `3000000` |
| a directory named without a slash | | 79 | `curl: (79) Error in the SSH layer` |
| 5-byte file (scripted) | `INIT` 3; `REALPATH .` (id 0); `OPEN` path, `READ`, attrs `PERMISSIONS` 0100644 (1); `STAT` path (2); `READ` 0, 20 (3); `CLOSE` (4) | 0 | |
| `STAT` size 0, failed or without size, file of 0 or 5 bytes | 14 `READ`s of 30000 in flight | 0 | the bytes |
| 100000 bytes | 14 `READ`s of 30000 | 0 | |
| `STAT` size 10, file of 5 | `READ` 0, 40; `READ` 5, 20 | 18 | `curl: (18) end of response with 5 bytes missing` |
| first `READ` answered `EOF`, size 5 | | 18 | `... with 5 bytes missing` |
| `READ` answered 2, 3 or 4 | `CLOSE` still sent | 79 | `curl: (79) Error in the SSH layer` |
| `OPEN` answered with code *N* | no `CLOSE` | per table | `Could not open remote file for reading: <text>` |
| `OPEN` answered `OK` | | hangs | curl waits for another answer |
| `REALPATH` answered 2 / 3 / 4 | | 78 / 9 / 79 | `Remote file not found` / `Access denied to remote resource` / `Error in the SSH layer` |
| `/~/f`, `/~/dir/f`, `/a%20b.txt`, `/x%2Fy` | opens `<home>/f`, `<home>/dir/f`, `/a b.txt`, `/x/y` | 0 | |
| no `Subsystem sftp` in `sshd_config` | | 2 | `curl: (2) Failure initializing sftp session: Unable to request SFTP subsystem` |
| connection killed before `VERSION` | | 2 | `... Timeout waiting for response from SFTP subsystem` |
| subsystem exits before `VERSION`; `STATUS` or a 1- or 2-byte packet in its place | | 28 | curl waits until its timeout |
| `VERSION` 2 or 4 | | 0 | accepted |
| `VERSION` extension name / data cut short | | 2 | `... Data too short when extracting extname` / `extdata` |

`OPEN` statuses: 2 and 10 are exit 78; 3, 12 and 17 exit 9; 14 and 15 exit 70; 11 exit
73; 18 exit 21; every other code, 1 included, exit 79. The texts are `No such file or
directory`, `Permission denied`, `Operation failed`, `Bad message from SFTP server`, `Not
connected to SFTP server`, `Connection to SFTP server lost`, `Operation not supported by
SFTP server`, `Invalid handle`, `No such file or directory`, `File already exists`, `File
is write protected`, `No media`, `Disk full`, `User quota exceeded`, `Unknown principal`,
`File lock conflict`, `Directory not empty`, `Not a directory`, `Invalid filename`, `Link
points to itself` for 2 to 21, and `Unknown error in libssh2` for 1, 22 and 99.

OpenSSH logged curl's `CHANNEL_OPEN` as `session`, window 2097152, packet 32768, the
subsystem request with a reply wanted, five window adjustments of about 540 KB during the
3 MB download, and at the end curl's `CHANNEL_EOF`, then its `CHANNEL_CLOSE` only after
the server's.

## Decision

- **`Connection.SshSessionChannel`** opens one `session` channel with libssh2's window and
  packet size, requests a subsystem with a reply wanted, sends data within the server's
  window and packet size (waiting for `WINDOW_ADJUST` when the window is spent), reads the
  channel's data as a stream, and closes with `EOF`, the server's `CLOSE`, then its own.
  It grants the server the whole window again once less than three quarters remain,
  which gives the ~540 KB adjustments measured; libssh2's own rule also counts its read
  buffer, which is not observable. It answers a `KEXINIT` with a re-exchange and refuses
  a global or channel request that wants a reply; extended data is counted and dropped.
- **`Sftp.SftpSession`** frames packets with their length (at most libssh2's 256 KiB),
  numbers requests from 0 and matches each answer by number, skipping others. Starting
  maps a refused channel to `Unable to startup channel`, a refused subsystem to `Unable
  to request SFTP subsystem`, and a connection or channel that ends before `VERSION` to
  `Timeout waiting for response from SFTP subsystem`, all exit 2. Packets that are not a
  `VERSION` of at least 5 bytes are skipped, and any version is accepted.
- **`Sftp.SftpFileDownload`** sends `REALPATH .`, resolves a leading `/~/` against its
  answer (`SftpRemotePath`, after percent-decoding to raw bytes; extended by ADR-0277 to
  `/~` itself, a home directory ending with `/`, and exit 3 for a `%00`), opens with `READ` and the
  permissions attribute `S_IFREG | --create-file-mode`, asks `STAT` for the size (0, a
  failure, no size flag or cut-short attributes are an unknown size), reads, writes each
  answer to the output and reports progress, and closes the handle, ignoring how the
  close ends. The channel stays open for the handler (BL-576) to shut down. (Superseded
  for failures before the copy by ADR-0274: they close the channel first, as measured.)
- **`Sftp.SftpReadAhead`** keeps 4 × min(remaining, 102400) bytes of reads in flight, in
  reads of min(that, 30000) bytes, rounded up to whole reads: one read of 20 bytes for a 5-byte
  file, 14 of 30000 for an unknown size or 100000 bytes, as measured. A short answer drops
  the reads in flight and reading goes on from where it ended.
- **Outcomes:** a failed `OPEN` is its status's exit code and `Could not open remote file
  for reading: <text>` (`SftpStatusCode`); a failed `REALPATH` is the status's exit code
  and that exit code's own text; an `OK` answer to `OPEN` is waited past. A read answered
  with anything but `EOF`, a malformed answer, or a connection that breaks during the copy
  is exit 79 `Error in the SSH layer` with the bytes so far; `EOF` before the `STAT` size is
  exit 18 `end of response with <n> bytes missing`. A connection that breaks between the
  session start and the copy is exit 79 too: not measured, it is curl's code for an SSH
  failure with no message of its own.

## Consequences

- Where curl hangs until its timeout (a subsystem that exits, a `STATUS` in place of
  `VERSION`, an `OPEN` answered `OK`), this library keeps reading too, so `-m` ends the
  transfer as it ends curl's; a channel that ends before `VERSION` fails at once instead,
  with the message measured for a lost connection.
- An empty `DATA` answer counts as the end of the file, so a server that keeps sending
  one cannot hold the download in a loop.
- Directory listing (`sftp://h/dir/`, BL-570), uploads (BL-571), quote commands (BL-572),
  ranges and resumes (BL-573), the handler that wires this into `Curl.Console` (BL-576),
  and its `-v` lines (BL-578) build on these classes.
