---
id: AF-0239
title: Curl.Console CLAUDE.md says SOCKS and HTTPS proxies end http/https transfers with exit 4 and other schemes ignore the proxy; the connector tunnels through them
auditor: truthfulness
severity: High
status: proposed
reason:
key: truthfulness:Curl.Console/CLAUDE.md:proxy-tunnel-exit-4:false-statement
reproduction: none
task: none
tasks:
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0239 - Curl.Console CLAUDE.md says SOCKS and HTTPS proxies end http/https transfers with exit 4 and other schemes ignore the proxy; the connector tunnels through them

## Summary

High finding from the truthfulness auditor at `Curl.Console/CLAUDE.md:294`: Curl.Console CLAUDE.md says SOCKS and HTTPS proxies end http/https transfers with exit 4 and other schemes ignore the proxy; the connector tunnels through them.

## Evidence

Location: `Curl.Console/CLAUDE.md:294`

Lines 293-295: 'a SOCKS proxy, or an HTTPS proxy for `https` or under `-p` or `-L`, ends an `http`/`https` transfer with exit 4 until the connector opens those tunnels (ADR-0053, BL-328). Other schemes do not read the proxy yet (BL-330)'. The code opens those tunnels. Curl.Networking.UnitLibrary/TcpConnector.cs:1929 returns 'await SocksProxyTunnel.OpenAsync(' and no proxy path in Curl.Console, Curl.Engine or Curl.Networking returns NotBuiltIn (exit 4). Curl.Console.UnitTests/CurlCompositionProxyTests.cs:250-260, RunAsync_Socks5Option_SendsCurlsGreetingAndConnectRequestThenTheRequestThroughTheTunnel, expects exit 0 and 'hello' for '--socks5 ... http://example.com/a'. Lines 291-294 of the same file show that dict, gopher and https URLs reach the connector carrying a SOCKS or HTTPS proxy. Curl.Protocol.Gopher.UnitLibrary/CLAUDE.md also says the proxy goes into the ConnectTarget and the connector tunnels through it. An agent reading this would expect exit 4 or think the tunnels are still to be built.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.Console/CLAUDE.md -Pattern 'exit 4 until the connector opens those tunnels').Count; (Select-String -Path Curl.Networking.UnitLibrary/TcpConnector.cs -Pattern 'SocksProxyTunnel\.OpenAsync\(').Count
```

- Expected: 0 then 1: the document no longer claims exit 4 for tunnels the connector opens
- Actual: 1 then 1: the document claims exit 4 while TcpConnector opens SOCKS tunnels

## Re-audits

## Log

- 2026-10-16: filed proposed.
