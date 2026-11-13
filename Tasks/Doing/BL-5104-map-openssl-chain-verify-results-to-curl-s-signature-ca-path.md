---
id: BL-5104
title: Map OpenSSL chain-verify results to curl's signature, CA, path-length and name-constraint messages on Linux
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs, Curl.Networking.UnitTests/OpenSslVerifyResultTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5104 — Map OpenSSL chain-verify results to curl's signature, CA, path-length and name-constraint messages on Linux

## Goal

On Linux and macOS, the exit-60 message for a failed certificate chain names the real OpenSSL verify result, as curl 8.18.0 (OpenSSL) does: signature failure (7), path length constraint exceeded (25), permitted subtree violation (47) and invalid CA certificate (79), instead of `self-signed certificate in certificate chain (19)` for all of them.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build; curl 8.18.0 OpenSSL, REF2, is the Linux reference per ADR-0505/ADR-0540), finding STD-034 (Low, Confirmed on all three Linux routes; exit codes match, message text only), re-reported with status CHANGED (mostly fixed). Earlier task BL-5032 (Done) covered Windows Schannel text. This task is the Linux half; the Windows half is the separate task "Print 'the revocation status is unknown' for Schannel chain errors ...".

**Repro (Linux).** `curl -sS https://localhost:<port>/ --cacert ca.pem` against (serve with PowerShell or a C# file-based app, not Python):
- a pathlen:0 violation: curl prints `path length constraint exceeded (25)`;
- a name-constrained intermediate permitting only `allowed.test`: `permitted subtree violation (47)`;
- a leaf signed by a CA:FALSE certificate, or under an intermediate without keyCertSign: `invalid CA certificate (79)`;
- a corrupted signature: `certificate signature failure (7)`.

**Actual.** The port prints `self-signed certificate in certificate chain (19)` for all of them.

**Where to start.** `Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs` (about line 115): map `X509ChainStatusFlags.NotSignatureValid` -> 7, `InvalidBasicConstraints` -> 79 (25 for a path length violation), `HasNotPermittedNameConstraint` -> 47. Existing tests: `Curl.Networking.UnitTests/OpenSslVerifyResultTests.cs`. Keep the existing mappings (untrusted root 19/20, expired 10, and so on) unchanged.

## Acceptance criteria

- [ ] New cases in `Curl.Networking.UnitTests/OpenSslVerifyResultTests.cs` prove each of `NotSignatureValid`, `InvalidBasicConstraints` (with and without a path-length violation), and `HasNotPermittedNameConstraint` yields the message text and code above (`certificate signature failure (7)`, `invalid CA certificate (79)`, `path length constraint exceeded (25)`, `permitted subtree violation (47)`).
- [ ] A test proves the precedence when several flags are set matches what OpenSSL reports first, and the existing mapping tests stay green.
- [ ] Tests are platform-neutral (they feed chain status flags to the mapping).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; use PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python.
- Review acceptance check, for a Linux hand check: the pathlen case prints `path length constraint exceeded (25)`.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: e142f849 7e0dbfc6 6687648c a0b49a29.
