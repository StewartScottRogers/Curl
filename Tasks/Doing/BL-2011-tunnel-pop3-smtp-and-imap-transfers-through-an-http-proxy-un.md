---
id: BL-2011
title: Tunnel pop3, smtp and imap transfers through an HTTP proxy under -p (upstream test1319, test1320, test1321, GF-0045)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2011 — Tunnel pop3, smtp and imap transfers through an HTTP proxy under -p (upstream test1319, test1320, test1321, GF-0045)

## Goal

A `pop3://`, `smtp://` or `imap://` transfer with `-p -x <http proxy>` opens a CONNECT tunnel to the mail server (`CONNECT pop.1319:<port> HTTP/1.1`, `Host`, `User-Agent`, `Proxy-Connection: Keep-Alive`) and runs its protocol inside it, as curl 8.21.0 does, so upstream test1319, test1320 and test1321 pass.

## Context

- Split from BL-1975 (GF-0045), whose lane could not touch the mail protocol libraries (held by BL-1989).
- Measured in `Curl.Conformance.UnitTests` (in-process over `TcpConnector`): test1319 `<verify><protocol>` expected `CAPA`, got the end; test1320 `EHLO 1320`, test1321 `A001 CAPABILITY`, the same. Nothing reaches the proxy or the server.
- Per Curl.Console's CLAUDE.md, "Other schemes do not read the proxy yet (BL-330)": the mail handlers build a `ConnectTarget` with no `Proxy`. `TcpConnector` already tunnels any target whose `Proxy` is `Http`/`Http10` (`HttpProxyTunnel`), so the work is putting the transfer's chosen proxy (`TransferProxySelection`, `-p` as tunnel) on the mail handlers' connect target and checking a non-tunnelling HTTP proxy's behaviour for these schemes against real curl.

## Acceptance criteria

- [ ] `UpstreamCase_RunThroughCurl_HoldsTheRatchet` passes for 1319, 1320 and 1321, and they are in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] Each mail handler's unit tests pin the proxy on its connect target.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
