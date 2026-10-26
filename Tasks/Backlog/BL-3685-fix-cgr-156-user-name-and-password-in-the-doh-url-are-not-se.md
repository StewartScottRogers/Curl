---
id: BL-3685
title: Fix CGR-156: User name and password in the --doh-url are not sent (no Authorization: Basic on DoH requests)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohDnsResolver.RequestEncoding.cs, Curl.Networking.UnitTests/Cgr156Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3685 — Fix CGR-156: User name and password in the --doh-url are not sent (no Authorization: Basic on DoH requests)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-156 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-156: User name and password in the --doh-url are not sent (no Authorization: Basic on DoH requests)

- **Severity:** Medium
- **Area:** doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `doh-ipfs-resolution DOH-5`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- `srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1`, then `curl -sS --doh-url http://u:p@127.0.0.1:21800/dns-query http://foo.test:21802/x`. Linux: `https://u:p@127.0.0.1:21801/dns-query` with `--doh-insecure`. Cases: `cases\v_f5_userinfo.txt`, `linux\cases\vl_f5_userinfo.txt`.

Commands (run each with REF and with PORT):

```sh
srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1
curl -sS --doh-url http://u:p@127.0.0.1:21800/dns-query http://foo.test:21802/x
```

**Expected (real curl)**

- both DoH POSTs carry `Authorization: Basic dTpw`.

**Actual (Curl)**

- no Authorization header is sent, so a DoH server that requires authentication would reject the queries.

**Suspected location in source**

- `Curl.Networking.UnitLibrary/DohDnsResolver.RequestEncoding.cs:29-38` (BuildRequestHead writes only Host/Accept/Content-Type/Content-Length).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr156Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-156 of the third-party gap report.
