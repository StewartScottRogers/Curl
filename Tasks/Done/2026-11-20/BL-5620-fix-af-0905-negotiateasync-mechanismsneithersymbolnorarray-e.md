---
id: BL-5620
title: Fix AF-0905: NegotiateAsync_MechanismsNeitherSymbolNorArray_EndsWithLoginDenied never checks the LoginDenied exit code its name promises
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp1.UnitTests/Client/Amqp1SaslNegotiationTests.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5620 — Fix AF-0905: NegotiateAsync_MechanismsNeitherSymbolNorArray_EndsWithLoginDenied never checks the LoginDenied exit code its name promises

## Goal

The defect the audit office reported as AF-0905 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0905 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Amqp1.UnitTests/Client/Amqp1SaslNegotiationTests.cs:50`

Location: `Curl.Extended.Protocol.Amqp1.UnitTests/Client/Amqp1SaslNegotiationTests.cs:50`

Step 1, reading every test in Curl.Extended.Protocol.Amqp1.UnitTests. The body's only assertion is Assert.AreEqual("AMQP 1.0 server does not offer the SASL PLAIN mechanism", failure!.Message); (line 54). failure.ExitCode is never checked, unlike its neighbour NegotiateAsync_MechanismNotOffered_EndsWithLoginDenied, which asserts Assert.AreEqual(CurlExitCode.LoginDenied, failure!.ExitCode). A branch that refused a malformed sasl-server-mechanisms field with another exit code would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.Amqp1.UnitTests/Client/Amqp1SaslNegotiationTests.cs -Pattern 'MechanismsNeitherSymbolNorArray_EndsWithLoginDenied' -Context 0,5
```

- Expected: The test asserts Assert.AreEqual(CurlExitCode.LoginDenied, failure!.ExitCode).
- Actual: Lines 50-55 assert only the message: Assert.AreEqual("AMQP 1.0 server does not offer the SASL PLAIN mechanism", failure!.Message);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added the LoginDenied exit-code assertion to the test; Amqp1 fast tests pass (240).

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-20: Doing -> Done. Added the LoginDenied exit-code assertion; Amqp1 fast tests pass
