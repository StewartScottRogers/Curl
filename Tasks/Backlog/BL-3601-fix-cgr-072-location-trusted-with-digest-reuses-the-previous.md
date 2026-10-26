---
id: BL-3601
title: Fix CGR-072: --location-trusted with Digest reuses the previous host's nonce after a redirect to another host or port, then gives up on the 401
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/DigestAuthenticator.cs, Curl.Authentication.UnitTests/Cgr072Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3601 — Fix CGR-072: --location-trusted with Digest reuses the previous host's nonce after a redirect to another host or port, then gives up on the 401

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-072 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-072: --location-trusted with Digest reuses the previous host's nonce after a redirect to another host or port, then gives up on the 401

- **Severity:** Medium (lowered from High: it needs --location-trusted, Digest and a redirect to another host or port together)
- **Area:** http-stateful
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux)
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-stateful HS-3`

**Reproduction**

- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- Routes: `/a` returns 401 `WWW-Authenticate: Digest realm="r", nonce="n", qop="auth"`, then `302 Location: http://localhost:20500/b`. `/b` returns the same 401, then 200 `ok`.
- Command: `curl -sSL --location-trusted --digest -u user:pass http://127.0.0.1:20500/a`.
- Cases: `EV\cases\x-trusted-digest-cross-min`, `x-trusted-digest-otherport` (Location on port 20501), `x-trusted-anyauth-digest-cross`.

Commands (run each with REF and with PORT):

```sh
curl -sSL --location-trusted --digest -u user:pass http://127.0.0.1:20500/a
```

**Expected (real curl)**

- the first request to the new host has no Authorization header. After the 401 it sends Digest with nc=00000001 and gets the 200. Stdout `ok`, exit 0.

**Actual (Curl)**

- The first request to the new host already carries `Authorization: Digest ... nonce="n", uri="/b", nc=00000002`, built from the old host's challenge.
- It never answers the 401 that follows. Stdout is the 401 body `d`, exit 0.
- On the same host PORT matches REF.

**Suspected location in source**

- `Curl.Authentication.UnitLibrary/DigestAuthenticator.cs:92` (`RepeatAuthorization`). The Digest state carries over to the new host instead of being reset.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr072Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-072 of the third-party gap report.
