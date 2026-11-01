---
id: GF-0079
title: A wrong --pinnedpubkey with --cacert on the Schannel build gives exit 60 (revocation unknown) instead of 90
area: behaviour
platform: windows
key: behaviour:schannel-cacert-revocation-judged-before-pinned-public-key
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-15_0304
closed: 2026-10-15_0543
regression: false
items: [behaviour:test2035, behaviour:test2038, behaviour:test2042]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
task: BL-2270
tasks: [BL-2270]
---
# GF-0079 - A wrong --pinnedpubkey with --cacert on the Schannel build gives exit 60 (revocation unknown) instead of 90

## Summary

Curl differs from upstream curl in behaviour: A wrong --pinnedpubkey with --cacert on the Schannel build gives exit 60 (revocation unknown) instead of 90.

## Evidence

behaviour:test2035, test2038 and test2042 each expected 'upstream test<N> passes', actual '<verify><errorcode>: expected exit code 90, got 60'. All three run '-4 --cacert %CERTDIR/certs/test-ca.crt --pinnedpubkey <wrong key: test-localhost.der, test-localhost.pem, or a wrong sha256// hash> https://localhost:%HTTPSPORT/<N>' with no --ssl-revoke-best-effort, and none is marked !Schannel. Curl.Networking.UnitLibrary/ServerCertificateVerification.cs Judge runs VerifyPeer first and JudgePinnedPublicKey only when it accepts; on the Schannel build the chain policy uses X509RevocationMode.Online for a --cacert chain, so the private test CA's unknown revocation status refuses the chain with exit 60 before the pin is reached. Upstream's positive Schannel case test2033 adds --ssl-revoke-best-effort, consistent with the pin being judged before the manual --cacert chain check in that build. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- '<release>/tests/data' $env:TEMP/raw.json 2035 (release C:/Users/Stewart Rogers/AppData/Local/Curl/gap/upstream/8.22.0)

## Suggestion

First measure the Schannel reference curl on a loopback TLS server with '--cacert <private CA> --pinnedpubkey <wrong key>' and no --ssl-revoke-best-effort. If it exits 90 as upstream's test2035, test2038 and test2042 expect, change Curl.Networking.UnitLibrary ServerCertificateVerification.Judge so that, when matchesSchannelBuild is set and a --cacert file drives the chain check (curl's Schannel manual credential validation path), PinnedPublicKey.Refusal is judged before VerifyPeer and the revocation check. Keep the current order for the OpenSSL build and for system-store validation. Add focused tests in Curl.Networking.UnitTests for a wrong pin with an unknown-revocation --cacert chain on each build. Explained by ADR-0193: it measured 'verification is judged before the pin' on the Schannel build, but its table has no --cacert chain whose revocation status is unknown (ADR-0321), which is the path these three cases take; the ADR does not remove the gap.

## Measurements

- 2026-10-15_0304: 3 of 3 items are gaps.
- 2026-10-15_0543: 0 of 3 items are gaps.
- 2026-10-15_0751: 0 of 3 items are gaps.
- 2026-10-15_1657: 0 of 3 items are gaps.
- 2026-10-17_2225: 0 of 3 items are gaps.
- 2026-10-19_0804: 0 of 3 items are gaps.
- 2026-10-19_1329: 0 of 3 items are gaps.
- 2026-10-22_0148: 0 of 3 items are gaps.
- 2026-10-23_1637: 0 of 3 items are gaps.
- 2026-10-25_1542: 0 of 3 items are gaps.
- 2026-10-30_2103: 0 of 3 items are gaps.
- 2026-10-31_1828: 0 of 3 items are gaps.

## Log

- 2026-10-15_0304: Opened by gap-behaviour.
- 2026-10-15_0349: Filed BL-2270.
- 2026-10-15_0543: Closed: run 2026-10-15_0543 measured every item as match or excluded.
