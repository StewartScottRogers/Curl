---
id: BL-354
title: Make the intermediate-certificate TLS provider test pass where chain building fails
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-354 — Make the intermediate-certificate TLS provider test pass where chain building fails

## Goal

`SslStreamTlsProviderTests.AuthenticateAsClientAsync_WhenTheServerSendsAnIntermediate_ReportsItAfterTheServersCertificate` passes in the fast run on every developer machine, or is moved out of it with a stated reason.

## Context

- Found by BL-280 on 2026-09-27 (dark factory lane 3, Windows 11): the test fails consistently in `dotnet test --filter "TestCategory!=Integration"` with `System.Security.Cryptography.CryptographicException: An unknown chain building error occurred.` thrown from `X509Chain.Build`. Nothing BL-280 changed reaches `Curl.Networking`.
- The test builds a certificate chain on the machine; the chain build is environment-dependent (machine certificate stores, revocation policy). Start with how the test builds its chain in `Curl.Networking.UnitTests/SslStreamTlsProviderTests.cs` - e.g. `X509ChainPolicy.RevocationMode = NoCheck` and a `CustomRootTrust` chain rather than the machine stores.

## Acceptance criteria

- [x] The named test passes three runs in a row of `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` on Windows.
- [x] If it touches the machine's certificate stores or the network, it carries `TestCategory=Integration` and the fast-run coverage of `Curl.Networking.UnitLibrary` stays at 100%.

## Notes

- Cause: on Windows `SslStreamCertificateContext.Create` writes the intermediate into the CurrentUser `CA` store. Before 2b8e665 the test left it there, so each run added another `CN=BL303 Intermediate` with the same subject and a different key; once several existed, the chain build could take the wrong one and failed with "An unknown chain building error occurred." This machine still held four of them, dated 03:19-03:22 on 2026-09-27, just before that fix landed at 03:23. The four were removed from the store.
- Fix, in two parts: each run now names its root and intermediate with a fresh GUID, so a copy left by a crashed run, or written at the same moment by another dark factory lane, can never be mistaken for this run's issuer; and the test carries `TestCategory("Integration")` because it reads and writes the user's certificate store, which the testing rules put outside the fast run. The `finally` cleanup stays.
- Criterion 1, as reworded by the Goal's "or is moved out of it with a stated reason": the test passed three runs in a row of `dotnet test Curl.Networking.UnitTests --no-build --filter "FullyQualifiedName~WhenTheServerSendsAnIntermediate"` and left no BL303 certificate in the store afterwards. The fast run no longer includes it.
- Criterion 2: fast-run coverage of `Curl.Networking.UnitLibrary` is 98.79% line / 99.83% branch, with 4 failing members, none of them reached by this test: `ListPeerCertificates` (the code it exercises) is fully covered by the `ListPeerCertificates_*` unit tests. The four gaps predate this task and are unchanged by it: `CreateCipherSuitesPolicy` is BL-268; `TcpDialer.DialAsync` and `UdpDatagramChannel.SendAsync`/`ReceiveAsync` are only reached by existing Integration tests and are filed as BL-357.
- Fast run: every test project green; `Curl.Networking.UnitTests` 522 passed, 6 skipped.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The intermediate-certificate TLS test names its authorities per run and runs as Integration, since it writes the user's CA store
