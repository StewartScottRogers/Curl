# ADR-0244 — SFTP uploads open, write and create directories as libssh2 does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-571.

## Context

`-T file sftp://host/path` uploads a file, and `-C`, `-a`, `--create-file-mode` and
`--ftp-create-dirs` change how. What curl sends for each, and what each failure reports,
had to be measured before `Curl.Protocol.Ssh.UnitLibrary` could upload.

The Windows reference build (curl 8.21.0, libssh2 1.11.1, Schannel) was run with `-sS -k
--key <rsa key> --pubkey <pub> -u <user>: -w '[%{size_upload}]'` through
`Record-CurlExchange.ps1 -NoServer` against OpenSSH 10.2 in WSL, the setup ADR-0220
describes: once with OpenSSH's own `sftp-server` at `-l DEBUG3`, which logs every request
with its flags, mode, offset and length, and once with a scripted SFTP subsystem (a
throwaway C# app, not committed) that answered `WRITE` and `MKDIR` by path. The source was
a 13-byte file unless the row says otherwise.

| Case | Requests after `INIT`, `REALPATH .` | Exit | `%{size_upload}` / stderr / remote file |
| --- | --- | ---: | --- |
| new file | `OPEN` `WRITE,CREATE,TRUNCATE` mode `0100644`, `WRITE` 0+13, `CLOSE` | 0 | 13 / - / 13 bytes, `-rw-r--r--` |
| existing 26-byte file, mode 0600 | as above | 0 | 13 / - / 13 bytes, still `-rw-------` |
| `--create-file-mode 0600` | `OPEN` mode `0100600` | 0 | 13 / - / `-rw-------` |
| 3,000,000 bytes, from a file or from standard input | `WRITE`s of 30000, 30000, 5536 per 64 KiB block from a file; pipe-sized from standard input | 0 | 3000000 |
| missing directory | `OPEN` answered 2 | 78 | 0 / `Upload failed: No such file or directory (2/-31)` |
| read-only file | `OPEN` answered 3 | 9 | 0 / `Upload failed: Permission denied (3/-31)` |
| `OPEN` answered 1 / 4 / 11 | | 79 / 79 / 73 | `Upload failed: Unknown error in libssh2 (1/-31)` / `Operation failed (4/-31)` / `File already exists (11/-31)` |
| `--ftp-create-dirs`, `/home/u/bl571/files/d1/d2/n.txt` | `OPEN` answered 2, `MKDIR` `/home`, `/home/u`, ... `/home/u/bl571/files/d1/d2`, each with permissions `040755`, the first five answered 4, then `OPEN` again, `WRITE`, `CLOSE` | 0 | 13 / - / directories `drwxr-xr-x` |
| same, the first `OPEN` answered 4 or 10 | directories made as above | 0 | 13 |
| same, the first `OPEN` answered 3 | no `MKDIR` | 9 | `Upload failed: Permission denied (3/-31)` |
| same, `MKDIR` answered 3, 4 or 11 | every `MKDIR`, then `OPEN` again, answered 2 | 78 | `Creating the dir/file failed: No such file or directory` |
| same, the first `MKDIR` answered 2 / 1 / 5 | no more requests | 78 / 79 / 79 | `Remote file not found` / `Error in the SSH layer` / `Error in the SSH layer` |
| `/~/mkdir/.../x.txt` with `--ftp-create-dirs` | `MKDIR` from `/home` down the resolved path | 0 | |
| `-C 5`, remote 25 bytes | `OPEN` `WRITE` alone, `WRITE` 5+8 | 0 | 8 / - / bytes 5-12 replaced |
| `-C -`, remote 5 bytes | `STAT`, `OPEN` `WRITE`, `WRITE` 5+8 | 0 | 8 |
| `-C -`, remote missing, `STAT` with no size, or size 0 | `STAT`, `OPEN` `WRITE,CREATE,TRUNCATE`, `WRITE` 0+13 | 0 | 13 |
| `-C -`, remote 13 or 20 bytes; `-C 13`, `-C 20` | `OPEN` `WRITE`, `CLOSE`, no `WRITE` | 0 | 0 / - / unchanged |
| `-C 5`, remote missing | `OPEN` `WRITE` answered 2 | 78 | `Upload failed: No such file or directory (2/-31)` |
| `-a`; `-a -C 5`; `-a -C -` (which still sends `STAT`) | `OPEN` `WRITE,APPEND,CREATE`, `WRITE` 0+13 | 0 | 13 / - / appended |
| `-T -` with `-C 5`, remote 5 bytes | `OPEN` `WRITE`, `WRITE` 5+13 | 0 | 13 / - / 18 bytes |
| empty source | `OPEN`, `CLOSE` | 0 | 0 |
| `WRITE` answered 3 | `CLOSE` still sent | 79 | 0 / `Error in the SSH layer` |
| 3 MB, second `WRITE` answered 4 | the block's three `WRITE`s, `CLOSE` | 79 | 30000 / `Error in the SSH layer` |
| connection killed at the first `WRITE` | | 79 | 0 / `Error in the SSH layer` |
| `WRITE` answered with `DATA` | | - | curl hangs until killed |

## Decision

- **`SshProtocolHandler`** sends an `sftp` transfer with `ITransferContext.Upload` to
  `Sftp.SftpFileUpload`, whatever its path ends with; `Sftp.SftpUploadOptions.From` reads
  `ResumeFrom` (a negative one counts as 0), `ResumeUploadFromUnknownOffset` (`-C -`),
  `Append`, `FtpCreateDirectories` and `CreateFileMode` from the context.
- **`Sftp.SftpSession`** gains `OPEN` with any `Sftp.SftpOpenFlags` (returning the failed
  status rather than throwing, with `OK` still waited past), `MKDIR` with permissions
  `040755` - curl's `CURLOPT_NEW_DIRECTORY_PERMS` default, which the curl tool never
  changes - `WRITE`, and a plain status read.
- **`Sftp.SftpFileUpload`**:
  - resolves the path as a download does; under `-C -` sends `STAT` and resumes from its
    size, or from 0 when it gives none;
  - opens with `WRITE|APPEND|CREAT` under `-a`, `WRITE` alone for a positive offset, and
    `WRITE|CREAT|TRUNC` otherwise, with `--create-file-mode` as a regular file's mode;
  - when that open fails with 2, 4 or 10 and `--ftp-create-dirs` was given, sends one
    `MKDIR` for every `/` after the first, from the root down, going on past statuses 0, 3,
    4 and 11 and failing on any other with its exit code's own text, then opens again;
    a second failure is `Creating the dir/file failed: <text>`; any other failed open is
    `Upload failed: <text> (<status>/-31)`, both with the status's exit code;
  - under `-a` writes the whole source from offset 0, whatever `-C` says; otherwise moves a
    source that can seek past the offset, up to its end, and sends standard input whole
    from the offset;
  - reads the source 64 KiB at a time, curl's upload buffer, sends each block as `WRITE`s
    of at most 30000 bytes, libssh2's `MAX_SFTP_OUTGOING_SIZE`, then reads their statuses
    in order, reporting progress after each acknowledged write with the source's remaining
    length when it can seek;
  - closes the handle and the channel as a download does, and reports the acknowledged
    bytes as `TransferReport.UploadSize` (`%{size_upload}`) and `BytesTransferred`.
- **Failures during the copy** - a refused `WRITE`, an answer of another type, a broken
  connection - are exit 79 `Error in the SSH layer` with the bytes acknowledged before it,
  after `CLOSE`. A source that fails to read ends the upload as its end does, as the FTP
  upload takes it.

## Consequences

- A `WRITE` answered with another packet type fails at once instead of hanging as curl
  does, as ADR-0220 decided for a channel that ends before `VERSION`.
- libssh2 keeps writes of the next block in flight while it waits; this client waits for
  a block's statuses before reading the next. The server sees the same requests in the
  same order, and `%{size_upload}` counts the same acknowledged bytes.
- The console reaches this code once BL-576 registers the SSH handler; `-a` is parsed
  already. `--ai-help` is unchanged: no option was added or changed.
- SCP uploads are BL-577; until then an `scp` transfer with `Upload` still downloads.
