# ADR-0280 — FTP's `CCC` clears TLS through `IConnection.ClearTlsAsync`, and fails with exit 81 as the Schannel build does

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-636.

## Context

`--ftp-ssl-ccc` and `--ftp-ssl-ccc-mode` were parsed (BL-634) but did nothing. With them curl
sends `CCC` after `PBSZ` and `PROT` and drops TLS from the control connection, carrying on in
plain text. `IConnection` had no way to shut TLS down and hand back the connection underneath,
and the FTP handler only ever sees the connection the TLS provider or the connector gave it,
possibly wrapped in a `PooledConnection` (implicit `ftps://`).

`Record-CurlExchange.ps1 -Ftp` was extended (a `CCC` reply, then `close_notify` and plain text)
and run on 2026-09-30 with `-k --ftp-ssl-reqd --ftp-ssl-ccc ftp://u:p@host:18121/f.txt`:

| Build | `CCC` reply | Mode | What curl did |
| --- | --- | --- | --- |
| 8.21.0 Schannel (Windows) | `200`, `450` | passive or active | `* schannel: shutting down SSL/TLS connection with 127.0.0.1 port 18121`, `* Failed to clear the command channel (CCC)`, no `QUIT`, `curl: (81) Failed to clear the command channel (CCC)` - whichever side sent `close_notify` first, and curl did send its own |
| 8.21.0 Schannel | `500`, `533` | passive | `PWD` and the rest over TLS, exit 0 |
| 8.21.0 Schannel | `421` | passive | `* We got a 421 - timeout`, exit 28 (curl's usual `421`) |
| 8.18.0 OpenSSL (WSL) | `200`, `450` | active | `TLS alert, close notify` out, then in; `PWD` and the rest in plain text, exit 0 |
| 8.18.0 OpenSSL | `200` | passive | `TLS alert, close notify` in only; `PWD` and the rest in plain text, exit 0 |
| 8.18.0 OpenSSL | `500` | passive | the rest over TLS, exit 0 |

So curl clears TLS on any reply below 500 (libcurl's `FTP_CCC` state: `if(ftpcode < 500)`),
ignores a 5xx, and fails the shutdown with exit 81 (`CURLE_AGAIN`) on the Schannel build.

## Decision

1. `IConnection` gains a default member,
   `ValueTask<IConnection?> ClearTlsAsync(bool sendCloseNotifyFirst, CancellationToken)`, which
   returns `null`: the connection cannot clear TLS. A default member, like `MarkReusable`, so
   no plaintext connection or test fake has to change.
2. `SslStreamConnection` implements it by the build it matches, told by
   `SslStreamTlsProvider`'s existing `matchesSchannelBuild`: matching OpenSSL it sends
   `close_notify` first only when asked (active), then reads the server's and hands back the
   plaintext connection (still disposed with the TLS one); data, a bare end or an I/O failure
   in place of the server's `close_notify` is `null`. Matching Schannel it sends
   `close_notify` and returns `null`, as curl 8.21.0's Schannel build fails.
3. `PooledConnection` forwards it to the connection it holds, and once asked never returns
   that connection to the pool: its pool key names a TLS connection it no longer is.
4. `ITransferContext.FtpCommandChannelClearing` (`Off`, `Passive`, `Active`, a new enum in
   Abstractions) carries the option; `TransferContextFactory` maps `Curl.Cli`'s
   `FtpClearCommandChannel` onto it.
5. `FtpSession` sends `CCC` after `PROT` whenever the control connection is TLS (explicit or
   implicit) and the option is on. A reply of 500 or more carries on over TLS (a `warning` in
   the diagnostic log); otherwise it calls `ClearTlsAsync(mode == Active)` and switches the
   control channel to the plaintext connection, or on `null` reports the `-v` line
   `Failed to clear the command channel (CCC)` and ends with exit 81 and that message, with
   no `QUIT`. Data connections keep the protection `PROT` gave them.
6. The hand-built TLS connection (`HandBuiltTlsConnection`, used only for `--tls-max` 1.0 or
   1.1 and `--cert-status`) keeps the default, so `CCC` there is exit 81 until it can send
   and read `close_notify` on request; that is follow-up work, not a decision to leave it out.
7. The Schannel build's `* schannel: shutting down SSL/TLS connection ...` line and the
   OpenSSL build's `TLS alert, close notify` lines are not written: Curl writes neither
   build's TLS-layer `-v` chatter anywhere yet.

## Consequences

- `curl -k --ftp-ssl-reqd --ftp-ssl-ccc` behaves as the platform's curl: exit 81 with no
  `QUIT` on Windows, plain text after `CCC` on Linux and macOS, and TLS kept after a 5xx on
  both. The built `curl.exe` was run against the recorder for `200` (exit 81) and `500`
  (exit 0, `PWD` over TLS).
- A later protocol that must drop TLS mid-connection (none today) has the seam already.
