# ADR-0258 — SCP uploads send the `C` line and bytes as libssh2 does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-577.

## Context

`-T file scp://host/path` uploads one file. What curl 8.21.0 sends to `scp -t`, which mode it
sends, when it waits, and what each refusal reports had to be measured before
`Curl.Protocol.Ssh.UnitLibrary` could do the same.

The Windows reference build (curl 8.21.0, libssh2 1.11.1, Schannel) ran as `curl -sS -k --key
<rsa key> --pubkey <pub> -u <user>: -w '%{size_upload}' -T <source>
scp://172.26.99.197:2231/<path>` through `Record-CurlExchange.ps1 -NoServer`, against
OpenSSH 10.2 in WSL (BL-569's unpacked build, `LogLevel DEBUG3`) with a `ForceCommand`
wrapper that logged `$SSH_ORIGINAL_COMMAND`, curl's bytes and `scp`'s answers, and ran
OpenSSH's own `scp` or, chosen by file name, a scripted one. A second server on port 2232
had `MaxSessions 0`.

| Case | Exec command / bytes curl sent | Exit | stderr | `size_upload` |
| --- | --- | ---: | --- | ---: |
| new file, 10 bytes | `scp -t '/…/new.txt'`; `C0644 10 new.txt\n`, the bytes | 0 | | 10 |
| existing file | the same; file replaced, mode kept | 0 | | 10 |
| `--create-file-mode 0600` | `C0600 10 m600.txt\n`; file created `-rw-------` | 0 | | 10 |
| `--create-file-mode 1` | `C01 10 mode1.txt\n`; `scp`: `\x01scp: protocol error: bad mode` | 25 | `failed to send file` | 0 |
| `--create-file-mode 0`, `0777` | `C0644`, `C0777` | 0 | | 10 |
| empty file | `C0644 0 empty.txt\n` | 0 | | 0 |
| 100000 and 5000000 bytes | every byte | 0 | | the size |
| missing directory, read-only file, read-only directory, a directory | `C…` line only; `scp`: `\x01scp: …: No such file or directory` / `Permission denied` | 25 | `failed to send file` | 0 |
| `-T -` (unknown size) | nothing: no channel is opened | 25 | `SCP requires a known file size for upload` | 0 |
| `MaxSessions 0` | | 25 | `Channel open failure (connect failed)` | 0 |
| `/~/bl577/files/tilde.txt` | `scp -t 'bl577/files/tilde.txt'`, name `tilde.txt` | 0 | | 10 |
| `/…/it%27s%21x.txt` | `scp -t '/…/it'"'"'s'\!'x.txt'`, name `it's!x.txt` | 0 | | 10 |

Against the scripted `scp`:

| First answer | Answer to the `C` line | Exit | stderr |
| --- | --- | ---: | --- |
| `\x01scp: nope\n`, `\x02fatal\n`, `X` | | 25 | `Invalid ACK response from remote` |
| channel ends | | 25 | `Unexpected channel close` |
| connection killed | | 25 | `SCP failure` |
| `\0` | `\x02fatal\n`, `\x01`, `Xjunk\n` | 25 | `failed to send file` |
| `\0` | channel ends | 25 | `Unexpected channel close` |
| `\0` | connection killed | 25 | `Invalid ACK response from remote` |
| `\0` | `\0`, then `\x01scp: late\n` after the bytes | 0 | |
| `\0` | `\0`, then silence for 3 s before the end | 0 | |
| `\0` | `\0`, connection killed after 1000 of 5000000 bytes | 79 | `Error in the SSH layer`, `size_upload` 1703800 |

`sshd`'s log showed the channel's end: after a success curl sends `EOF`, waits for the
server's `CLOSE` and sends its own; after a refusal it sends `EOF` and `CLOSE` together and
then waits for the server's `CLOSE`.

## Decision

`ScpFileUpload` does what was measured, and what libssh2 1.11.1's `scp_send` and curl's
`SSH_SCP_UPLOAD_INIT` (which maps every libssh2 failure there to `CURLE_UPLOAD_FAILED`) do:

1. A source that cannot seek has no known size: exit 25, `SCP requires a known file size for
   upload`, before any channel. A seekable source's size is what remains of it.
2. Open a `session` channel (a refusal is exit 25 with libssh2's reason text), `exec`
   `scp -t` and the path, resolved and quoted as the download's (ADR-0225).
3. Read one byte: a zero goes on; any other byte is `Invalid ACK response from remote`; the
   channel's end `Unexpected channel close`.
4. Send `C0`, the permission bits in octal, the size, the path's last segment and a line
   feed - no `T` line, since curl passes no times. A `--create-file-mode` of 0 sends curl's
   default 0644, because curl leaves the option unset for 0.
5. Read one byte: any byte but zero is `failed to send file`; the end `Unexpected channel close`.
6. Send the source in curl's 64 KiB blocks, reporting progress, and no zero byte after it.
7. Close with `EOF`, the server's `CLOSE` and curl's `CLOSE`, ignoring anything the server
   says or does then. A failure in steps 2 to 5 closes with `EOF` and `CLOSE` at once
   (`SshSessionChannel.CloseAtOnceAsync`) before it is thrown.

A connection that breaks while curl waits for the first acknowledgement is `SCP failure`,
for the second `Invalid ACK response from remote`, and during the bytes exit 79 `Error in
the SSH layer` with the bytes sent so far as `%{size_upload}`. Two breaks cannot be made to
happen against OpenSSH, before the channel is confirmed and before the `exec` request is
answered; both are taken as `SCP failure`, the nearest measured point. The `exec` refusal
keeps ADR-0225's libssh2 text, `Unable to complete request for channel-process-startup`, at
exit 25.

## Consequences

- `scp://` uploads work from `-T`, with `--create-file-mode`, and fail as curl does.
- `size_upload` after a break during the bytes counts the bytes handed to the channel; curl's
  count depends on how much libssh2 had buffered when the connection died, so the number
  differs from run to run on either side.
- What `--create-file-mode 0` does for an SFTP upload is not measured or decided here; since
  curl leaves the option unset for 0, it probably sends 0644 there too, while the SFTP upload
  still passes the 0 it is given. BL-984 measures it.
