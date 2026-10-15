---
id: BL-2270
title: Close GF-0079: A wrong --pinnedpubkey with --cacert on the Schannel build gives exit 60 (revocation unknown) instead of 90
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2270 — Close GF-0079: A wrong --pinnedpubkey with --cacert on the Schannel build gives exit 60 (revocation unknown) instead of 90

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0079 (A wrong --pinnedpubkey with --cacert on the Schannel build gives exit 60 (revocation unknown) instead of 90), so a later gap analysis measures each of `behaviour:test2035`, `behaviour:test2038`, `behaviour:test2042` as `match`.

## Context

- Finding: GF-0079, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test2035`, `behaviour:test2038`, `behaviour:test2042`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test2035, test2038 and test2042 each expected 'upstream test<N> passes', actual '<verify><errorcode>: expected exit code 90, got 60'. All three run '-4 --cacert %CERTDIR/certs/test-ca.crt --pinnedpubkey <wrong key: test-localhost.der, test-localhost.pem, or a wrong sha256// hash> https://localhost:%HTTPSPORT/<N>' with no --ssl-revoke-best-effort, and none is marked !Schannel. Curl.Networking.UnitLibrary/ServerCertificateVerification.cs Judge runs VerifyPeer first and JudgePinnedPublicKey only when it accepts; on the Schannel build the chain policy uses X509RevocationMode.Online for a --cacert chain, so the private test CA's unknown revocation status refuses the chain with exit 60 before the pin is reached. Upstream's positive Schannel case test2033 adds --ssl-revoke-best-effort, consistent with the pin being judged before the manual --cacert chain check in that build. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- '<release>/tests/data' $env:TEMP/raw.json 2035 (release C:/Users/Stewart Rogers/AppData/Local/Curl/gap/upstream/8.22.0)

Suggestion, copied from the finding:

First measure the Schannel reference curl on a loopback TLS server with '--cacert <private CA> --pinnedpubkey <wrong key>' and no --ssl-revoke-best-effort. If it exits 90 as upstream's test2035, test2038 and test2042 expect, change Curl.Networking.UnitLibrary ServerCertificateVerification.Judge so that, when matchesSchannelBuild is set and a --cacert file drives the chain check (curl's Schannel manual credential validation path), PinnedPublicKey.Refusal is judged before VerifyPeer and the revocation check. Keep the current order for the OpenSSL build and for system-store validation. Add focused tests in Curl.Networking.UnitTests for a wrong pin with an unknown-revocation --cacert chain on each build. Explained by ADR-0193: it measured 'verification is judged before the pin' on the Schannel build, but its table has no --cacert chain whose revocation status is unknown (ADR-0321), which is the path these three cases take; the ADR does not remove the gap.

## Acceptance criteria

- [x] `behaviour:test2035`: Curl answers what curl 8.22.0 answers, `upstream test2035 passes`, so the item measures `match`.
- [x] `behaviour:test2038`: Curl answers what curl 8.22.0 answers, `upstream test2038 passes`, so the item measures `match`.
- [x] `behaviour:test2042`: Curl answers what curl 8.22.0 answers, `upstream test2042 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). No option was added or changed, so `--ai-help` is unchanged.

## Notes

- Measured curl 8.21.0 Schannel with `Record-CurlExchange.ps1 -Tls -TlsRootCertificateFile`, `-sS --cacert root.pem`: a wrong `sha256//` pin is exit 90, the right pin exit 60 (`schannel: the revocation status is unknown`), no pin exit 60. So with `--cacert` the pin is judged before the manual chain check.
- `ServerCertificateVerification.Judge` now judges the pin first when `matchesSchannelBuild` and `--cacert` are both set (`JudgesPinnedPublicKeyBeforeVerification`); the OpenSSL build and system-store validation keep the old order. ADR-0504 records it.
- Focused tests in `SslStreamTlsProviderTests.ChainErrors.cs`: a wrong pin with an unknown-revocation `--cacert` chain is exit 90 on both builds; the right pin on Schannel is still exit 60.
- `CURL_UPSTREAM_CASE=2035`, `2038`, `2042` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`: all three pass. Adding them to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is outside this task's touches; the ratchet picks them up.
- Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary: 0 failing members. Full build clean; fast tests green in all 37 test projects.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. A wrong --pinnedpubkey with --cacert on the Schannel build is exit 90 (pin judged before the manual chain check); upstream test2035, test2038, test2042 pass
