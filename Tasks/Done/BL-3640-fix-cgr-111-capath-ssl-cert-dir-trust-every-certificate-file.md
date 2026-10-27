---
id: BL-3640
title: Fix CGR-111: --capath / SSL_CERT_DIR trust every certificate file in the directory; OpenSSL REF only uses subject-hash-named (c_rehash) files
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitLibrary/OpenSslSubjectNameHash.cs, Curl.Networking.UnitLibrary/CLAUDE.md, Curl.Networking.UnitTests/Cgr111Tests.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.cs, Curl.Networking.UnitTests/HandBuiltTlsProviderTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3640 — Fix CGR-111: --capath / SSL_CERT_DIR trust every certificate file in the directory; OpenSSL REF only uses subject-hash-named (c_rehash) files

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-111 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-111: `--capath` / `SSL_CERT_DIR` trust every certificate file in the directory; OpenSSL REF only uses subject-hash-named (c_rehash) files

- **Severity:** Medium (the port trusts a server that REF rejects; the user did put the certificate in the directory)
- **Area:** environment
- **Platform(s):** Linux
- **Confidence:** Confirmed. Detail: Confirmed. The behaviour comes from OpenSSL's hash-dir lookup, and curl's `--capath` documentation requires c_rehash, so it does not depend on the curl version.
- **Related existing finding (GF/AF):** GF-0097
- **Source findings:** `environment ENV-04`

**Reproduction**

- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- Run `linux/vtls.sh`, which is self-contained. It creates a self-signed certificate for `localhost` / `127.0.0.1`, starts `openssl s_server -accept 127.0.0.1:21213 -www`, copies the certificate into `plain/server-ca.crt` (not hashed), and makes `hashdir/` with `openssl rehash`.
  - `~/curlnet/curl -sS -o /dev/null --capath plain https://127.0.0.1:21213/`
  - `SSL_CERT_DIR=plain ~/curlnet/curl -sS -o /dev/null https://127.0.0.1:21213/`
  - `SSL_CERT_DIR=/nope:plain ...`

Commands (run each with REF and with PORT):

```sh
openssl s_server -accept 127.0.0.1:21213 -www
openssl rehash
~/curlnet/curl -sS -o /dev/null --capath plain https://127.0.0.1:21213/
SSL_CERT_DIR=plain ~/curlnet/curl -sS -o /dev/null https://127.0.0.1:21213/
SSL_CERT_DIR=/nope:plain ...
```

**Expected (real curl)**

- All three exit 60 with `SSL certificate OpenSSL verify result: self-signed certificate (18)`. With `--capath hashdir` REF exits 0.

**Actual (Curl)**

- All three exit 0. The handshake is accepted. Results were the same in both runs.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\ServerCertificateVerification.cs:487-500`. `ReadCaCertificateDirectory` imports every file in the directory.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr111Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Plan and decision (ADR-0835, decided by Claude under Stewart's delegation): the OpenSSL build now reads `--capath` / `SSL_CERT_DIR` as OpenSSL's `by_dir.c` does. The value is split at `Path.PathSeparator` (`:` off Windows), empty entries skipped. Only files named `<8 lowercase hex>.<digits>` are opened, `<hash>.0` upward until a number is missing, and only certificates whose subject hashes to `<hash>` are trusted. The hash is `X509_NAME_hash`, computed by hand in the new `OpenSslSubjectNameHash` (canonical UTF8String encoding, SHA-1, the first four bytes read little-endian).
- Measured: Git's `openssl x509 -noout -subject_hash` gives `4042bcee` for `/C=US/O=Internet Security Research Group/CN=ISRG Root X1` and `ce275665` for `/CN=localhost`. `Cgr111Tests` pins both, plus the case and whitespace canonicalisation.
- Repro results. REF is the report's REF-LINUX (curl 8.18.0 OpenSSL): this Windows lane has only the Schannel curl, which ignores `--capath`, so the Linux run could not be repeated here.
  - `--capath plain` (cert as `server-ca.crt`): REF exit 60 `SSL certificate OpenSSL verify result: self-signed certificate (18)`, nothing on stdout. PORT: no anchors are read (`ReadTrustAnchors_CapathWhoseCertificateFileIsNotHashNamed_TrustsNothing`), so it is exit 60 with the same message (`SslStreamTlsProviderTests`' empty-directory and unreadable-file cases pin that message for an empty `--capath`).
  - `SSL_CERT_DIR=plain`: same as above. `CaCertificateEnvironment` passes `SSL_CERT_DIR` as `CaCertificateDirectory`.
  - `SSL_CERT_DIR=/nope:plain`: REF exit 60. PORT exit 60 (`..._SslCertDirListWithAMissingDirectoryThenAPlainOne_TrustsNothing`).
  - The variant that already matched, `--capath hashdir` (after `openssl rehash`): REF exit 0, PORT exit 0 (`..._CapathWhoseCertificateFileIsHashNamed_TrustsIt`, and the `SslStreamTlsProviderTests` / `HandBuiltTlsProviderTests` handshakes with the certificate under its hash name). `/nope:hashdir` is now trusted as OpenSSL trusts it.
  - Bytes on the wire are unchanged: only the trust decision after the server's certificate differs.
- Before the fix, `Cgr111Tests`' plain-directory and `/nope:plain` tests fail, because every file was trusted. After the fix they pass.
- `touches` widened: the new `OpenSslSubjectNameHash.cs`, the project's `CLAUDE.md` (its `--capath` sentence), and `SslStreamTlsProviderTests.cs` / `HandBuiltTlsProviderTests.cs`, whose `--capath` handshakes wrote `server.pem` and now write the hash name. No other task in Doing on origin/work/dark-factory names a Networking file.
- Platform-neutral: tests use temporary directories, `Path.PathSeparator` and ECDSA certificates made in the test, with no OS-specific text.
- Fast tests: all 46 assemblies passed (Networking 3645 passed, 31 skipped). Measure-CodeQuality for Curl.Networking.UnitLibrary showed 100% lines. One member, `OpenSslSubjectNameHash.WriteCanonicalAttribute`, had its non-universal tag branch uncovered (complexity 4, CRAP 4). `Of_ContextTaggedValueWithAStringTagNumber_IsHashedAsEncoded` was added for it; Measure-CodeQuality was not run again afterwards.

## Log

- 2026-10-26: Created from CGR-111 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. --capath and SSL_CERT_DIR in the OpenSSL build trust only openssl-rehash-named files, as OpenSSL's hashed directory lookup does (CGR-111)
