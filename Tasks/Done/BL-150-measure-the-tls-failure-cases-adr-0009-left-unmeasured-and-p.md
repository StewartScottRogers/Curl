---
id: BL-150
title: Measure the TLS failure cases ADR-0009 left unmeasured and pin them in SslStreamTlsProvider
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-064]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-150 — Measure the TLS failure cases ADR-0009 left unmeasured and pin them in SslStreamTlsProvider

## Goal

Every TLS failure case listed below is measured against the reference curl builds. For each one, `TlsFailureMessages` and its tests either change to the measured text, or keep BL-064's default with the measurement written down.

## Context

- ADR-0009 (`Documentation/Planning/Decisions/ADR-0009-tls-behaviour-matches-the-platforms-usual-curl-build.md`) decided which build to match: Schannel-build messages on Windows and OpenSSL-build messages elsewhere. BL-064 implemented it in `Curl.Networking.UnitLibrary` (`TlsFailureMessages.cs`, `SslStreamTlsProvider.cs`, `TlsClientOptions.cs`, with tests in `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs`, `SslStreamTlsProviderTests.cs` and `TlsClientOptionsTests.cs`). Where the ADR had no measurement, BL-064 chose a default. These are the cases to measure, each with the default BL-064 took:
  1. **Server closes mid-handshake (exit 35).** Schannel default: `schannel: failed to receive handshake, SSL/TLS connection failed`. This is curl source text and has not been measured. OpenSSL default: `TLS connect error: <innermost .NET exception message>`. That is a stand-in, because .NET raises the EOF before OpenSSL sees it.
  2. **Name mismatch against a certificate with subjectAltName entries (exit 60).** The ADR measured only a CN-only certificate. BL-064 uses the ADR's CN form `SSL: certificate subject name '<CN>' does not match target hostname '<host>'` for every certificate. The OpenSSL build probably prints `SSL: no alternative certificate subject name matches target hostname '<host>'` for SAN certificates, or `ipv4 address` / `ipv6 address` in place of `hostname` when the target is an IP literal.
  3. **Schannel name mismatch without `--cacert` (exit 60).** BL-064 uses the ADR's `schannel: CertFindExtension() returned no extension.`. Native Schannel probably reports SEC_E_WRONG_PRINCIPAL (0x80090322) here instead.
  4. **`--cacert` naming a directory, OpenSSL build.** Default: exit 77 `error adding trust anchors from file: <path>`.
  5. **Whether `--cacert` and `--capath` replace the default trust store or add to it.** ADR-0009 section 4 left this to BL-064. BL-064 decided: `--cacert` replaces the store, `--capath` alone adds beside the system store, and both together trust the union of the cacert and capath certificates.
  6. **OpenSSL verify errors beyond the measured 18.** BL-064 maps an untrusted chain ending in a self-signed root to 19, an incomplete chain to 20, and certificate validity failures to 9 (not yet valid) and 10 (expired).
  7. **macOS.** SslStream there does not use OpenSSL, so exit 35 has no OpenSSL error string and falls back as in case 1. The CA bundle path the OpenSSL build uses on macOS has not been measured (ADR-0009 section 4).
  8. **Linux strings were measured on curl 8.18.0, not 8.21.0.** Re-measure the ADR's OpenSSL-build strings on 8.21.0.
- Reference builds: curl 8.21.0 Schannel on Windows, and an OpenSSL build of curl 8.21.0 on Linux. For macOS, record what was or was not measured. Run each case with `-sS` and capture standard error byte for byte.
- **Measuring needs the reference curl binaries and a TLS test server**, for example `openssl s_server` with purpose-made certificates (CN-only, SAN with DNS names, SAN with IP addresses, self-signed, missing intermediate, expired, not yet valid). If the running agent does not have them, do not guess. Move this task to `Blocked` with the reason "needs Stewart to provide curl 8.21.0 Schannel and OpenSSL reference binaries and a TLS test server (openssl s_server) to measure against".
- Do not edit ADR-0009. If a measurement contradicts one of its decisions, file a new ADR task for Stewart rather than changing this one's scope.
- Out of scope: the exit 60 help block (BL-149, `Curl.Console`) and printing `SslStreamTlsProvider.Warnings` (BL-072).

