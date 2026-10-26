---
id: AF-0441
title: QuicDialer_PublicConstructor_CreatesADialer's only assertion is IsNotNull on the result of new, which cannot fail
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-25_1944.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1542.md, 2026-10-25_1944.md).
key: quality:Curl.Networking.UnitTests/TcpConnectorQuicTests.cs:QuicDialer_PublicConstructor_CreatesADialer:weak-assertion
reproduction: none
task: BL-3518
tasks: BL-3518
found: 2026-10-25
found-at: 03fe150efe7a6f73cd1a1c8fbd2c7bda2248009b
scorecard: 2026-10-25_0941.md
duplicate-of:
closed: 2026-10-25
closed-how: consecutive
closed-by: 2026-10-25_1542.md, 2026-10-25_1944.md
---
# AF-0441 - QuicDialer_PublicConstructor_CreatesADialer's only assertion is IsNotNull on the result of new, which cannot fail

## Summary

Low finding from the quality auditor at `Curl.Networking.UnitTests/TcpConnectorQuicTests.cs:553`: QuicDialer_PublicConstructor_CreatesADialer's only assertion is IsNotNull on the result of new, which cannot fail. Reported by an auditor flagged unreliable in 2026-10-25_0941.md.

## Evidence

Location: `Curl.Networking.UnitTests/TcpConnectorQuicTests.cs:553`

Body: 'var dialer = new QuicDialer(new TlsClientOptions(), TimeProvider.System, IPAddress.Loopback, 0);' then 'Assert.IsNotNull(dialer);'. A C# constructor call never returns null, so the assertion cannot fail. Nothing checks what the public constructor wires up (its channel opener, local address or port). Found by Find-WeakTests.ps1 (weak-assertion) and confirmed by reading the test.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TcpConnectorQuicTests.cs -Pattern 'QuicDialer_PublicConstructor_CreatesADialer' -Context 0,9
```

- Expected: The test asserts something the constructor decides, such as the dialer's local binding or the UdpChannelOpener it uses.
- Actual: Its only assertion is Assert.IsNotNull(dialer) after new QuicDialer(...).

## Re-audits

- 2026-10-25 | 2026-10-25_1542.md | reproduces: no | Ran the Select-String reproduction: no QuicDialer_PublicConstructor_CreatesADialer matches. Its replacement, QuicDialer_PublicConstructor_BindsTheLocalAddressAndPortGiven, holds the local UDP port and checks the dialer's own bind fails, which needs the constructor to have wired a real opener to the given address and port.
- 2026-10-25 | 2026-10-25_1944.md | reproduces: no | Select-String finds no QuicDialer_PublicConstructor_CreatesADialer. The test is now QuicDialer_PublicConstructor_BindsTheLocalAddressAndPortGiven (TcpConnectorQuicTests.cs:545). It holds the local UDP port and asserts Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode), so it no longer stops at IsNotNull on new.

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-25: accepted -> closed. Re-audit 2026-10-25_1944.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1542.md, 2026-10-25_1944.md).
