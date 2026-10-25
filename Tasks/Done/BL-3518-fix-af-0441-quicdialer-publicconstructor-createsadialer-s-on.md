---
id: BL-3518
title: Fix AF-0441: QuicDialer_PublicConstructor_CreatesADialer's only assertion is IsNotNull on the result of new, which cannot fail
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-25
completed: 2026-10-25
---
# BL-3518 — Fix AF-0441: QuicDialer_PublicConstructor_CreatesADialer's only assertion is IsNotNull on the result of new, which cannot fail

## Goal

The defect the audit office reported as AF-0441 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0441 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0441-quicdialer-publicconstructor-createsadialer-s-only.md`.

Location: `Curl.Networking.UnitTests/TcpConnectorQuicTests.cs:553`

Location: `Curl.Networking.UnitTests/TcpConnectorQuicTests.cs:553`

Body: 'var dialer = new QuicDialer(new TlsClientOptions(), TimeProvider.System, IPAddress.Loopback, 0);' then 'Assert.IsNotNull(dialer);'. A C# constructor call never returns null, so the assertion cannot fail. Nothing checks what the public constructor wires up (its channel opener, local address or port). Found by Find-WeakTests.ps1 (weak-assertion) and confirmed by reading the test.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TcpConnectorQuicTests.cs -Pattern 'QuicDialer_PublicConstructor_CreatesADialer' -Context 0,9
```

- Expected: The test asserts something the constructor decides, such as the dialer's local binding or the UdpChannelOpener it uses.
- Actual: Its only assertion is Assert.IsNotNull(dialer) after new QuicDialer(...).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Replaced the test with `QuicDialer_PublicConstructor_BindsTheLocalAddressAndPortGiven`: a loopback UDP socket holds a port, the public constructor is given `IPAddress.Loopback` and that port, and a multiplexed connect through `TcpConnector` fails with exit 7 at the bind, before any datagram is sent. It passes only if the constructor wired a real `UdpChannelOpener` to the address and port it was given. The reproduction's Select-String now finds no `CreatesADialer` test; the test in its place asserts the dialer's local binding.
- Pipeline `feature` run as a direct change: one test-only edit, no production code, so no Measure-CodeQuality run was needed.
- Platform check: a second UDP bind to a held port without SO_REUSEADDR fails on Linux and macOS too (the existing `BindLocalEnd_WhenEveryPortIsInUse*` tests depend on the same). Only the exit code is asserted, not the platform's own words. Full build clean; fast tests green on Windows (46 projects passed, 0 failed).

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-25: Doing -> Done. QuicDialer's public-constructor test now checks the dialer binds the local address and port it was given
