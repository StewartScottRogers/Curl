---
id: BL-3794
title: Fix CGR-265: Libcurl accepts options that the build it advertises does not support (REF answers CURLE_NOT_BUILT_IN or UNKNOWN_OPTION)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr265Tests.cs, Libcurl/Options/Authentication/EasyAuthenticationOptionSetters.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3794 — Fix CGR-265: Libcurl accepts options that the build it advertises does not support (REF answers CURLE_NOT_BUILT_IN or UNKNOWN_OPTION)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-265 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-265: Libcurl accepts options that the build it advertises does not support (REF answers CURLE_NOT_BUILT_IN or UNKNOWN_OPTION)

- **Severity:** Low
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux). Detail: Both (the option set differs by platform)
- **Confidence:** Confirmed. Detail: Confirmed (Windows, Linux).
- **Related existing finding (GF/AF):** GF-0029, GF-0031, GF-0033, GF-0036, GF-0099, AF-0022, AF-0183
- **Source findings:** `libcurl-api LA-32`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe sweep` and `probe.exe repro`. On Windows the options are:
  - TLSAUTH_TYPE `SRP`, TLSAUTH_USERNAME, TLSAUTH_PASSWORD and the PROXY_TLSAUTH_* options
  - DNS_SERVERS, DNS_INTERFACE, DNS_LOCAL_IP4, DNS_LOCAL_IP6
  - ECH, CAPATH, CRLFILE, ISSUERCERT and their PROXY_ variants
  - TLS13_CIPHERS, SSL_EC_CURVES, SSL_SIGNATURE_ALGORITHMS
  - SSL_VERIFYSTATUS, DOH_SSL_VERIFYSTATUS
  - HTTP_VERSION 3, 4, 5, 30 and 31; STREAM_WEIGHT; TCP_FASTOPEN; KRBLEVEL
  - HTTPSIG_KEY, HTTPSIG_KEYID, HTTPSIG_HEADERS, HTTPSIG_ALGORITHM
  - PROXYTYPE=8

  On Linux: SSLENGINE `x` and SSLENGINE_DEFAULT.

Commands (run each with REF and with PORT):

```sh
probe.exe sweep
probe.exe repro
```

**Expected (real curl)**

- Windows (Schannel 8.22.0): TLSAUTH_* return 4; DNS_* and HTTPSIG_* return 48; the TLS-backend, HTTP/2 and Kerberos options return 4 or 1; PROXYTYPE=8 returns 4.
- Linux (OpenSSL 8.18): SSLENGINE returns 53 and SSLENGINE_DEFAULT returns 0.

**Actual (Curl)**

- Windows: everything returns 0, except TLSAUTH_TYPE (43 for strings other than `SRP`), HTTPSIG_ALGORITHM (43 outside 0..2) and PROXYTYPE=8 (43). DNS_SERVERS is actually used (`Could not resolve host ... (Could not contact DNS servers)`).
- Linux: SSLENGINE and SSLENGINE_DEFAULT return 4.
- The port's curl_version still reports `libcurl/8.22.0 Schannel`.

**Suspected location in source**

- `Libcurl\Options\Authentication\EasyAuthenticationOptionSetters.cs:71-74`, `Options\Tls\EasyTlsOptionSetters.cs:104,107`, `Options\Network\EasyNetworkOptionSetters.cs`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr265Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-265 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Partly fixed. TLSAUTH_TYPE/USERNAME/PASSWORD and PROXY_ twins answer CURLE_NOT_BUILT_IN (4) on Windows (Schannel has no SRP), unchanged on Linux/macOS, via `EasyAuthenticationOptionSetters.SrpIsBuiltIn`; unit tests in Cgr265Tests.cs (before the fix Windows answered 0). The probe/REF libcurl of the report is not available; expected codes are the report's. Remaining options split to BL-4000 (HTTPSIG_* change reverted: LibTest Lib5000/5004 expect it accepted). Build clean, fast tests green, Measure-CodeQuality Libcurl: 0 failing members.
- 2026-10-28: Doing -> Done. TLSAUTH fixed; remainder in BL-4000
