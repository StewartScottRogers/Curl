---
id: BL-3818
title: Fix CGR-289: Windows ldaps://: PORT uses one connection with SNI localhost and honours --cacert; curl's WinLDAP makes a second connection with the mac...
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4026, BL-4054, BL-4055, BL-4056]
touches: [Curl.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Protocol.Ldap.UnitLibrary/WinLdapResultText.cs, Curl.Protocol.Ldap.UnitTests/Cgr289Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3818 — Fix CGR-289: Windows ldaps://: PORT uses one connection with SNI localhost and honours --cacert; curl's WinLDAP makes a second connection with the mac...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-289 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-289: Windows `ldaps://`: PORT uses one connection with SNI `localhost` and honours `--cacert`; curl's WinLDAP makes a second connection with the machine name as SNI and uses the system store

- **Severity:** Low
- **Area:** tls-protocols-revocation
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `tls-protocols-revocation TPR-12`

**Reproduction**

- *Setup (tls-protocols-revocation):* E = `SCR\evidence\tls-protocols-revocation`; TP = `SCR\evidence\tls\pki` (srv.crt/srv.key/srv.pfx password `x`, ca.crt for CN localhost). Server `E\srv\out\tsrv.exe <smtp|imap|pop3|ftp|ldap|http|proxy|crl> <port> pfx=<pfx> [tls=implicit|starttls|none] [tls12=1] [strict=1] [updir=<dir>] [mode=reneg|nonotify|closedelim] [www=<dir>]` (Linux `E\srv\outlinux\tsrv`; it logs every client message, `Cs:` = inside TLS). Also `openssl s_server`. Revocation PKI `E\pki` (`rca.crt`; `ocg*` good, `ocr*` revoked with staples `certs\<tag>.ocsp`; `rev*` with CDP on port 21990). Harness `E\h.ps1`, `E\linux\lh.sh`; verifier `E\v.ps1`, `E\linux\lv.sh`.
- `E\srv\out\tsrv.exe ldap 21900 pfx=TP\srv.pfx` (implicit TLS), then `curl -sS -k ldaps://localhost:21900/dc=test`, and `curl -sS --cacert TP\ca.crt --ssl-no-revoke ldaps://localhost:21900/dc=test`. Verifier cases: `v_ldaps_k`, `v_ldaps_ca`.

Commands (run each with REF and with PORT):

```sh
E\srv\out\tsrv.exe ldap 21900 pfx=TP\srv.pfx
curl -sS -k ldaps://localhost:21900/dc=test
curl -sS --cacert TP\ca.crt --ssl-no-revoke ldaps://localhost:21900/dc=test
```

**Expected (real curl)**

- `-k`: one TLS connection with SNI `localhost` that is closed, then a second TLS connection with SNI = the machine host name, carrying 3 RootDSE searches, an NTLM bind and an unbind. It exits 38 `curl: (38) LDAP local: bind via ldap_win_bind Protocol Error`. With `--cacert`, the second handshake fails (system store, host-name SNI), and it exits 38 `curl: (38) LDAP local: bind via ldap_win_bind Server Down`.

**Actual (Curl)**

- A single TLS connection with SNI `localhost`. It sends the 3 searches and the NTLM bind twice (8 messages and an unbind; the NTLM flags also differ, `B7B208E2` vs `07B208A2`), then exits 38 `curl: (38) LDAP local: bind via ldap_win_bind Local Error` in both cases. With `--cacert` it trusts a server that WinLDAP does not. The exit codes match.

**Suspected location in source**

- `Curl.Protocol.Ldap.UnitLibrary\LdapProtocolHandler.cs` and `WinLdapResultText.cs`. The emulated WinLDAP path does not reproduce WinLDAP's own TLS connection or trust store.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr289Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

Notes (2026-10-29 lane 2, closing): Criteria ticked from BL-4044, BL-4055 and BL-4056. -k: both curl 8.22.0 and Curl make 2 TLS connections, send the same 3 searches and sicilyNegotiate (flags 07 b2 08 a2) and exit 38 Unavailable when the server stops; with a full server both bind once, search, unbind and exit 0 (BL-4055). Trusted --cacert --ssl-no-revoke: both exit 38 `LDAP local: bind via ldap_win_bind Server Down`, 0 request bytes (BL-4056, ADR-0994). Non-issuing --cacert: both exit 60 with the same stderr (BL-4044). Focused tests in Cgr289Tests.cs fail before each fix and pass after. Build clean; fast tests 0 failed.

## Log

- 2026-10-26: Created from CGR-289 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

Notes (2026-10-28 lane 8, requeue): The fix needs ConnectTarget to carry an SNI host and a system-store-only trust flag (neither exists; ConnectTarget lives in Curl.Protocol.Abstractions, TLS in Curl.Networking). Filed BL-4026 for that. Then this task must: in LdapProtocolHandler's WinLdap ldaps path, close the first connection and open a second with SNI = Environment.MachineName and system-store trust (so --cacert gives Server Down, -k gives the Protocol Error after 3 RootDSE searches, an NTLM bind and an unbind), and match the NTLM flags B7B208E2 and the single bind (WinLdapLogonBind). Measure real curl with Record-CurlExchange.ps1 first. Add Curl.Networking and Curl.Protocol.Abstractions files from BL-4026 to this task's touches only if they still need changes.
- 2026-10-28: Doing -> Backlog. Needs BL-4026 (ConnectTarget SNI host and system-store-only trust in Abstractions and Networking)
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Code for the ldaps second connection landed; real-curl measurement and NTLM flags left to BL-4044

Notes (2026-10-28 lane 4): Code done: LdapProtocolHandler.WinLdapTargetOf gives the WinLDAP second ldaps connection TlsServerName = Environment.MachineName and TrustsSystemStoreOnly; Cgr289Tests.cs pins it; Ldap fast tests pass (638); Measure-CodeQuality for the library reports 0 failing members. Not done: real-curl measurement, NTLM flags and single bind; filed BL-4044. Once BL-4044 is Done, tick the criteria here from its Notes.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Backlog. Code and -k measurement done (BL-4054, BL-4055); the --cacert variant is BL-4056's work, which closes this task
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Done. ldaps:// -k and trusted --cacert through Curl match curl 8.22.0's WinLDAP exit, stderr and request bytes
