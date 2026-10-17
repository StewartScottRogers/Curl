---
id: BL-2576
title: Fix AF-0239: Curl.Console CLAUDE.md says SOCKS and HTTPS proxies end http/https transfers with exit 4 and other schemes ignore the proxy; the connector tunnels through them
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Console]
requirement: none
created: 2026-10-17
completed:
---
# BL-2576 — Fix AF-0239: Curl.Console CLAUDE.md says SOCKS and HTTPS proxies end http/https transfers with exit 4 and other schemes ignore the proxy; the connector tunnels through them

## Goal

The defect the audit office reported as AF-0239 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0239 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0239-curl-console-claude-md-says-socks-and-https-proxie.md`.

Location: `Curl.Console/CLAUDE.md:294`

Location: `Curl.Console/CLAUDE.md:294`

Lines 293-295: 'a SOCKS proxy, or an HTTPS proxy for `https` or under `-p` or `-L`, ends an `http`/`https` transfer with exit 4 until the connector opens those tunnels (ADR-0053, BL-328). Other schemes do not read the proxy yet (BL-330)'. The code opens those tunnels. Curl.Networking.UnitLibrary/TcpConnector.cs:1929 returns 'await SocksProxyTunnel.OpenAsync(' and no proxy path in Curl.Console, Curl.Engine or Curl.Networking returns NotBuiltIn (exit 4). Curl.Console.UnitTests/CurlCompositionProxyTests.cs:250-260, RunAsync_Socks5Option_SendsCurlsGreetingAndConnectRequestThenTheRequestThroughTheTunnel, expects exit 0 and 'hello' for '--socks5 ... http://example.com/a'. Lines 291-294 of the same file show that dict, gopher and https URLs reach the connector carrying a SOCKS or HTTPS proxy. Curl.Protocol.Gopher.UnitLibrary/CLAUDE.md also says the proxy goes into the ConnectTarget and the connector tunnels through it. An agent reading this would expect exit 4 or think the tunnels are still to be built.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.Console/CLAUDE.md -Pattern 'exit 4 until the connector opens those tunnels').Count; (Select-String -Path Curl.Networking.UnitLibrary/TcpConnector.cs -Pattern 'SocksProxyTunnel\.OpenAsync\(').Count
```

- Expected: 0 then 1: the document no longer claims exit 4 for tunnels the connector opens
- Actual: 1 then 1: the document claims exit 4 while TcpConnector opens SOCKS tunnels

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
