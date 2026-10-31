---
id: BL-4379
title: Re-close GF-0092: An --ech ecl: handshake the server does not accept exits 35 instead of 101 (ECH required) on macOS (on macos)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
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

## Log

- 2026-10-30: Created.
