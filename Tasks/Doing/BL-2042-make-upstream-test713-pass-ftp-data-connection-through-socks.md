---
id: BL-2042
title: Make upstream test713 pass: FTP data connection through socks5:// with --connect-to still downloads the control banner
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2042 — Make upstream test713 pass: FTP data connection through socks5:// with --connect-to still downloads the control banner

## Goal

Upstream test713 (`ftp://ftp.example.com/713 --connect-to ::%HOSTIP:%FTPPORT --proxy socks5://%HOSTIP:%SOCKSPORT`) passes in the conformance ratchet, so GF-0046's `behaviour:test713` can measure `match`.

## Context

- Split out of BL-2041 (BL-1976, GF-0046), which landed the other eleven items and made FTP's
  passive data connector apply no `--connect-to` mapping (`TcpConnector.ForFtpDataConnections`,
  as curl 8.21.0's `socks.c` maps only the control connection's `conn_to_host`/`conn_to_port`).
- After that change test713 still fails: `the --output file against <reply><data> differs at
  byte 0: expected "silly content\n", got "220-        _ "` - the data connection still reaches
  the FTP control port (8993) and downloads the banner.
- The EPSV dial (`FtpSession.cs` ~line 1486) asks for `context.Url.IdnHost`
  (`ftp.example.com`) on the EPSV port (9005) through the SOCKS5 proxy, which resolves locally.
  The conformance tests' `LoopbackOnlyDnsResolver` answers nothing for `ftp.example.com`, so a
  data connect that reached SOCKS with that name should fail, not reach 8993: some other hop
  (the DNS cache holding the control connection's mapped answer, the SOCKS5 local resolve, the
  pool, or `InMemoryServerTcpDialer`) sends it to the control port. Start by running the case
  once with `-v` captured and reading the data connection's `Connecting to`, `Trying` and
  `[SOCKS]` lines. Also check what curl 8.21.0 sends in the SOCKS5 request for the data
  connection (`lib/socks.c`, `lib/ftp.c` `control_address`).

## Acceptance criteria

- [ ] test713 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] The cause is pinned by a unit test in the project where it was fixed.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
