---
id: BL-3517
title: Fix AF-0439: GophersServerConnector can start asking the client for a certificate with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests/GophersServerConnectorTests.cs, Curl.Conformance.UnitTests/MailTlsServerConnectorTests.cs]
requirement: none
created: 2026-10-25
completed: 2026-10-25
---
# BL-3517 — Fix AF-0439: GophersServerConnector can start asking the client for a certificate with no test failing

## Goal

The defect the audit office reported as AF-0439 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0439 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0439-gophersserverconnector-can-start-asking-the-client.md`.

Location: `Curl.Conformance.UnitLibrary/GophersServerConnector.cs:22`

Location: `Curl.Conformance.UnitLibrary/GophersServerConnector.cs:22`

Sampled mutant (seed 0) survived: 'new(certificate, [], false)' -> 'new(certificate, [], true)'. That third argument is TlsServerOptions.RequestClientCertificate. The class doc says the stand-in behaves as stunnel at verify = 0, which asks for no client certificate. With the mutant every gophers handshake sends a CertificateRequest, which changes the handshake curl sees (and its -v TLS lines) for gophers cases such as 1272. GophersServerConnectorTests never checks that no certificate is asked for.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/GophersServerConnector.cs:22:false -Member options -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was right (stunnel at verify = 0 asks for no client certificate); the gap was the test. Added `GophersServerConnectorTests.ConnectAsync_GophersPort_AsksForNoClientCertificate`: the client offers a certificate and the test asserts the handshake is not mutually authenticated. `MailTlsServerConnectorTests.HandshakeAsync` gained an optional client certificate for it.
- Touches widened to the two test files: the fix is a test in the library's own test project; no other task in Doing on origin/work/dark-factory names them (only BL-3522, Curl.Mcp*).
- The audit office's mutation tool is out of a lane's reach, so its mutant was applied by hand: with `new(certificate, [], true)` the new test fails (`Assert.IsFalse(sentCertificate)`); without it all 4 GophersServerConnectorTests pass. The mutant is killed.
- Platform check: in-memory connection, generated certificates, and an assertion on `SslStream.IsMutuallyAuthenticated`, which is true only when the client sent a certificate; no paths or OS text.
- No production code changed, so no Measure-CodeQuality run. Build clean; fast tests green (Curl.Conformance.UnitTests 4381 passed, 87 skipped, 0 failed).

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-25: Doing -> Done. GophersServerConnectorTests now fails when the gophers stand-in asks the client for a certificate
