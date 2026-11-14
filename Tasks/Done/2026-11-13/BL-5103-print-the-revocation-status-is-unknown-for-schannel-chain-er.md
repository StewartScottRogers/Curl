---
id: BL-5103
title: Print 'the revocation status is unknown' for Schannel chain errors curl reports that way
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/TlsFailureMessagesTests.RevocationUnknown.cs]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5103 — Print 'the revocation status is unknown' for Schannel chain errors curl reports that way

## Goal

On Windows, the exit-60 message for a chain whose only reported problems are an unknown or offline revocation status reads `schannel: the revocation status is unknown`, as curl's Schannel backend prints, instead of `schannel: error 0x...`.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build; curl 8.22.0 Schannel as the reference), finding STD-034 (Low, Confirmed; exit codes match, message text only), re-reported with status CHANGED (mostly fixed). Earlier task BL-5032 (Done) made most chain messages specific; two gaps remain. This task is the Windows half; the Linux half is the separate task "Map OpenSSL chain-verify results to curl's ... messages on Linux". Do not change the `error 0x00000400` text for a path-length violation.

**Repro (Windows).** `curl -sS https://localhost:<port>/ --cacert ca.pem` (no `--ssl-no-revoke`) against: (a) a leaf with one corrupted signature byte; (b) a leaf signed by a CA:FALSE certificate whose CRL distribution point serves a CRL signed by that non-CA; (c) a leaf under an intermediate whose keyUsage is only `digitalSignature`. (Serve with PowerShell or a C# file-based app, not Python.)

**Expected (curl 8.22.0).** `curl: (60) schannel: the revocation status is unknown` for all three (curl checks CERT_TRUST_REVOCATION_STATUS_UNKNOWN before falling back to `error 0x%08x`).

**Actual.** `schannel: error 0x00000008`, `error 0x00000400`, `error 0x00000010` respectively.

**Where to start.** `Curl.Networking.UnitLibrary/TlsFailureMessages.cs`, `SchannelChainErrorOfUntrustedOrUnknownStatus` (about line 909) returns the raw mask before checking RevocationStatusUnknown / OfflineRevocation; check those after Revoked, PartialChain, UntrustedRoot and NotTimeValid. Existing tests: `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs`, `TlsFailureMessagesTests.HandBuilt.cs`.

## Acceptance criteria

- [x] New tests in `Curl.Networking.UnitTests/TlsFailureMessagesTests.RevocationUnknown.cs` prove that chain status flag combinations as produced by cases (a), (b) and (c) (NotSignatureValid with RevocationStatusUnknown, NotValidForUsage-style flags with RevocationStatusUnknown, and OfflineRevocation) print `schannel: the revocation status is unknown`.
- [x] Tests prove the precedence: Revoked, PartialChain, UntrustedRoot and NotTimeValid still win over the unknown status, and a status with a path-length/other error and no revocation flag still prints `error 0x00000400` as before.
- [x] Tests are platform-neutral (they feed `X509ChainStatusFlags` values to the message function).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; use PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python.
- Review acceptance check, for a Windows hand check: the corrupted-signature case prints `the revocation status is unknown` and the pathlen case still prints `error 0x00000400`.

- Decision: precedence is NotTimeValid, Revoked, PartialChain, UntrustedRoot, then unknown/offline revocation, then the raw mask. Removed a test row that pinned the old raw mask for NotSignatureValid with unknown revocation. Quality run: 0 failing members; tests are platform-neutral.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: a89e0105 08b69a0b 275452ca a0b49a29.
- 2026-11-13: Doing -> Done. Unknown or offline revocation status prints curl's schannel line
