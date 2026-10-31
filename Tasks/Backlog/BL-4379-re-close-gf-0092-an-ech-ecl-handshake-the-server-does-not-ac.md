---
id: BL-4379
title: Re-close GF-0092: An --ech ecl: handshake the server does not accept exits 35 instead of 101 (ECH required) on macOS (on macos)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
lane: no
requirement: none
created: 2026-10-30
completed:
---
# BL-4379 — Re-close GF-0092: An --ech ecl: handshake the server does not accept exits 35 instead of 101 (ECH required) on macOS (on macos)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0092 (An --ech ecl: handshake the server does not accept exits 35 instead of 101 (ECH required) on macOS), so a later gap analysis measures each of `behaviour:test4001` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0092 ([BL-2335]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0092, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Platform: macos, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is macos's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on macos passing a test that pins each item; write those tests platform-neutral or mark them for macos.
- Items: `behaviour:test4001`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test4001 expected 'upstream test4001 passes', actual '<verify><errorcode>: expected exit code 101, got 35'. The case runs '--ech ecl:AEv+DQBHdAAgACCCU49qdxKOUXJPs3wlsM06v/t42sMH5xQOL37MAd3HaAAEAAEAAQAYb3RoZXJwdWJsaWMudGVzdC5kZWZvLmllAAA= -k https://%HOSTIP:%HTTPSPORT/4001' against the https server and verifies exit 101 for an OpenSSL build. The same case is match on windows in this run, and test4000 (--ech grease, which may fall back to TLS 1.2) is match on macos. Reproduce on macOS from the repository root: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <release>/tests/data $env:TEMP/raw.json 4001

Suggestion, copied from the finding:

Platform-specific (macOS). The likely cause is the harness, not Curl's ECH client. With an ecl: config list, HandBuiltTlsProvider.HandshakeAsync (Curl.Networking.UnitLibrary) offers TLS 1.3 only (settings.EchConfigs is not null, so runsTls12 is false; BL-1950), as curl's OpenSSL build does. The harness's HTTPS stand-in, Curl.Conformance.UnitLibrary/TlsServerStream.cs, is an SslStream server, and on macOS .NET's server-side SslStream negotiates at most TLS 1.2. The handshake therefore fails on the protocol version (exit 35) before the client can see that the server ignored the ECH offer and send ech_required (exit 101). Change Curl.Conformance.UnitLibrary so its HTTPS server speaks TLS 1.3 on macOS, as upstream's stunnel/OpenSSL server does: for example, serve TLS through Curl's hand-built TLS 1.3 server code (Curl.Tls.UnitLibrary) when the platform's SslStream server cannot. Add a unit test in Curl.Conformance.UnitTests that the macOS HTTPS stand-in completes a TLS 1.3-only ClientHello. Then rerun test4001 on macOS. If it still exits 35, check Tls13ClientHandshake's ech_required decision (TlsAlertDescription.EchRequired) against a server that ignores the ECH extension, and add a focused test in Curl.Tls.UnitTests (ADR-0482). Explained by ADR-0327 (ECH runs only on the hand-built client) and ADR-0359 (exit 101 text as measured with OpenSSL); neither explains the macOS-only exit 35.

## Acceptance criteria

- [ ] `behaviour:test4001`: Curl answers what curl 8.22.0 answers on macos, `upstream test4001 passes`, so the item measures `match`.
- [ ] The gap-platforms workflow run on the branch (or the CI job on macos) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-30 (lane 1): BL-2335's fix (ADR-0522) is in place. `TlsServerStream.AcceptAsync` with `PlatformServesTls13` false on macOS hands a TLS 1.3-only hello to `Tls13ServerStream`. Measured on Windows with the macOS path forced (`PlatformServesTls13 = false`, temporary, reverted): `CURL_UPSTREAM_CASE=4001`, the Manual test, passes (exit 101). With the SslStream fallback made to throw, it still passes, so Curl's real `--ech ecl:` hello is detected as TLS 1.3-only and served by the hand-built server. A new test, `Tls13ServerStreamTests.AcceptAsync_OpenSslProfileEchOfferedToHandBuiltServer_ClientAbortsWithEchRequired`, drives the server with the OpenSSL-profile hello that macOS sends (X25519MLKEM768 + X25519 shares, padding, compress_certificate, ECH). It passes too, so neither the server path nor the macOS client profile explains exit 35. It is left uncommitted for the next claim.
- macOS CI does not show the failure, because `UpstreamConformanceTests.NeedsTls13ServerCases` (BL-1949) keeps 4001 off the ratchet there, and a lane can neither run on macOS nor start gap-platforms. The cause is macOS-only and not reachable from Windows. Candidates: macOS's `GetECDsaPrivateKey`/signing of the PKCS#12-reloaded `test-localhost.pem`, or how the gap tool `Gap/Tools/Measure-UpstreamCases.cs` (an audit path a lane may not read) builds its runner.
- Interactive only. Remove lane: no once someone has run test4001 on macOS and recorded the failing step here: either `CURL_UPSTREAM_CASE=4001 CURL_UPSTREAM_KEEP_LOGS=1 dotnet test Curl.Conformance.UnitTests --filter TestCategory=Manual` on a Mac, or a gap-platforms run on a branch, with the case's `trace4001` / stderr (the curl error text and which server path answered).

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 45ea446b 5e325fc7 1fd5de8c 28346828.
- 2026-10-30: Doing -> Backlog. Interactive only: the exit 35 does not reproduce on Windows with the macOS server path or the OpenSSL-profile ECH hello; it needs a macOS run of test4001 (Manual test or gap-platforms) to find the failing step
