---
id: AF-0905
title: NegotiateAsync_MechanismsNeitherSymbolNorArray_EndsWithLoginDenied never checks the LoginDenied exit code its name promises
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-20_1740.md: the reproduction no longer reproduces.
key: quality:Curl.Extended.Protocol.Amqp1.UnitTests/Client/Amqp1SaslNegotiationTests.cs:NegotiateAsync_MechanismsNeitherSymbolNorArray_EndsWithLoginDenied:name-lies
reproduction: none
task: BL-5620
tasks: BL-5620
found: 2026-11-20
found-at: 1ce730765b795e99d4d034faf03432b41b5ec6a4
scorecard: 2026-11-20_0456.md
duplicate-of:
closed: 2026-11-20
closed-how: reliable-reaudit
closed-by: 2026-11-20_1740.md
---
# AF-0905 - NegotiateAsync_MechanismsNeitherSymbolNorArray_EndsWithLoginDenied never checks the LoginDenied exit code its name promises

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.Amqp1.UnitTests/Client/Amqp1SaslNegotiationTests.cs:50`: NegotiateAsync_MechanismsNeitherSymbolNorArray_EndsWithLoginDenied never checks the LoginDenied exit code its name promises.

## Evidence

Location: `Curl.Extended.Protocol.Amqp1.UnitTests/Client/Amqp1SaslNegotiationTests.cs:50`

Step 1, reading every test in Curl.Extended.Protocol.Amqp1.UnitTests. The body's only assertion is Assert.AreEqual("AMQP 1.0 server does not offer the SASL PLAIN mechanism", failure!.Message); (line 54). failure.ExitCode is never checked, unlike its neighbour NegotiateAsync_MechanismNotOffered_EndsWithLoginDenied, which asserts Assert.AreEqual(CurlExitCode.LoginDenied, failure!.ExitCode). A branch that refused a malformed sasl-server-mechanisms field with another exit code would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.Amqp1.UnitTests/Client/Amqp1SaslNegotiationTests.cs -Pattern 'MechanismsNeitherSymbolNorArray_EndsWithLoginDenied' -Context 0,5
```

- Expected: The test asserts Assert.AreEqual(CurlExitCode.LoginDenied, failure!.ExitCode).
- Actual: Lines 50-55 assert only the message: Assert.AreEqual("AMQP 1.0 server does not offer the SASL PLAIN mechanism", failure!.Message);

## Re-audits

- 2026-11-20 | 2026-11-20_1740.md | reproduces: no | Ran the Select-String reproduction: NegotiateAsync_MechanismsNeitherSymbolNorArray_EndsWithLoginDenied now asserts `Assert.AreEqual(CurlExitCode.LoginDenied, failure!.ExitCode);` (line 54) and the message (line 55).

## Log

- 2026-11-20: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_1740.md: the reproduction no longer reproduces.
