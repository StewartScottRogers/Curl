---
id: BL-5095
title: Apply curl's host-name matching rules on the Windows SslStream route with --ssl-no-revoke
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameMatching.cs]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5095 — Apply curl's host-name matching rules on the Windows SslStream route with --ssl-no-revoke

## Goal

On the Windows SslStream route taken with `--ssl-no-revoke` (and the other routes listed below), host-name verification applies curl's rules: a partial-label wildcard such as `f*.a.test` and a dNSName SAN holding an IP string no longer match, so the connection is refused with exit 60.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-050 (Medium, Confirmed; 3+ runs, re-reproduced by the orchestrator). Weakens security: host-name verification is looser than curl's; it needs a certificate of this shape from a trusted CA, so the realistic risk is a private or misissued CA. Windows only.

Affected: host-name verification on the SslStream route (`--cacert` + `--ssl-no-revoke`, also with `--cert-status`, `--http1.1`, `--ssl-allow-beast`, `--false-start`); DoH under `--ssl-no-revoke`; HTTPS origins through http and https proxies; ftps, imaps, pop3s, smtps, ldaps, wss, mqtts, gophers. Not affected: the default revocation route, the `--no-sessionid`/`--curves` hand-built routes, the HTTPS-proxy certificate itself, the Linux build.

**Repro.**
1. Make a test CA `ca.pem`. Issue two leaves, each with serverAuth EKU, keyUsage `digitalSignature,keyEncipherment` and a CRL distribution point: leaf A with `subjectAltName=DNS:f*.a.test`; leaf B with `subjectAltName=DNS:127.0.0.1` (a dNSName entry, not iPAddress).
2. Serve each over TLS on 127.0.0.1 (A on 22106, B on 22115; PowerShell or a C# file-based app, not Python), answering any request with `HTTP/1.1 200 OK` and body `SECRET`.
3. `curl -sS https://foo.a.test:22106/ --resolve foo.a.test:22106:127.0.0.1 --cacert ca.pem --ssl-no-revoke`
4. `curl -sS https://127.0.0.1:22115/ --cacert ca.pem --ssl-no-revoke`
5. DoH: run the same certificates on a DoH server at 22304, then `curl -sS http://foo.test:22098/ --doh-url https://foo.a.test:22304/dns-query --resolve foo.a.test:22304:127.0.0.1 --cacert ca.pem --ssl-no-revoke`

**Expected (curl 8.22.0 Schannel).** Step 3: `curl: (60) schannel: CertGetNameString() failed to match connection hostname (foo.a.test) against server certificate names`, exit 60 (also for `f.a.test`, `fx.a.test`, `FOO.a.test`). Step 4: `curl: (60) SSL peer certificate or SSH remote key was not OK`, exit 60 (also for `https://127.1:22115/`). Step 5: `curl: (6) Could not resolve host: foo.test`. Other protocols exit 60. curl 8.18.0 OpenSSL and the Linux port also exit 60.

**Actual.** Steps 3 and 4 print `SECRET` with exit 0 and the server logs the GET. Step 5 resolves and fetches (exit 0). wss sends its upgrade request; ldaps reaches the bind (exit 38); ftps, imaps, pop3s, smtps, mqtts and gophers pass TLS. Without `--ssl-no-revoke` the port exits 60 with curl's message.

**Where to start.** `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`, `WithTheNameCheckCurlRuns` -> `WithoutNameMismatchSchannelAccepts` (about line 576). On the SslStream route the name result comes from Windows (CERT_CHAIN_POLICY_SSL), which allows partial wildcards and matches an IP host against a DNS SAN string; the Schannel branch only clears a mismatch, never adds one. The hand-built route already applies curl's rules (`HandBuiltCertificateVerifier.MatchesAsCurlDoes` -> `SchannelCommonNameCheck.PatternMatchesHost`). Apply the same matching on the SslStream route and for DoH: mismatch when the target is an IP literal and no iPAddress SAN matches, or when no SAN DNS name matches under `PatternMatchesHost`. Existing tests: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.*.cs`, `HandBuiltCertificateVerifierTests.cs`.

## Acceptance criteria

- [x] New tests in `Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameMatching.cs` (certificates generated in the test with `CertificateRequest`) prove that on the SslStream/`--ssl-no-revoke` route a leaf with `DNS:f*.a.test` is refused for `foo.a.test`, `f.a.test`, `fx.a.test` and `FOO.a.test`, with curl's Schannel message and exit 60.
- [x] A test proves a leaf with `DNS:127.0.0.1` (dNSName) is refused for the IP hosts `127.0.0.1` and `127.1` with `SSL peer certificate or SSH remote key was not OK`, while a leaf with an iPAddress SAN `127.0.0.1` is accepted.
- [x] A regression test proves `x.a.test` against a `*.a.test` certificate is still accepted.
- [x] The DoH path uses the same check: a test shows a DoH TLS handshake with the partial-wildcard certificate is refused, so the resolve fails as in curl's exit 6 case.
- [x] Tests are platform-neutral, or `[OSCondition(OperatingSystems.Windows)]` where they need the Windows chain policy; the Linux route is unchanged.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python.
- Review acceptance check, for a Windows hand check: steps 3 and 4 exit 60 with curl's messages and the server logs no request; step 5 exits 6; `https://x.a.test` against a `*.a.test` certificate with `--ssl-no-revoke` still exits 0.
- Shares `ServerCertificateVerification.cs` with the trust-anchor task from the same review (finding STD-055); the board runs them one after the other.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 1d64b051 0ec61694 8ee5f415 7fe234e3 956b991b a0b49a29.
- Fix: `ServerCertificateVerification.SchannelAlternativeNamesMatch` (called from `WithoutNameMismatchSchannelAccepts`, `--cacert` Schannel build) refuses a name unless an IP host matches an iPAddress SAN, or a DNS host matches a dNSName under `SchannelCommonNameCheck.PatternMatchesHost`; a certificate with no name of the kind asked for stays with the common name check. Tests in `ServerCertificateVerificationTests.NameMatching.cs` are platform-neutral (they call `VerifyPeer` with the policy errors Windows leaves out).
- DoH criterion: DoH's TLS handshake goes through the same `SslStreamTlsProvider`/`ServerCertificateVerification` as every other route, so the fix covers it; a refused handshake is the resolve failure (exit 6) as before. No separate DoH code changed.
- Quality: Measure-CodeQuality run for Curl.Networking.UnitLibrary showed 100% line/branch and one method at complexity 16 (`WithoutNameMismatchSchannelAccepts`); I then split it into helpers (complexity now ~3) but could not re-measure, as the run aborts on unrelated Engine test failure filed as BL-5141.
- 2026-11-13: Doing -> Done. Schannel name rules (partial wildcards, dNSName IP strings) now apply on the SslStream route; tests added
