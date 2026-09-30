---
id: BL-932
title: Answer tftp:// requests from Record-CurlExchange.ps1 with a scripted TFTP responder
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-932 — Answer tftp:// requests from Record-CurlExchange.ps1 with a scripted TFTP responder

## Goal

`Record-CurlExchange.ps1 -Tftp` serves one TFTP transfer on UDP so real curl's `tftp://` output (`-v`, `--trace`, `--trace-ascii`, exit code) can be measured against a loopback server, as `-Ftp`, `-Smtp`, `-Imap` and `-Pop3` already allow for their protocols.

## Context

- Needed by BL-933 (curl's `-v`/`--trace` lines for TFTP). Today the recorder can only swallow UDP (`-UdpSink`, which never answers), so a TFTP measurement has no server to talk to. Root `CLAUDE.md`: measure real curl with `Record-CurlExchange.ps1` and extend it when it falls short; PowerShell only, no Python.
- Behaviour to add, following the `-Ftp` parameter's style (reply table with overrides):
  - `-Tftp`: bind UDP on `ListenAddress`/`Port`; answer a read request (RRQ) by sending `-TftpData` (default `hello\n`) in DATA blocks of the agreed block size from a fresh ephemeral port, as RFC 1350 requires, waiting for each ACK; answer a write request (WRQ) with ACK 0 (or OACK) and ACK each DATA block, saving the bytes to `upload.bin`.
  - Options (RFC 2347/2348/2349): when the request carries `blksize`, `tsize` or `timeout`, answer with an OACK echoing them (tsize filled in for RRQ) unless `-TftpNoOack` is given.
  - `-TftpReply` overrides, each `STEP=reply`: `RRQ=ERROR 1 File not found` sends an ERROR packet with that code and text instead of data; `ACK<n>=DROP` ignores the n-th ACK once so curl retransmits; `DATA<n>=DROP` does not send block n the first time.
  - Output: `datagrams.txt` with one line per datagram in both directions (`> ` from curl, `< ` from the server, lowercase hex, then a decoded summary such as `RRQ file octet blksize=512 tsize=0`), plus the usual `stdout.bin`, `stderr.txt` and `exitcode.txt`.
- Combining `-Tftp` with `-Ftp`, `-Smtp`, `-Imap`, `-Pop3`, `-Script` or `-Tls` is refused, as the other server modes refuse each other.
- Update the script's comment-based help (`.PARAMETER Tftp`, `TftpData`, `TftpReply`, `TftpNoOack`) in the same style.

## Acceptance criteria

- [x] `powershell -NoProfile -File Record-CurlExchange.ps1 -Tftp -CurlArgs 'tftp://127.0.0.1:<port>/file'` (with the port it prints or the one given) records curl's exit code 0, standard output `hello` plus a line feed, and a `datagrams.txt` whose first line is curl's RRQ.
- [x] `-TftpReply 'RRQ=ERROR 1 File not found'` records curl's exit 68 (`CURLE_TFTP_NOTFOUND`).
- [x] A `-T` upload records the uploaded bytes in `upload.bin`.
- [x] `Get-Help .\Record-CurlExchange.ps1 -Parameter Tftp` describes the mode.
- [x] The script still runs every existing mode unchanged (spot-check `-Ftp` and the default HTTP mode once each), and the transcript of all three runs is pasted into Notes.

## Notes

- The responder is a C# class compiled with Add-Type (`RecorderTftpResponder`), as the DNS
  responder is: a thread with no runspace, polling the listening port and the ephemeral
  transfer port with `Socket.Select`, so a retransmitted request is answered too.
- Choices taken as defaults: `DROP` applies to the n-th packet of that kind whichever side
  sends it (a received one is ignored once, a sent one is not sent once), so `ACK<n>` and
  `DATA<n>` work for both RRQ and WRQ. The server never retransmits on a timer; it resends
  its last packet when curl repeats one, which is how curl drives recovery. Added
  `-TftpIdleMilliseconds` (default 15000), since curl 8.21.0's retry interval is several
  seconds (a dropped DATA 1 took 7.3 s). No request.bin in -Tftp mode: datagrams.txt holds
  curl's datagrams in full. `-Tftp` also refuses -NoServer, -UnixSocket and -UdpSink
  (all bind the same port or none).
- Measured with Git for Windows' curl 8.21.0: an RRQ carries `tsize=0 blksize=512 timeout=6`
  by default, so the default answer is an OACK.

Transcript (output under %TEMP%\bl932):

```
> Record-CurlExchange.ps1 -Port 18069 -Tftp -CurlArgs 'tftp://127.0.0.1:18069/file'
curl exited 0; stdout.bin = 68 65 6C 6C 6F 0A
> 000166696c65006f63746574007473697a65003000626c6b73697a65003531320074696d656f7574003600 RRQ file octet tsize=0 blksize=512 timeout=6
< 00067473697a65003600626c6b73697a65003531320074696d656f7574003600 OACK tsize=6 blksize=512 timeout=6
> 00040000 ACK 0
< 0003000168656c6c6f0a DATA 1 6 bytes
> 00040001 ACK 1

> -Tftp -TftpReply 'RRQ=ERROR 1 File not found' -CurlArgs '-sS','tftp://127.0.0.1:18069/file'
exit: 68   stderr: curl: (68) TFTP: File Not Found
< 0005000146696c65206e6f7420666f756e6400 ERROR 1 File not found

> -Tftp -CurlArgs '-sS','-T','up.txt','tftp://127.0.0.1:18069/up.txt'
exit: 0   upload.bin: uploaded bytes
> ...WRQ up.txt octet tsize=14 blksize=512 timeout=6
< ...OACK tsize=14 blksize=512 timeout=6
> 0003000175706c6f61646564206279746573 DATA 1 14 bytes
< 00040001 ACK 1

> -Tftp -TftpNoOack -TftpReply 'DATA1=DROP'   -> exit 0 after 7281 ms; RRQ, RRQ again, DATA 1, ACK 1
> -Tftp -TftpReply 'ACK1=DROP' -TftpData 'abcdefghij' --tftp-blksize 8 -> exit 0, ACK 1 twice, then DATA 2

> -Port 18021 -Ftp -FtpData 'ftp file\n' -CurlArgs '-sS','ftp://127.0.0.1:18021/f.txt'
curl exited 0 after 262 ms; stdout: ftp file

> -Port 18081 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello' -CurlArgs '-sS','http://127.0.0.1:18081/a'
curl exited 0 after 159 ms; stdout: hello
request.bin: GET /a HTTP/1.1 / Host: 127.0.0.1:18081 / User-Agent: curl/8.21.0 / Accept: */*

> Get-Help .\Record-CurlExchange.ps1 -Parameter Tftp  -> "-Tftp [<SwitchParameter>] Serve one TFTP transfer on UDP ..."
> -Tftp -Ftp -> refused: "-Tftp serves one TFTP transfer on UDP -Port, so it cannot be combined with -Ftp, ..."
```

dotnet build clean; fast tests green (no failures in any test project).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Record-CurlExchange.ps1 -Tftp serves one TFTP read or write on UDP with OACK, ERROR and DROP overrides, logging datagrams.txt
