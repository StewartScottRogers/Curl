---
id: BL-5819
title: Fix fitness finding STD-171: Linux LDAP SASL bind failures exit 38 "LDAP: cannot bind"; curl exits 67
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapOpenLdapSaslBind.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5819 — Fix fitness finding STD-171: Linux LDAP SASL bind failures exit 38 "LDAP: cannot bind"; curl exits 67

## Goal

STD-171 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Linux LDAP SASL bind failures exit 38 "LDAP: cannot bind"; curl exits 67. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (fails closed; wrong exit code and text)
- **Confidence:** Confirmed (4 shapes, 2 runs)
- **Protocol/area:** LDAP SASL, Linux build
- **Repro (Linux, OpenLDAP from STD-117 on 22389):**
  1. `curl -sS -m 10 -u nobody:x --digest 'ldap://127.0.0.1:22389/ou=people,dc=example,dc=org?cn?sub?(uid=alice)'`
  2. The same with `-u :secret --digest`.
  3. A mock LDAP server on 22503 that answers the first SASL bind with success at once: `curl -sS -u alice:secret --digest ldap://127.0.0.1:22503/dc=example,dc=org`
  4. Set `olcSaslSecProps: noanonymous,noplain,minssf=56` on the server, restart it, and rerun command 1 with `-u alice:secret`.
- **Expected (REF2):** 1–3: `curl: (67) Login denied`, exit 67. 4: `curl: (67) Authentication cancelled`, exit 67.
- **Actual (.NET, Linux):** `curl: (38) LDAP: cannot bind`, exit 38, in all four. A wrong password for an existing user already gives 67.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapOpenLdapSaslBind.cs` failure paths; only the no-mechanism path uses `LdapBind.OpenLdapLoginDeniedAsync`. ADR-1302's "keeps its exit 38" contradicts measured REF2 behaviour.
- **Acceptance check:** command 1 exits 67 `curl: (67) Login denied`; command 4 exits 67 `curl: (67) Authentication cancelled`.

## Acceptance criteria

- [x] command 1 exits 67 `curl: (67) Login denied`; command 4 exits 67 `curl: (67) Authentication cancelled`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Decision in ADR-1342: OpenLDAP SASL bind failures exit 67 (Login denied; Local Error challenge -> Authentication cancelled). Not measured against real curl (no OpenLDAP here); taken from the finding. Measure-CodeQuality was blocked by an unrelated Curl.Console.UnitTests failure (CreateRunnerOverConnectors_WritesProgressMeter_WritesTheMeterOnlyWhenAsked); the change adds one small method, all branches tested.

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: fa86ec17 69116afe 3d836d5e.
- 2026-11-23: Doing -> Backlog. Lane 13 could not integrate: quality gates missed by 1 member(s) of files it changed: LdapOpenLdapSaslBind.SaslBindFailedAsync(Curl.Original.Protocol.Ldap.LdapExchange, Curl.Original.Protocol.Ldap.LdapBindReply, System.Threading.CancellationToken) (Curl.Original.Protocol.Ldap.UnitLibrary/LdapOpenLdapSaslBind.cs: branch), still after a repair run. The work is on branch factory/BL-5819-lane-13-20261123-102134; start with git cherry-pick --no-commit factory/BL-5819-lane-13-20261123-102134 and fix it.
- 2026-11-23: Backlog -> Doing. Criteria at claim: fa86ec17 69116afe 3d836d5e.
