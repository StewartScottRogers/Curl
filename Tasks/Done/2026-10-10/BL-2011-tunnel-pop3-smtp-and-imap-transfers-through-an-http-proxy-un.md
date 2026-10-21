---
id: BL-2011
title: Tunnel pop3, smtp and imap transfers through an HTTP proxy under -p (upstream test1319, test1320, test1321, GF-0045)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2011 — Tunnel pop3, smtp and imap transfers through an HTTP proxy under -p (upstream test1319, test1320, test1321, GF-0045)

## Goal

A `pop3://`, `smtp://` or `imap://` transfer with `-p -x <http proxy>` opens a CONNECT tunnel to the mail server (`CONNECT pop.1319:<port> HTTP/1.1`, `Host`, `User-Agent`, `Proxy-Connection: Keep-Alive`) and runs its protocol inside it, as curl 8.21.0 does, so upstream test1319, test1320 and test1321 pass.

## Context

- Split from BL-1975 (GF-0045), whose lane could not touch the mail protocol libraries (held by BL-1989).
- Measured in `Curl.Conformance.UnitTests` (in-process over `TcpConnector`): test1319 `<verify><protocol>` expected `CAPA`, got the end; test1320 `EHLO 1320`, test1321 `A001 CAPABILITY`, the same. Nothing reaches the proxy or the server.
- Per Curl.Console's CLAUDE.md, "Other schemes do not read the proxy yet (BL-330)": the mail handlers build a `ConnectTarget` with no `Proxy`. `TcpConnector` already tunnels any target whose `Proxy` is `Http`/`Http10` (`HttpProxyTunnel`), so the work is putting the transfer's chosen proxy (`TransferProxySelection`, `-p` as tunnel) on the mail handlers' connect target and checking a non-tunnelling HTTP proxy's behaviour for these schemes against real curl.

## Acceptance criteria

- [x] `UpstreamCase_RunThroughCurl_HoldsTheRatchet` passes for 1319, 1320 and 1321, and they are in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] Each mail handler's unit tests pin the proxy on its connect target.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Curl needed no change: the Pop3, Smtp and Imap handlers already put `context.Proxy` on their `ConnectTarget`, `TcpConnector` already tunnels through an HTTP proxy, and each handler's `*ProtocolHandlerSessionTests` already pins the proxy on the connect target (`ExecuteAsync_ProxyAndEvents_ArePassedToTheConnector`). The three cases failed because the conformance harness's http-proxy stand-in served every tunnel from the sws emulation, so the mail commands never reached a mail stand-in.
- Added `Curl.Conformance.UnitLibrary` to `touches` (no task in Doing on origin/work/dark-factory names it): `SwsHttpServerConnector.TunnelServerForPort` relays a CONNECT to `%POP3PORT`, `%IMAPPORT` or `%SMTPPORT` to the mail stand-ins once the `<connect>` reply keeps the connection open, as upstream's http-proxy connects to the named port; the runner sets it. Other ports keep being served by sws, so the existing CONNECT cases are unchanged.
- Not measured against real curl: the cases' own `<verify>` sections are curl 8.21.0's answer, and they now pass. Measure-CodeQuality not run (cost); every new branch has a `SwsHttpServerConnectorTests` case (relay, refused relay, closing reply, no port, no version).

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Harness relays http-proxy CONNECT tunnels to the mail stand-ins; test1319-1321 pass and are listed
