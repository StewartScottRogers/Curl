# ADR-0294 — `scp` and `sftp` uploads report libssh2's writes, and a failure during the bytes closes the connection

- **Status:** Accepted; replaces the unmeasured short-file part of ADR-0262's decision 6
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-988.

## Context

ADR-0262 left uploads without `} [N bytes data]` or `=> Send data`, and could not measure a
right password, `keyboard-interactive` succeeding, or a short `scp` file, whose `PartialFile`
outcome it treated as leaving the connection intact.

Measured 2026-10-01 with the Windows reference build (curl 8.21.0, libssh2 1.11.1, WinCNG)
through `Record-CurlExchange.ps1 -NoServer`, against BL-569's unpacked OpenSSH 10.2 in WSL:
unprivileged on port 2278 for key uploads, and run as root (a throwaway user with a
password) on further ports for a password, for `keyboard-interactive` through PAM, and with
a `ForceCommand` script answering `scp -pf` for short files.

| Case | Lines after the session lines | Exit |
| --- | --- | ---: |
| `sftp` or `scp` `-T` of 13 bytes | `} [13 bytes data]`, `upload completely sent off: 13 bytes`, `Connection #0 ... left intact` | 0 |
| the same, `--trace-ascii -` | `=> Send data, 13 bytes (0xd)` and its dump, then the same lines | 0 |
| 200000 bytes over `sftp`, `--trace-ascii` | blocks of 30000, 30000, 5536 for each 64 KiB read, then 3392 | 0 |
| 200000 bytes over `scp`, `--trace-ascii` | blocks of 32700, 32700, 136 for each 64 KiB read, then 3392 | 0 |
| an empty source, either scheme | `Request completely sent off`, `left intact` | 0 |
| `sftp` upload to a missing directory | `Upload failed: No such file or directory (2/-31)`, `left intact` | 78 |
| `scp` upload to a missing directory | `failed to send file`, `left intact` | 25 |
| a right password | `SSH: initialized password authentication`, `SSH: authentication complete` | 0 |
| `keyboard-interactive` (methods `publickey,keyboard-interactive`) | the two agent lines, `SSH: initialized keyboard interactive authentication`, `SSH: authentication complete` | 0 |
| `scp` file announcing 10 bytes, sending 5 | `{ [5 bytes data]`, `end of response with 5 bytes missing`, `closing connection #0`; `--trace-ascii` shows `<= Recv data, 0 bytes (0x0)` after the 5 bytes | 18 |
| `scp` file of size -1 read to the channel's end | `<= Recv data, 0 bytes (0x0)` after the bytes, `left intact` | 0 |
| `scp` connection killed after 5 of 10 bytes | `{ [5 bytes data]`, `closing connection #0` (no line for `Error in the SSH layer`) | 79 |

The password and `keyboard-interactive` lines are those `SshInfoLines` already held.

## Decision

1. **Sent data is reported per libssh2 write.** `SftpFileUpload` reports each `WRITE`'s
   chunk (at most 30000 bytes) after sending it; `ScpFileUpload` sends each 64 KiB block as
   channel writes of at most 32700 bytes (`ChannelWriteSize`, libssh2's limit per
   `libssh2_channel_write`) and reports each. The console's writers turn the reports into
   `} [N bytes data]` and `=> Send data`.
2. **A copy that reaches the source's end writes `upload completely sent off: N bytes`**, or
   `Request completely sent off` for none (`SshInfoLines.UploadSent`), inside the upload
   class, so an `sftp` upload's line comes before its `-Q` post-commands, as curl's transfer
   ends before its `sftp_done`. A failed copy writes neither.
3. **A failure while the bytes move closes the connection.** curl's `PERFORMING` state
   closes the connection on any transfer error ("Transfer returned error"), while failures in
   its `DO` state machine leave it. `SshProtocolHandler.FailedWhileTransferring` takes a
   short file (exit 18) and `Error in the SSH layer` returned by a copy as such failures, and
   writes `closing connection #N` after them; an `sftp` directory listing is read in curl's
   state machine, so its failures still leave the connection intact. The `sftp` short file
   and upload failures during the bytes follow the same rule, unmeasured.
4. **The `scp` channel's end is written on as an empty block.** `ScpFileDownload` writes an
   empty block to the output when the channel ends, which `ReceivedDataReportingStream`
   reports as 0 bytes of received data, as measured for a short file and a size of -1. `-v`
   shows it not at all, as it shows only the first of consecutive data lines.

## Consequences

- `Curl.Output.UnitLibrary` needed no change.
- `InMemorySshServer` gained `OffersKeyboardInteractive` and `ScpFileShortBy` so the console
  tests pin the `keyboard-interactive` and short-file output end to end.
