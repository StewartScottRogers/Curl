---
id: BL-3640
title: Fix CGR-111: --capath / SSL_CERT_DIR trust every certificate file in the directory; OpenSSL REF only uses subject-hash-named (c_rehash) files
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/Cgr111Tests.cs]
requirement: none
created: 2026-10-26
completed:
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

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr111Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-111 of the third-party gap report.
