---
id: BL-3756
title: Fix CGR-227: Linux: the certificate-mismatch message for an IPv4-mapped IPv6 address keeps the brackets
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/Cgr227Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3756 — Fix CGR-227: Linux: the certificate-mismatch message for an IPv4-mapped IPv6 address keeps the brackets

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-227 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-227: Linux: the certificate-mismatch message for an IPv4-mapped IPv6 address keeps the brackets

- **Severity:** Low
- **Area:** tls
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable (Linux only)
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls TLS-15`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- WSL `stsrv.py http <p> <pki> implicit` (SAN DNS:localhost, IP:127.0.0.1, IP:::1), then run `curl -sS --cacert ca.crt 'https://[::ffff:127.0.0.1]:<p>/'`. Case: `V_L_mapped`.

Commands (run each with REF and with PORT):

```sh
curl -sS --cacert ca.crt 'https://[::ffff:127.0.0.1]:<p>/'
```

**Expected (real curl)**

- *REF-LINUX:* `curl: (60) SSL: no alternative certificate subject name matches target ipv6 address '::ffff:127.0.0.1'`

**Actual (Curl)**

- *PORT-LINUX:* `curl: (60) SSL: no alternative certificate subject name matches target ipv6 address '[::ffff:127.0.0.1]'`

**Suspected location in source**

- `Curl.Networking.UnitLibrary\TlsFailureMessages.cs:337`. It adds brackets to every IPv6 target.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr227Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-227 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