## Acceptance criteria

- [x] Case 1: named tests in `Curl.Networking.UnitTests` pin the measured Schannel and OpenSSL exit 35 text for a server that closes mid-handshake, or Notes record the measurement and why the default stands.
- [x] Case 2: a named test pins the OpenSSL-build exit 60 message for a SAN certificate whose names do not match, one for each of hostname, IPv4 and IPv6 targets, or Notes record why the default stands.
- [x] Case 3: a named test pins the Schannel-build exit 60 message for a name mismatch without `--cacert`, or Notes record why the default stands.
- [x] Case 4: a named test pins the OpenSSL-build exit code and message for `--cacert <directory>`.
- [x] Case 5: named tests pin the measured trust-store behaviour for `--cacert` alone, `--capath` alone, and both together, in each build.
- [x] Case 6: named tests pin the OpenSSL verify-result text for self-signed-root (19), incomplete chain (20), not yet valid (9) and expired (10), each matching the measured bytes.
- [x] Case 7: Notes record what was measured on macOS (exit 35 text, CA bundle path), or that it was not measured and why.
- [x] Case 8: the OpenSSL-build strings BL-064 took from the 8.18.0 measurements are checked against 8.21.0. Named tests pin any that changed, and Notes say which were confirmed.
- [x] Notes state, for every case, the curl version and build it was measured against and the exact command line used.
- [x] ADR-0009 is unchanged (`git diff master -- Documentation/Planning/Decisions/ADR-0009-tls-behaviour-matches-the-platforms-usual-curl-build.md` is empty).
- [x] No new test carries `TestCategory=Integration` or opens a socket.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes, including 100% line and branch coverage of `Curl.Networking.UnitLibrary`.

## Notes

- Filed as a follow-up to BL-064.

### Reference builds and setup (measured 2026-09-27)

- **Schannel:** curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel, `/mingw64/bin/curl`, the ADR-0009/ADR-0018 reference. System32 `curl.exe` 8.21.0 Schannel was spot-checked and printed the same lines.
- **OpenSSL:** curl 8.21.0 (x86_64-pc-linux-musl) libcurl/8.21.0 OpenSSL/3.5.7, the `curlimages/curl:8.21.0` Docker image on Docker Desktop's Linux VM, run as `docker run --rm -v <dir>:/w -w /w curlimages/curl:8.21.0 -sS -o /dev/null ... --connect-to ::host.docker.internal:<port> https://<host>:<port>/`. The `--connect-to` prefix is left out of the command lines below.
- **Servers:** `openssl s_server -accept <port> -cert <c>.pem -key <c>.key [-cert_chain <chain>.pem] -www -quiet` (OpenSSL 3.5.7) on Windows. Certificates were made with `openssl req`/`x509`: `root.pem` (CA "BL150 Root"), `inter.pem` (intermediate), leaves signed by the root: `cnonly` (CN=localhost, no SAN), `sandns` (SAN DNS:foo.test, DNS:bar.test), `sanip` (SAN IP:10.9.9.9, IP:fd00::9), `expired` (2020-2021), `future` (2040-2041); `chained` (signed by the intermediate); `self` (self-signed CN=localhost, no SAN). Public servers: `*.badssl.com`, `example.com`, and `140.82.112.4` (github.com).
- Every line below is the first stderr line with `-sS`; exit 60 lines are followed by the help block, as before.
- No design decision needed an ADR: every change reproduces a measured line, and nothing contradicts ADR-0009 (it left these cases to be measured). The documents are outside this task's `touches`, so the measurements are recorded here.

### Case 1 - server closes mid-handshake (exit 35)

