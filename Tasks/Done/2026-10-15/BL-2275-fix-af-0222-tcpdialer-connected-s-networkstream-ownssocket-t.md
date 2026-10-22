---
id: BL-2275
title: Fix AF-0222: TcpDialer.Connected's NetworkStream ownsSocket: true can become false with no test failing, so a dialed socket is never closed on dispose
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2275 — Fix AF-0222: TcpDialer.Connected's NetworkStream ownsSocket: true can become false with no test failing, so a dialed socket is never closed on dispose

## Goal

The defect the audit office reported as AF-0222 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0222 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0222-tcpdialer-connected-s-networkstream-ownssocket-tru.md`.

Location: `Curl.Networking.UnitLibrary/TcpDialer.cs:228`

Location: `Curl.Networking.UnitLibrary/TcpDialer.cs:228`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 (score 0.8684, 32 killed, 1 timed out, 5 survived, 2 stillborn) reported the mutant survived: 'Stream stream = socket.Connected ? new NetworkStream(socket, ownsSocket: true) : new DeferredConnectSocketStream(socket);' -> 'ownsSocket: false'. A second run with the same seed gave the same outcome. Why it matters: StreamConnection.DisposeAsync (StreamConnection.cs:85) only disposes the stream, and DialedTcpConnection wraps that. With ownsSocket false, closing any dialed TCP connection leaves the socket open, so the peer sees no FIN until the process exits. That changes behaviour a server can see: a connection curl ends, or an upload whose end is signalled by closing the connection. The method is [ExcludeFromCodeCoverage] ('measured by the Integration run'), so the unit twin cannot reach it. Curl.Networking.IntegrationTests/TcpDialerIntegrationTests.cs has no test that checks the peer sees the close after DisposeAsync either (its tests check bytes carried, endpoints and a refused dial). Of the 5 survivors this is the only one kept: the other four are equivalent (PinnedPublicKey.cs:140 '>' -> '>=' in a branch reached only when lengths differ; EncryptedPrivateKeyInfoDecryption.cs:96 '&&' -> '||' where the BCL's Pbkdf2 refuses a non-positive count and the same CryptographicException is thrown either way; DnsSourceBinding.cs:31 and DnsServerResolver.cs:168 flip a compile-time-only [NotNullWhen] attribute).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpDialer.cs:228:true -Member Connected -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test fails when a dialed connection's dispose leaves its socket open.
- Actual: outcome survived: every Curl.Networking.UnitTests test passed with ownsSocket: false.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: `TcpDialer.Connected` is now `internal` and no longer `[ExcludeFromCodeCoverage]`; it needs only a
  socket with a local end point, which a unit test can make without sending a byte (ADR-0421). Two new tests
  in `TcpDialerTests` (`Connected_WithAConnectedSocket_...` over a loopback listener, and
  `Connected_WithASocketWhoseConnectIsDeferred_...` over a bound, unconnected socket) dispose the dialed
  connection and assert the socket's handle is closed. The stream line stays at `TcpDialer.cs:228` and in
  member `Connected`, so the finding's `-Site ...:228:true -Member Connected` still names it.
- Reproduction: a lane may not run `Audit/Tools/Invoke-MutationTest.ps1` (audit guard), so the mutant was
  applied by hand instead: with `ownsSocket: false` on line 228,
  `Connected_WithAConnectedSocket_DisposingTheConnectionClosesTheSocket` fails ("Expected condition to be
  true"); restored, both pass. The quality auditor's re-audit closes the finding.
- Added `Curl.Networking.UnitTests` to `touches`: the mutation run measures that project's tests, so the
  killing test must live there; no other task in Doing names it.
- Fast tests: 37 projects green (Curl.Networking.UnitTests 3170 passed, 30 skipped). Measure-CodeQuality
  for Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Disposing a dialed TCP connection closes its socket, pinned by two TcpDialerTests that kill AF-0222's ownsSocket: false mutant
