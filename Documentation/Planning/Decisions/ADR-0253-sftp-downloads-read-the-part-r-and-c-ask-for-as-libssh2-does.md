# ADR-0253 — SFTP downloads read the part `-r` and `-C` ask for as libssh2 does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-573.

## Context

`-r`/`--range` and `-C`/`--continue-at` on an `sftp://` download ask for part of the file.
Which bytes curl reads, where its reads start, and what a range or offset past the end
reports had to be measured before `Curl.Protocol.Ssh.UnitLibrary` could honour them.

The Windows reference build (curl 8.21.0, libssh2 1.11.1, Schannel) ran as `curl -sS -k --key
<rsa key> --pubkey <pub> -u <user>: <option> sftp://localhost:2235/~/bl573/files/a.txt`
through `Record-CurlExchange.ps1 -NoServer`, against OpenSSH 10.2 in WSL with
`sftp-server -l DEBUG3` logging every request (ADR-0220's setup). `a.txt` held the 10 bytes
`0123456789`. `sftp_download_stat` in `lib/vssh/libssh2.c` and `Curl_ssh_range` in
`lib/vssh/vssh.c` at the `curl-8_21_0` tag explain every result.

| Option | Requests after `OPEN`, `STAT` | Exit | stdout / stderr |
| --- | --- | ---: | --- |
| `-r 2-5` | `READ` off 2 len 16, `CLOSE` | 0 | `2345` |
| `-r 7-`, `-r -3` | `READ` off 7 len 12, `CLOSE` | 0 | `789` |
| `-r 5-100` | `READ` off 5 len 20, `CLOSE` | 0 | `56789` |
| `-r -20` | `READ` off 0 len 40, `CLOSE` | 0 | `0123456789` |
| `-r 3-3` | `READ` off 3 len 4, `CLOSE` | 0 | `3` |
| `-r 10-` | `CLOSE` | 33 | `Bad range: start offset larger than end offset` |
| `-r 11-`, `-r 20-30` | `CLOSE` | 33 | `Offset (11) was beyond file size (10)`, `Offset (20) ...` |
| `-C 3` | `READ` off 3 len 28, `CLOSE` | 0 | `3456789` |
| `-C 0` | as no option | 0 | `0123456789` |
| `-C 10` | `CLOSE` | 33 | `Bad range: start offset larger than end offset` |
| `-C 11` | `CLOSE` | 33 | `Offset (11) was beyond file size (10)` |
| `-C - -o f`, `f` holds `012` | `READ` off 3 len 28 | 0 | `f` holds `0123456789` |
| `-C - -o f`, `f` holds 10 bytes / 12 bytes | `CLOSE` | 33 | as `-C 10` / `-C 12` |
| `-C 3` on an empty file | `CLOSE` | 36 | `Offset (3) was beyond file size (0)` |
| `-r 2-5` on an empty file | the whole-file reads from 0 | 0 | empty |
| `-C 3 -r 5-6` | none: the tool refuses the pair | 2 | `--continue-at is mutually exclusive with --range` |
| `-r 1000-250999` of 300000 bytes | 14 `READ`s of 30000 from off 1000, 17 in all | 0 | 250000 bytes |

## Decision

`SftpDownloadPart.Choose` ports the two C functions as measured:

- A nonzero `-C` offset becomes the range `offset-` in place of any `-r`, as curl's
  `setup_range` does; so `-C` fails with exit 33 and the range messages, not exit 36.
- A range applies only when `STAT` gave a size above 0. With none the whole file is read,
  and a nonzero `-C` fails with exit 36, `Offset (N) was beyond file size (0)`.
- A suffix longer than the file is the whole file; a last position past the end stops at
  the end; a first position past the end is `Offset (N) was beyond file size (S)` and one
  at the end is `Bad range: start offset larger than end offset`, both exit 33.
- A refused part reads nothing, and the handle is still closed, as measured; `-` quote
  commands do not run, as for any failed transfer.
- `SftpReadAhead` reads the part as it reads a whole file (ADR-0220), from the part's first
  byte with its length for the size. What a read returns past the part is dropped, and the
  progress size is the part's length.

`-C -` needs nothing here: the console resolves it to the output file's size before the
handler runs.

## Consequences

- The `READ` offsets and lengths of the small cases match byte for byte. Over a large range
  ADR-0220's read-ahead sends 19 reads where libssh2 sent 17; the extra reads fall past the
  range, their answers are dropped either way, and nothing curl prints differs.
- `Curl.Protocol.Ssh.UnitTests` pins every row above but the tool's own refusal of `-C`
  with `-r`, which never reaches a handler.
