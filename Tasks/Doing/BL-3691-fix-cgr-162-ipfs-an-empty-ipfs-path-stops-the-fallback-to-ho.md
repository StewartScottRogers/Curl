---
id: BL-3691
title: Fix CGR-162: IPFS: an empty IPFS_PATH stops the fallback to $HOME/.ipfs/gateway
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/IpfsGatewayRewriter.cs, Curl.Core.UnitTests/Cgr162Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3691 — Fix CGR-162: IPFS: an empty IPFS_PATH stops the fallback to $HOME/.ipfs/gateway

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-162 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-162: IPFS: an empty IPFS_PATH stops the fallback to $HOME/.ipfs/gateway

- **Severity:** Medium
- **Area:** doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `doh-ipfs-resolution DOH-11`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- HOME and USERPROFILE point to a folder whose `.ipfs\gateway` contains `http://127.0.0.1:21802` (`evidence\doh-ipfs-resolution\homes\gw`). Set env `IPFS_PATH=` (empty), run `srv web=21802`, then `curl -sS ipfs://bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi/f`. With IPFS_PATH unset both binaries behave the same (`v_f11_ipfs_path_unset`). Cases: `cases\v_f11_*.txt`, `linux\cases\vl_f11_ipfs_path_empty.txt`.

Commands (run each with REF and with PORT):

```sh
srv web=21802
curl -sS ipfs://bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3oclgtqy55fbzdi/f
```

**Expected (real curl)**

- exits 0, reads `$HOME/.ipfs/gateway` and fetches `GET /ipfs/<cid>/f`.

**Actual (Curl)**

- exits 37 with `curl: IPFS automatic gateway detection failed`.

**Suspected location in source**

- `Curl.Core.UnitLibrary/IpfsGatewayRewriter.cs:247` (an empty value is not treated as unset).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr162Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-162 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
