---
id: BL-5524
title: OpenSSL build accepts an intermediate from --cacert or --capath that the server did not send
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.UnsentIntermediate.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5524 — OpenSSL build accepts an intermediate from --cacert or --capath that the server did not send

## Goal

In the OpenSSL build, a chain whose intermediate the server did not send but the user's `--cacert` or `--capath` anchors list verifies as in curl, instead of failing with exit 60 "unable to get local issuer certificate (20)".

## Context

Found by the shift-end review of 2026-11-18 (ADR-0710), pr-review-toolkit code-reviewer (rated Critical) and silent-failure-hunter (Important); confirmed against the code.

`Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:330-336`, `RelyingOnAnIntermediateTheServerDidNotSend`, added by BL-5449 (STD-131), refuses any chain element between the leaf and the last element that the server did not send:

```csharp
&& chain.ChainElements.Skip(1).SkipLast(1).Any(element => !WasSent(element.Certificate, peerCertificates));
```

It runs first in the judge (line 297). It has two problems:

- It does not exempt a certificate the user's trust anchors list. `chain.ChainPolicy.CustomTrustStore` is filled from `--cacert` and `--capath` at lines 227-228.
- It ignores `AnchorIndexOf(chain)` (`ServerCertificateVerification.ChainErrors.cs`). The same shift added that helper because the chain can continue past a listed anchor that is not self-signed (BL-5438).

**Failure:** `curl --cacert bundle.pem https://host` on Linux or macOS, where `bundle.pem` holds the intermediate and the root and the server sends only its leaf. OpenSSL finds the intermediate in its trusted store and succeeds. Curl exits 60.

The intent of BL-5449 must stay: an intermediate that only .NET's CurrentUser CA store or an AIA download supplied is still refused.

## Acceptance criteria

- [ ] `RelyingOnAnIntermediateTheServerDidNotSend` counts an element as available when the server sent it or when `chain.ChainPolicy.CustomTrustStore` lists it.
- [ ] `RelyingOnAnIntermediateTheServerDidNotSend` checks only the elements below `AnchorIndexOf(chain)`.
- [ ] A unit test in `ServerCertificateVerificationTests.UnsentIntermediate.cs` covers the OpenSSL build with an unsent intermediate that the custom trust store lists, and the chain is accepted.
- [ ] The existing test `Judge_IntermediateTheServerDidNotSendInTheOpenSslBuild_IsExit60WithError20` still passes: an unsent intermediate the anchors do not list is still exit 60 with error 20.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: dc90fc2a f4ecab4a 46ff4b1a 6ced534c 3d836d5e.
