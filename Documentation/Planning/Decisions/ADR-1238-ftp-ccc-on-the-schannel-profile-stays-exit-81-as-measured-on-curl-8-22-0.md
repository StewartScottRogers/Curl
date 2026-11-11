# ADR-1238 — FTP's `CCC` on the Schannel profile stays exit 81, as measured on curl 8.22.0

- **Status:** Accepted
- **Date:** 2026-11-11
- **Supersedes:** [ADR-0280](ADR-0280-ftp-ccc-clears-tls-through-iconnection-and-fails-with-exit-81-as-the-schannel-build-does.md) (its 8.21.0 measurement; its design stands)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4910.

## Context

The outside fitness review of 2026-11-11 (finding STD-007) reported that curl 8.22.0's Schannel
build completes `--ftp-ssl-ccc` in passive and active mode (`* schannel: server close
notification received (close_notify)`, `PWD` in plain text, exit 0) against a Python FTPS
server, where Curl's Schannel profile ends with exit 81, as ADR-0280 measured on 8.21.0.

`Record-CurlExchange.ps1 -Ftp` gained `-FtpCccAwaitsCloseNotify` (the server reads curl's
`close_notify` before sending its own, instead of sending its own at once) and was run on
2026-11-11 with `-v -sS -k --ssl-reqd --ftp-ssl-ccc [--ftp-ssl-ccc-mode passive|active]
ftp://127.0.0.1:18403/f.txt`, `FtpData` `0123456789ABCDEF\n`:

| Build | Server order | Mode | TLS | What curl did |
| --- | --- | --- | --- | --- |
| 8.22.0 Schannel, `C:\Windows\System32\curl.exe` | either | passive, active | default | `< 200 CCC command successful`, `* schannel: shutting down SSL/TLS connection with 127.0.0.1 port 18403`, `* Failed to clear the command channel (CCC)`, exit 81, no `PWD` |
| 8.22.0 Schannel, `C:\Windows\System32\curl.exe` | either | passive | `--tls-max 1.2` | the same, exit 81 |
| 8.22.0 Schannel (Git for Windows, debug libcurl) | either | passive, active | default, `--tls-max 1.2`, `--tls-max 1.3` | the same plus `* schannel: clear security context handle`, exit 81 |

curl sent its own `close_notify` in every run (the server read it when it waited for it), and
never printed `server close notification received`. The recorder's server is `SslStream`, so
Schannel on both sides; the review's server was Python's OpenSSL, which this repository cannot
use (no Python), so its exit 0 was not reproduced here.

## Decision

Keep ADR-0280's behaviour: on the Schannel profile `IConnection.ClearTlsAsync` sends
`close_notify` and gives no connection, and `CCC` ends with exit 81 and no `QUIT`. Every
platform build of curl 8.22.0 measured here does that; changing it on one unreproduced report
would make Curl differ from the curl it replaces on this machine.

## Consequences

- BL-4910's acceptance criteria (exit 0) contradict this measurement; the task is Blocked for
  Stewart to close STD-007 as not reproducible, or to name a server stack it reproduces with.
- `-FtpCccAwaitsCloseNotify` stays in the recorder for the next measurement.
