---
id: BL-330
title: Send non-HTTP schemes through a selected proxy as curl does
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-330 — Send non-HTTP schemes through a selected proxy as curl does

## Goal

A non-HTTP transfer (for example `ftp://`, `dict://`, `gopher://`) with `-x` or a proxy variable goes through the proxy as curl 8.21.0 does, or is refused as it refuses it.

## Context

- Filed by BL-238 (2026-09-27). The proxy chosen by `TransferProxySelection` reaches only `HttpRequestOptions.ForwardProxy`, which only `HttpProtocolHandler` reads; every other handler connects directly whatever `-x`, `all_proxy` or `--socks5` say. curl forwards `ftp://` through an HTTP proxy as a GET, and tunnels other schemes with `-p` or a SOCKS proxy.
- Needs a way for non-HTTP handlers to see the proxy, probably `ConnectTarget.Proxy` set from the transfer context; that may touch `Curl.Protocol.Abstractions.UnitLibrary` and each protocol library - split per protocol when planning.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH (ADR-0009) - against a loopback server (`Record-CurlExchange.ps1`), record the exact command and the bytes in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Measured: `curl -x <proxy> dict://example.com/d:x`, `curl -x <proxy> gopher://example.com/` and `curl -p -x <proxy> dict://example.com/d:x` - the bytes the proxy receives are recorded in `Notes` and each case's route is decided in an ADR.
- [x] Follow-up tasks per protocol are filed for the decided routes.
- [x] The ADR is indexed in `Documentation/Planning/Decisions/README.md`.

## Notes

### Measurements (2026-09-27)

curl 8.21.0 (x86_64-w64-mingw32, Schannel), `C:\Program Files\Git\mingw64\bin\curl.exe`, against a
PowerShell `TcpListener` on 127.0.0.1 standing in for the proxy. `Record-CurlExchange.ps1` recorded
the first request; a small listener (`tunnel.ps1`, not kept) answered
`HTTP/1.1 200 Connection established\r\n\r\n` and recorded what curl sent next. Bytes shown with
`\r\n` escaped.

| Command | First bytes at the proxy | After `200` | Exit |
| --- | --- | --- | --- |
| `curl -sS -x http://127.0.0.1:18331 dict://example.com/d:x` | `CONNECT example.com:2628 HTTP/1.1\r\nHost: example.com:2628\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n` | `CLIENT libcurl 8.21.0\r\nDEFINE ! x\r\nQUIT\r\n` | 0 |
| `curl -sS -x http://127.0.0.1:18331 gopher://example.com/` | `CONNECT example.com:70 HTTP/1.1\r\nHost: example.com:70\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n` | `\r\n` | 0 |
| `curl -sS -p -x http://127.0.0.1:18331 dict://example.com/d:x` | identical to the dict case without `-p` | identical | 0 |
| `curl -sS -x http://127.0.0.1:18332 ftp://example.com/f.txt` (reply `HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello`) | `GET ftp://example.com/f.txt HTTP/1.1\r\nHost: example.com:21\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n` | - | 0, stdout `hello` |
| `curl -sS -p -x … ftp://example.com/f.txt` | `CONNECT example.com:21 HTTP/1.1…` (same header set) | - | - |
| `telnet`, `imap`, `pop3`, `smtp`, `mqtt`, `rtsp`, `ws`, `ldap` (no `-p`) | `CONNECT example.com:<23/143/110/25/1883/554/80/389> HTTP/1.1…` | mqtt: the MQTT CONNECT packet; rtsp: `OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n…`; ws: the upgrade GET | - |
| `curl -sS -x http://127.0.0.1:18331 tftp://example.com/f` | `GET http://127.0.0.1:18331/.well-known/masque/udp/example.com/69/ HTTP/1.1\r\nHost: 127.0.0.1:18331\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\nConnection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n` | - | 7, `curl: (7) bind() failed; Invalid arguments` |
| `curl -sS -x http://127.0.0.1:18331 file:///c:/windows/win.ini -o nul` | no connection | - | 0 |
| dict, CONNECT answered `HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n` | as above | - | 7, `curl: (7) CONNECT tunnel failed, response 403` |
| `curl -sS --socks5 127.0.0.1:18332 dict://example.com/d:x` | a SOCKS5 greeting, then (after `05 00`) a SOCKS5 CONNECT naming `example.com` port 2628 | - | 97 when the listener closed |

With `Record-CurlExchange.ps1`'s canned `200 OK` and close, dict/gopher end with exit 56
(`Recv failure: Connection was reset/aborted`), because the tunnel is closed at once.

### Decision

ADR-0056: every TCP scheme tunnels through the selected proxy (`-p` or not); `ftp` without `-p`
through an HTTP proxy is forwarded as an HTTP GET; `tftp` fails as the reference build does;
`file` ignores the proxy; the ADR-0053 guard covers every tunnelled scheme. The proxy reaches
handlers through a new `ITransferContext.Proxy`.

### Follow-up tasks

BL-337 (context member), BL-338 (Curl.Console sets it, guard widened), BL-339 dict, BL-340
gopher, BL-341 telnet, BL-342 mqtt, BL-343 HTTP handler forwards `ftp://`, BL-344 Curl.Console
routes `ftp` to it, BL-345 tftp. `imap`, `pop3`, `smtp`, `rtsp`, `ws`, `ldap`, `scp`/`sftp`,
`smb` and FTP's `-p` tunnel have no connecting handler yet; ADR-0056 rule 2 applies when each is
built, so no separate task was filed for them.

Choice: ADR number 0056 was the next free one in this checkout; the ADR number may need
renumbering if another lane took it first.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ADR-0056 decides every non-HTTP proxy route from measured curl 8.21.0 bytes; follow-ups BL-337 to BL-345 filed