A Python listener that reads the ClientHello and closes (`close.py 19410 read`).
- Schannel `curl -sS https://localhost:19410/` -> `curl: (35) schannel: failed to receive handshake, SSL/TLS connection failed`. BL-064's default is confirmed.
- OpenSSL `curl -sS https://localhost:19410/` -> `curl: (35) TLS connect error: error:0A000126:SSL routines::unexpected eof while reading`. **Changed:** `OpenSslSslConnectError` now prints this OpenSSL string when the innermost exception is a bare `IOException`. That is how `SslStream` reports end of stream during the handshake (checked: `IOException: Received an unexpected EOF or 0 bytes from the transport stream.`, no inner exception). Any other exception still prints its own message.
- Pinned by `SslStreamTlsProviderTests.AuthenticateAsClientAsync_WhenTheServerClosesMidHandshake_ReportsTheMeasuredLine` (both builds, a real handshake over the in-memory pair), `TlsFailureMessagesTests.OpenSslSslConnectError_WhenTheServerClosesMidHandshake_IsTheMeasuredUnexpectedEofLine` and `SchannelSslConnectError_WhenTheServerClosesMidHandshake_IsTheMeasuredHandshakeNotReceivedLine`.
- A listener that closes without reading (Windows then sends a reset) printed `curl: (35) Recv failure: Connection was aborted` in the Schannel build. That is not this case; it is filed as BL-369.

### Case 2 - OpenSSL name mismatch (exit 60)

