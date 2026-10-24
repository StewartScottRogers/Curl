---
id: BL-3440
title: Drive ProxyTunnelTests over in-memory streams instead of loopback sockets that send bytes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Differential.UnitTests/Protocols/Proxy/ProxyTunnelTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3440 — Drive ProxyTunnelTests over in-memory streams instead of loopback sockets that send bytes

## Goal

`ProxyTunnelTests` checks both of `ProxyTunnel.RelayAsync`'s end orders without opening a socket, so it is a unit test by ADR-0421's definition and still covers the lines BL-3436 added.

## Context

Found by the shift-end review of 2026-10-23 (ADR-0710, code-reviewer) in the shift merged at `bb39553be`.

`Curl.Conformance.Differential.UnitTests/Protocols/Proxy/ProxyTunnelTests.cs:13-48, 63-72` (BL-3436) opens two `TcpListener`/`TcpClient` loopback pairs per test and writes payloads across them (`target.WriteAsync(new byte[] { 1, 2, 3 })`, `client.WriteAsync(new byte[] { 4, 5 })`). The root `CLAUDE.md` and ADR-0421 allow a loopback socket in a unit test only if it "opens and closes without sending a byte"; anything more is an Integration test. `VerifyIntegrationTestPlacement` does not catch it because the tests carry no `Integration` category. It is the only test in `Curl.Conformance.Differential.UnitTests` that uses `TcpListener`.

Moving the tests to `Curl.Conformance.Differential.IntegrationTests` would satisfy the placement rule but drop the coverage of `ProxyTunnel.cs`'s new `if (ended == toClient)` branch, because coverage is measured from the fast tests only. So keep them as unit tests and replace the sockets with an in-memory duplex stream pair: a small hand-written fake in the test project (BCL only, no package), where disposing one end makes the other end's read return 0, as a socket's does.

## Acceptance criteria

- [ ] `ProxyTunnelTests.cs` creates no `TcpListener`, `TcpClient` or `Socket`; `grep -n "TcpListener\|TcpClient\|Socket" Curl.Conformance.Differential.UnitTests/Protocols/Proxy/ProxyTunnelTests.cs` finds nothing.
- [ ] `RelayAsync_TargetEndsFirst_ClosesTheClientAndReturns` and `RelayAsync_ClientEndsFirst_ClosesTheTargetAndReturns` still pass, each asserting the relayed bytes and that the other side then reads end of stream.
- [ ] Each test still fails if the `await connection.DisposeAsync()` in `ProxyTunnel.RelayAsync` is removed (target-ends-first) or `remote.Dispose()` is removed (client-ends-first), checked by hand and noted in the Log.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary` reports `ProxyTunnel.RelayAsync` at 100% line and branch coverage.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-23: Created.
