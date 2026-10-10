---
id: BL-2035
title: Run upstream test713-715 (FTP through SOCKS5, an HTTP tunnel or both, with --connect-to) through the conformance harness
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2035 — Run upstream test713-715 (FTP through SOCKS5, an HTTP tunnel or both, with --connect-to) through the conformance harness

## Goal

The conformance harness runs upstream test713, test714 and test715 faithfully, so each either passes or fails on a real Curl difference that its verdict names.

## Context

- Split out of BL-1976 (GF-0046), which waits on it.
- test714 and test715 fail with `<verify><protocol>` "got the end": the harness's http-proxy
  stand-in relays a CONNECT only to the mail ports (`SwsHttpServerConnection`,
  `UpstreamCaseRunner` line ~299, BL-2011), never to `%FTPPORT` or the FTP stand-in's passive
  port, so no FTP command reaches the FTP server. Relay a CONNECT to the FTP port and to an
  open passive data port the same way (upstream's http-proxy connects to the port named).
- test713 (`--proxy socks5://` with `--connect-to ::%HOSTIP:%FTPPORT`) writes the FTP banner
  (`220-   _ ...`) as the downloaded file: its data connection reaches the FTP control port.
  BL-1976 made FTP's data connector apply no `--connect-to` mapping
  (`TcpConnector.ForFtpDataConnections`), as curl 8.21.0 maps only the control connection, and
  the case still writes the banner, so find which hop (Curl's SOCKS5 request for the data
  connection, the socksd stand-in, `InMemoryServerTcpDialer`, or the FTP stand-in's passive
  port) sends it to the control port. Run the case once with `-v` captured to see the
  data connection's `Connecting to` and SOCKS lines.
- test712 (SOCKS5 without `--connect-to`) already passes.

## Acceptance criteria

- [ ] test714 and test715 reach the FTP stand-in through the http-proxy's CONNECT, control and data connections both.
- [ ] Why test713's data connection reaches the control port is found and recorded under Notes; a harness fault is fixed here, a Curl fault filed as its own task.
- [ ] Each of test713-715 that now passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-10: Created.