`--cacert root.pem` in every case:
- `sandns` `https://localhost:19402/` -> `SSL: no alternative certificate subject name matches target hostname 'localhost'`
- `sanip` `https://10.1.2.3:19403/` -> `... matches target ipv4 address '10.1.2.3'`
- `sanip` `https://[fd00::1]:19403/` -> `... matches target ipv6 address '[fd00::1]'`
- `sandns` `https://10.1.2.3:19402/` -> `... target ipv4 address '10.1.2.3'`; `sanip` `https://localhost:19403/` -> `... target hostname 'localhost'`. The alternative-name form is used whenever the SAN holds DNS or IP entries, and the kind of target names it.
- `cnonly` `https://10.1.2.3:19401/`, `https://[fd00::1]:19401/`, `https://otherhost:19401/` -> `SSL: certificate subject name 'localhost' does not match target hostname '10.1.2.3'` / `'[fd00::1]'` / `'otherhost'`. The ADR's CN form always says "hostname", and IPv6 is bracketed.
- `curl -sS https://wrong.host.badssl.com/` -> `SSL: no alternative certificate subject name matches target hostname 'wrong.host.badssl.com'`; `curl -sS https://140.82.112.4/` -> `... target ipv4 address '140.82.112.4'`.
- **Precedence (measured):** the OpenSSL build checks the name before the chain. `self` untrusted at `https://other:19407/` -> `SSL: certificate subject name 'localhost' does not match target hostname 'other'`, and `expired` at `https://other:19405/` with `--cacert root.pem` -> the name line, not verify result 10.
- **Changed:** `OpenSslPeerFailedVerification` reports any name mismatch first, uses the alternative-name form when the SAN holds a DNS name or IP address (the CN form otherwise, including a SAN holding only an e-mail address, which follows curl's source), names the target kind, and brackets IPv6. Pinned by `TlsFailureMessagesTests.OpenSslPeerFailedVerification_WithANameMismatchAgainst{Dns,Ip,No,OnlyAnEmail}AlternativeNames_*`, `..._WithChainErrorsAndANameMismatch_ReportsTheNameMismatch`, and end to end by `SslStreamTlsProviderTests.AuthenticateAsClientAsync_WithUntrustedCertificateNotNamingTheTargetHost_ReportsWhatTheBuildChecksFirst` and the two `..._ReportsNoAlternativeNameMatches` tests. The test server certificate has a DNS SAN, so those two tests changed from the CN form.

### Case 3 - Schannel name mismatch (exit 60)

- Without `--cacert`: `curl -sS https://wrong.host.badssl.com/` and `curl -sS https://140.82.112.4/` -> `curl: (60) schannel: SNI or certificate check failed: SEC_E_WRONG_PRINCIPAL (0x80090322) - The target principal name is incorrect.` **Changed** from BL-064's CertFindExtension default.
- With `--cacert` (plus `--ssl-no-revoke` for root-signed leaves, see BL-368), curl checks the name itself:
  - IP target, no SAN extension: `cnonly` `https://127.0.0.1:19401/`, and `--cacert self.pem` at `https://127.0.0.1:19407/` and `https://[::1]:19407/` -> `schannel: CertFindExtension() returned no extension.` (ADR-0009's measured line; confirmed).
  - IP target, SAN present: `sandns` `https://127.0.0.1:19402/`, `sanip` `https://[::1]:19403/` -> `SSL peer certificate or SSH remote key was not OK` (libcurl's generic exit 60 text).
  - Host-name target: `cnonly` `--resolve other:19401:127.0.0.1 https://other:19401/`, `sandns` `https://localhost:19402/`, `--cacert self.pem ... https://other:19407/` -> `schannel: CertGetNameString() failed to match connection hostname (<host>) against server certificate names`.
- Precedence: the Schannel build reports the chain first. `self` at `https://other:19407/` with no `--cacert` -> SEC_E_UNTRUSTED_ROOT; with `--cacert root.pem` -> `schannel: the certificate or certificate chain is based on an untrusted root`.
- **Changed:** `SchannelPeerFailedVerification` takes the chain and target host and returns the lines above. Pinned by `TlsFailureMessagesTests.SchannelPeerFailedVerification_WithOnlyANameMismatch*` (four tests) and `SslStreamTlsProviderTests.AuthenticateAsClientAsync_WithCaCertificateFileButAnotherHostNameInTheSchannelBuild_ReportsTheHostNameCertGetNameStringDidNotMatch`.

### Case 4 - OpenSSL `--cacert <directory>`

`curl -sS --cacert cadir.pem https://example.com/` and the same against the local server -> `curl: (77) error adding trust anchors from file: cadir.pem`. BL-064's default is confirmed and already pinned by `SslStreamTlsProviderTests.AuthenticateAsClientAsync_WithCaCertificateFileNamingADirectoryInTheOpenSslBuild_ReportsNoTrustAnchorsAdded`. For the Schannel build, `curl: (77) schannel: failed to open CA file 'cadir.pem'` is re-confirmed.

### Case 5 - does `--cacert` / `--capath` replace or add?

`caproot/` holds `root.pem`, `caself/` holds `self.pem`, each under its `openssl x509 -hash` name `.0`; `capempty/` is empty.
- OpenSSL: `--cacert root.pem https://example.com/` -> 60 verify result 20 (**replaces** the system bundle); `--cacert root.pem` local -> 0. `--capath caproot` against example.com -> 0 and against local -> 0 (**adds** beside the bundle; `-v` shows `CAfile: /etc/ssl/certs/ca-certificates.crt`, `CApath: caproot`). `--capath capempty` against example.com -> 0. `--cacert root.pem --capath caself`: local root-signed -> 0, `self` -> 0, example.com -> 60 verify result 20 (**the union of the two, without the system store**). All three match BL-064's decision.
- Schannel: `--cacert root.pem https://example.com/` -> 60 `schannel: the certificate chain is incomplete` (replaces the store; the text is BL-368's). `--capath caproot` local -> 60 SEC_E_UNTRUSTED_ROOT and against example.com -> 0 (ignored). `--cacert root.pem --capath caself` against `self` -> 60 `schannel: the certificate or certificate chain is based on an untrusted root`, and against local -> 0 with `--ssl-no-revoke` (the file alone; the directory is ignored).
- Pinned: OpenSSL by the existing `..._WithCaCertificateFileHoldingAnUnrelatedAuthorityInTheOpenSslBuild_ReportsVerifyResult18`, `..._WithCaCertificateDirectoryHoldingTheServerCertificateInTheOpenSslBuild_Succeeds`, `..._WithCaCertificateDirectoryThatAddsNothingInTheOpenSslBuild_VerifiesAgainstTheSystemStoreAlone`, `..._WithCaCertificateDirectoryAndAnUnrelatedCaCertificateFileInTheOpenSslBuild_TrustsBoth`. Schannel by `..._WithCaCertificateFileHoldingAnUnrelatedAuthorityInTheSchannelBuild_ReportsAnUntrustedRoot`, `..._WithCaCertificateDirectoryHoldingTheServerCertificateInTheSchannelBuild_IgnoresTheDirectory`, and the new `..._WithCaCertificateDirectoryAndAnUnrelatedCaCertificateFileInTheSchannelBuild_TrustsTheFileAlone`.

### Case 6 - OpenSSL verify results

- `chained` sent with `inter.pem` and `root.pem` (`-cert_chain interroot.pem`), no `--cacert`: `self-signed certificate in certificate chain (19)`; `curl -sS https://untrusted-root.badssl.com/` -> the same.
- `chained` without its intermediate, `--cacert root.pem`: `unable to get local issuer certificate (20)`; also `incomplete-chain.badssl.com`, and any untrusted leaf whose issuer was not sent.
- `future` with `--cacert root.pem`: `certificate is not yet valid (9)`. `expired` with `--cacert root.pem`: `certificate has expired (10)`; `expired.badssl.com` -> the same.
- An expired or not-yet-valid leaf that is also untrusted -> 20: trust is reported before validity, as BL-064 has it.
- `self`: `self-signed certificate (18)`. All match BL-064's text, already pinned by `TlsFailureMessagesTests.OpenSslPeerFailedVerification_*ReportsVerifyResult{9,10,19,20}`.

### Case 7 - macOS

Not measured: no macOS host is available to this lane, and Docker runs Linux only. The exit 35 text and the OpenSSL build's CA bundle path on macOS stay unmeasured. The code keeps BL-064's behaviour (case 1's fallback, now the EOF string for a clean close; `SslStream`'s default trust on macOS).

### Case 8 - ADR-0009's 8.18.0 OpenSSL strings on 8.21.0

All confirmed byte for byte on 8.21.0: 18 (`self`), the CN mismatch form, `--cacert bad.pem` / `empty.pem` 77 `error adding trust anchors from file: <path>`, `--tlsv1.3 -k` against `-tls1_2` -> `TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version`, `-k --tls-max 1.2 --ciphers ECDHE-RSA-AES256-GCM-SHA384` against `-cipher AES128-SHA` -> `error:0A000410:SSL routines::ssl/tls alert handshake failure`, `--ciphers NOSUCHCIPHER` 59, `--tls13-ciphers NOSUCH` 59 `failed setting TLS 1.3 cipher suite: NOSUCH`, `--cert self.p12` and `--cert nonexist.pem` 58 lines. BL-065's 43/58 strings were also re-checked and all match: `--key nonexist.key`, `--key self.pem`, `--cert-type DER` with empty or PEM file, `--cert-type P12` missing/non-PKCS#12/wrong password, `--cert-type FOO|ENG|PROV`, `--key-type FOO|ENG|PROV|P12`. None changed, so no test changed for this case.

### Gates

- `dotnet build Curl.Networking.UnitTests -warnaserror`: clean. `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"`: 580 passed, 6 skipped (platform-specific), 0 failed.
- Coverage (cobertura from the MSTest collector): `TlsFailureMessages` 100% line and branch. `SslStreamTlsProvider` is unchanged apart from one call. Its remaining gap, the `OperatingSystem.IsWindows()` branch in `CreateCipherSuitesPolicy` taken only on Linux, was there before, as were the integration-only `TcpDialer` and `UdpDatagramChannel` and a branch in `TcpConnector.ConnectThroughProxyAsync`. This task added no uncovered line or branch.
- New tests use in-memory handshakes or built chains only: no `Integration` category, no socket.
- Follow-ups filed: BL-368 (Schannel text for expired, incomplete and revocation-unknown chains, and the CN fallback) and BL-369 (reset mid-handshake).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Lane handed over mid-run while the factory's restart logic was fixed; the run had only just resumed and left no work.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. TLS failure text measured on curl 8.21.0 Schannel and OpenSSL; OpenSSL EOF and SAN/IP name-mismatch lines, Schannel WRONG_PRINCIPAL and CertGetNameString lines now pinned
