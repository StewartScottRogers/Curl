---
id: BL-3620
title: Fix CGR-091: Linux: -w %{certs} uses the x509asn1 (Schannel/GnuTLS) layout, not the OpenSSL layout
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/PeerCertificateText.cs, Curl.Output.UnitTests/Cgr091Tests.cs, Curl.Output.UnitLibrary/OpenSslPeerCertificateText.cs, Curl.Output.UnitLibrary/OpenSslDistinguishedNameText.cs, Curl.Output.UnitLibrary/OpenSslCertificateText.cs, Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Output.UnitTests/TransferWriteOutVariablesTests.cs, Curl.Output.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3620 — Fix CGR-091: Linux: -w %{certs} uses the x509asn1 (Schannel/GnuTLS) layout, not the OpenSSL layout

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-091 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-091: Linux: `-w %{certs}` uses the x509asn1 (Schannel/GnuTLS) layout, not the OpenSSL layout

- **Severity:** Medium (corrected from High: `%{certs}` is little used)
- **Area:** tls
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable (Linux only)
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls TLS-05`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- WSL `openssl s_server ... -WWW`, then run `curl -s -k -o /dev/null -w '%{certs}' https://localhost:<p>/index.txt`. Case: `V_L_certs_k`.

Commands (run each with REF and with PORT):

```sh
openssl s_server ... -WWW
curl -s -k -o /dev/null -w '%{certs}' https://localhost:<p>/index.txt
```

**Expected (real curl)**

- *REF-LINUX:* `Subject:CN = localhost`, `Serial Number:10da0d56...` (plain hex), the X509v3 extension lines, and dates after the extensions (`Start date:Oct 26 ...`). Then `rsa(n):9A93...` and `rsa(e):10001`.

**Actual (Curl)**

- *PORT-LINUX:* `Subject:CN=localhost`, `Serial Number:10:da:0d:...:85:`, `Start Date:2026-10-26 04:24:58 GMT` before the key, no extension lines, `RSA Public Key:2048`, `rsa(n):9a:93:...` and `rsa(e):0x10001`. Windows matches the Schannel REF.

**Suspected location in source**

- `Curl.Output.UnitLibrary\PeerCertificateText.cs`, a port of x509asn1.c with no OpenSSL variant.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr091Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Measured 2026-10-26 in WSL (Ubuntu) with curl 8.18.0 / OpenSSL 3.5.5, the only OpenSSL build available here (no 8.22.0 OpenSSL build on this machine; the 8.22.0 Windows build is Schannel, whose layout is unchanged). `ossl_certchain` in curl 8.22.0's `lib/vtls/openssl.c` makes the same records as 8.18.0's. Two certificates: `openssl req -x509 -newkey rsa:2048 -subj /CN=localhost -addext subjectAltName=DNS:localhost,IP:127.0.0.1 -addext keyUsage=critical,digitalSignature,keyEncipherment -addext extendedKeyUsage=serverAuth,clientAuth`, and a CA-signed leaf (serial 0x0080, `authorityKeyIdentifier=keyid,issuer:always`, IPv6/e-mail/URI SAN, custom `1.2.3.4=ASN1:UTF8String:hi there`, the other key usages, `codeSigning,emailProtection,timeStamping,OCSPSigning,1.2.3.5`). Server `openssl s_server -accept <p> -cert <n>.crt -key <n>.key -WWW -quiet`, command `curl -s -k -o /dev/null -w '%{certs}' https://localhost:<p>/index.txt`. REF records are pinned verbatim in `Cgr091Tests.cs` (`SelfSignedRecords`, `CaSignedRecords`): `Subject:CN = localhost`, plain lower-case serial, extensions after `Public Key Algorithm`, `Start date:Oct 26 20:13:19 2026 GMT` after them, `RSA Public Key:2048`, `rsa(n):AE27...` upper-case, `rsa(e):10001`, `Signature:a4:a1:...:01:`, PEM.
- PORT on Linux: published `Curl.Console` self-contained for linux-x64 and ran the same command in WSL against the same servers. Both certificates: REF exit 0, PORT exit 0; stdout byte-identical (`cmp`), stderr identical (empty), and the body file from `-o` without `-w` identical. Request bytes: this fix changes only the `-w` output, not the request; the TLS client hello differs by design and is not compared. Before the fix PORT printed the x509asn1 layout (the issue's Actual).
- Windows (the variant that already matched): `TlsBackend.Schannel` still prints the x509asn1.c layout; `TransferWriteOutVariablesTests.TryGetVariableText_CertificatesOfLoopbackHttpsTransfer_MatchCurl` now pins it with `TlsBackend = Schannel` explicitly and passes, and `Format_Schannel_PrintsTheX509Asn1Records` checks `FormatFor(Schannel)` equals the old `Format`.
- Fail before: `Cgr091Tests` calls `PeerCertificateText.FormatFor(der, TlsBackend.OpenSsl)`; before the fix the only printer was `Format`, whose output starts `Subject:CN=localhost` and has `Start Date:` before the key, so the pinned records cannot match.
- Decision (ADR-0825): `%{certs}` follows `PlatformTlsBackend.ForProcess` through a new `TransferWriteOutVariables.TlsBackend` init property, as the `-v` handshake lines do (ADR-0085). The new overload is named `FormatFor`, not `Format`, because a second `Format` made existing `<see cref="PeerCertificateText.Format"/>` crefs in other projects ambiguous.
- `touches` widened (no other Doing task names these files on origin/work/dark-factory): the new `OpenSslPeerCertificateText.cs`; `OpenSslDistinguishedNameText.cs` (gained the `XN_FLAG_ONELINE` style: `, `, ` = `, bytes above 0x7F as `\XX`); `OpenSslCertificateText.cs` (signature names and `FormatTime` made internal for reuse); `TransferWriteOutVariables.cs` and its tests; the library's `CLAUDE.md`, which described `%{certs}` as x509asn1 only.
- Ported printers: key identifiers, basic constraints, key usage, extended key usage, subject/issuer alternative names; any other extension prints its raw bytes with dots (OpenSSL's fallback). The rest of OpenSSL's printers and the DSA/DH key records are BL-3875; `CURLINFO_CERTINFO` still in the x509asn1 layout is BL-3876. Records for the crafted certificates in the branch tests (othername, X400, EDI, RID, invalid IP, BMP DirName, negative serial, odd RSA keys, unprintable names) follow OpenSSL 3's source, not a measurement.
- Platform check: the tests use in-memory certificates and fixed dates only, no paths or OS text; `FormatFor` takes the backend explicitly, so they give the same answer on Windows, Linux and macOS. CI on Linux/macOS is met by that and the fast tests green here.
- Gates: `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` first found three members (`FormatExtension` complexity 32, `FormatAuthorityKeyIdentifier` 14, `Format` branches 75%); fixed with a printer table, a helper and a test, the second run reports 0 failing members. `dotnet build` clean; fast tests all green (every test project passed).

## Log

- 2026-10-26: Created from CGR-091 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. -w %{certs} prints curl's OpenSSL-build records (ossl_certchain layout) off Windows, byte-identical to curl 8.18.0/OpenSSL 3.5.5 on Linux
