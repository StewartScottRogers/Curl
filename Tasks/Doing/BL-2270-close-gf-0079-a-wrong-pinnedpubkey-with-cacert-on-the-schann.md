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
completed:
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

- [ ] `behaviour:test2035`: Curl answers what curl 8.22.0 answers, `upstream test2035 passes`, so the item measures `match`.
- [ ] `behaviour:test2038`: Curl answers what curl 8.22.0 answers, `upstream test2038 passes`, so the item measures `match`.
- [ ] `behaviour:test2042`: Curl answers what curl 8.22.0 answers, `upstream test2042 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
