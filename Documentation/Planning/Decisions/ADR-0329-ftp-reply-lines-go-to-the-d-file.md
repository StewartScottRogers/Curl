# ADR-0329 — FTP control reply lines go to the -D file, and a refused write is exit 23

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1131.
Builds on BL-1129's `ITransferContext.DumpHeaderOutput`.

## Context

curl 8.21.0's `Curl_pp_readresp` passes every complete control-connection line to
`Curl_client_write(CLIENTWRITE_INFO)`, which the tool's header callback writes to the `-D`
stream but `-i` never prints. Measured on 2026-10-01 with `Record-CurlExchange.ps1 -Ftp`
(BL-1131 Notes): a download, a listing and an `-I` transfer write every reply line in
arrival order, continuation lines included, except `QUIT`'s `221`; under `-I` each
synthesised `Last-Modified`, `Content-Length` and `Accept-ranges` line follows the reply it
came from. A line curl refuses for its length (exit 100) or a NUL byte (exit 8) never
reaches the callback.

## Decision

`FtpControlChannel` writes each reply line it reports to `-v` to `DumpHeaderOutput` too,
after the length and NUL checks, and stops with `StopReporting` before `QUIT`, so the file
holds what curl's does. When the stream refuses a line with an `IOException`, the transfer
ends with exit 23 and `client returned ERROR on write of <n> bytes`, the message `-I`'s
lines already use for the same callback failure; a refusal of `ABOR`'s reply, read once
the outcome is decided, changes nothing, as an unreadable `ABOR` reply already does.

## Consequences

- `-D` on `ftp://` and `ftps://` matches curl's file byte for byte; `-i` alone is unchanged.
- A failing `-D` destination fails at the first reply, the greeting, as curl's does.

## Alternatives considered

- Ignoring a refused write: the transfer would succeed where curl's header callback error
  ends it with exit 23.
- Writing from `FtpSession` per reply: it sees only each reply's last line, not the
  continuation lines curl writes.
