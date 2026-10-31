---
id: AF-0449
title: CreateKdcClient_Configuration_MakesAClient's only assertion is IsNotNull on a constructed client
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-29_0606.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1944.md, 2026-10-29_0606.md).
key: quality:Curl.Engine.UnitTests/HandBuiltKerberosSourcesTests.cs:CreateKdcClient_Configuration_MakesAClient:weak-assertion
reproduction: none
task: BL-3526
tasks: BL-3526
found: 2026-10-25
found-at: cb96798969fdc083bbbf245bc653f5fc69cb8acf
scorecard: 2026-10-25_1542.md
duplicate-of:
closed: 2026-10-29
closed-how: consecutive
closed-by: 2026-10-25_1944.md, 2026-10-29_0606.md
---
# AF-0449 - CreateKdcClient_Configuration_MakesAClient's only assertion is IsNotNull on a constructed client

## Summary

Low finding from the quality auditor at `Curl.Engine.UnitTests/HandBuiltKerberosSourcesTests.cs:86`: CreateKdcClient_Configuration_MakesAClient's only assertion is IsNotNull on a constructed client. Reported by an auditor flagged unreliable in 2026-10-25_1542.md.

## Evidence

Location: `Curl.Engine.UnitTests/HandBuiltKerberosSourcesTests.cs:86`

The test calls Sources(...).CreateKdcClient(KerberosConfiguration.Empty) and asserts only Assert.IsNotNull(client). A non-nullable factory result cannot be null, so the test cannot fail unless it throws. Nothing checks that the client uses the configuration or the sources given.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/HandBuiltKerberosSourcesTests.cs -Pattern 'CreateKdcClient_Configuration_MakesAClient' -Context 0,9
```

- Expected: An assertion on how the client behaves with the configuration (for example which KDC it would contact).
- Actual: The only assertion is Assert.IsNotNull(client);

## Re-audits

- 2026-10-25 | 2026-10-25_1944.md | reproduces: no | Select-String finds no CreateKdcClient_Configuration_MakesAClient as the old test. It is now CreateKdcClient_Configuration_MakesAClientThatContactsTheConfiguredKdcThroughTheTransport (HandBuiltKerberosSourcesTests.cs:78). It asserts KerberosKdcException with Error KdcUnreachable, and that the transport contacted exactly kdc.example.test:750.
- 2026-10-29 | 2026-10-29_0606.md | reproduces: no | Ran the Select-String reproduction. It matched CreateKdcClient_Configuration_MakesAClientThatContactsTheConfiguredKdcThroughTheTransport (HandBuiltKerberosSourcesTests.cs:78), which now drives the client and asserts KerberosKdcError.KdcUnreachable and that the transport contacted exactly kdc.example.test:750. There is no lone IsNotNull.

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-29: accepted -> closed. Re-audit 2026-10-29_0606.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1944.md, 2026-10-29_0606.md).
