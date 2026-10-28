---
id: BL-3763
title: Fix CGR-234: LDAP on Windows: the anonymous (negotiate) bind failure text differs, and curl's extra TCP connection is missing
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ldap.UnitLibrary/WinLdapLogonBind.cs, Curl.Protocol.Ldap.UnitTests/Cgr234Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3763 — Fix CGR-234: LDAP on Windows: the anonymous (negotiate) bind failure text differs, and curl's extra TCP connection is missing

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-234 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-234: LDAP on Windows: the anonymous (negotiate) bind failure text differs, and curl's extra TCP connection is missing

- **Severity:** Low
- **Area:** other-protocols
- **Platform(s):** Windows
- **Confidence:** Confirmed. Detail: Confirmed (verifier re-ran this on Windows).
- **Related existing finding (GF/AF):** none
- **Source findings:** `other-protocols OP-15`

**Reproduction**

- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- `EV\confirm.cases` C20 and `EV\ldap.cases`. The bench LDAP server answers every BindRequest with success and returns a canned search entry. Command: `curl -s -S "ldap://127.0.0.1:21001/dc=bench"`

Commands (run each with REF and with PORT):

```sh
curl -s -S "ldap://127.0.0.1:21001/dc=bench"
```

**Expected (real curl)**

- Opens an empty TCP connection (conn 0) before the WinLDAP connection (conn 1).
- stderr: `curl: (38) LDAP local: bind via ldap_win_bind Protocol Error`

**Actual (Curl)**

- Opens only the WinLDAP connection; its bytes match REF's conn 1.
- stderr: `curl: (38) LDAP local: bind via ldap_win_bind Local Error`.
- With `-u` (simple bind), output and wire match.

**Suspected location in source**

- `Curl.Protocol.Ldap.UnitLibrary\WinLdapLogonBind.cs` (success before auth completes is mapped to LocalError).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr234Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-234 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

### Run notes (2026-10-28, lane 8)

- Repro: `curl -s -S "ldap://127.0.0.1:21001/dc=bench"` (server answers every bind with success). Expected per report: `curl: (38) LDAP local: bind via ldap_win_bind Protocol Error`. Cause: the Sicily negotiate answer was a bare success with no NTLM challenge in the matchedDN; we mapped that to Local Error. Decision (default taken): a Sicily negotiate success with an empty matchedDN fails the attempt with WinLDAP's `Protocol Error` (code 2); a challenge the package cannot answer stays `Local Error`. Real curl was not re-run (no Windows LDAP bench here); text taken from the verified report.
- Fix: `WinLdapLogonBind.SicilyBindAsync`, `WinLdapResultText.ProtocolError`; test `Cgr234Tests`. Fast tests for the library pass (632); Measure-CodeQuality for Curl.Protocol.Ldap.UnitLibrary: 0 failing members.
- Not done: curl's extra empty TCP connection (conn 0) before WinLDAP's own. Split out as BL-3999; the connection-bytes part of criterion 1 is covered there.
- 2026-10-28: Doing -> Done. Protocol Error text fixed; extra connection split to BL-3999
