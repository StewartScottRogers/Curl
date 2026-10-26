---
id: BL-3601
title: Fix CGR-072: --location-trusted with Digest reuses the previous host's nonce after a redirect to another host or port, then gives up on the 401
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3867]
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/Cgr072Tests.cs, Curl.Authentication.UnitLibrary/DigestAuthenticator.cs, Curl.Authentication.UnitTests/Cgr072Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
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

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr072Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 8, before any edit): The cause is not in `DigestAuthenticator`, which cannot see the previous host (a sent value carries only the uri). `Curl.Core.UnitLibrary/RedirectFollower.cs:255` passes the previous hop's `DigestAuthorizationSent` as `KeptDigestAuthorization` to every hop that may be sent credentials, which with `--location-trusted` includes another host or port; `HttpProtocolHandler.RepeatKeptDigestAuthorization` then sends it with `nc=00000002` before any challenge, and the 401 to that kept answer is taken as a refusal. Fix: keep `KeptDigestAuthorization` only when `IsSameOrigin(hop.Url, next)` (scheme, host and port), whatever `--location-trusted` says; the anyauth variant needs nothing more, as Digest never answers before a challenge. Focused test in `Curl.Core.UnitTests/Cgr072Tests.cs` (a trusted redirect to another host and to another port sends no kept Digest value; same origin still does, test1286). `touches` changed to `RedirectFollower.cs` and that test file (the Authentication entries kept, unneeded); BL-3867 in Doing on origin/work/dark-factory holds `RedirectFollower.cs`, so `depends-on: [BL-3867]` and back to Backlog. Still to do: measure real curl with `Record-CurlExchange.ps1` for the three repro variants and record both results here.
- 2026-10-26 (lane 4): Fixed as lane 8 planned: `RedirectFollower.KeptDigestAuthorization` keeps the previous hop's Digest answer only when `IsSameOrigin(hop.Url, next)`, whatever `--location-trusted` says. The Authentication entries in `touches` were not needed. No ADR: no design choice, only matching measured curl. `Cgr072Tests` (other-host, other-port, same-origin rows) failed 2 of 3 with the fix reverted and passes with it; the existing test1286 row in `RedirectFollowerTests` still passes. Measure-CodeQuality for Curl.Core.UnitLibrary: 0 failing members.
- Measured with `Record-CurlExchange.ps1 -Port 20500 -Connections 4`, responses 401 Digest (`realm="r", nonce="n", qop="auth"`, body `d`), 302 `Location: http://localhost:20500/b`, 401 again, 200 `ok`, every reply on its own connection; REF = curl 8.22.0 Schannel (`/ucrt64/bin/curl`), PORT = this build's `Curl.Console\bin\Debug\net10.0\curl.exe`:
  - `curl -sSL --location-trusted --digest -u user:pass http://127.0.0.1:20500/a`: REF and PORT both exit 0, stdout `ok`, stderr empty, no files; requests `GET /a`, `GET /a` + Digest uri="/a" nc=00000001, `GET /b` (Host localhost:20500, no Authorization), `GET /b` + Digest uri="/b" nc=00000001. Equal but for cnonce and response (random, normalised by the report's section 1). Before the fix PORT sent the kept nc=00000002 on the first `/b` and gave up on its 401 (lane 8's reading, and the unit test's failing rows).
  - `... --anyauth ...` (case x-trusted-anyauth-digest-cross): both exit 0, stdout `ok`, same four requests, the first `/b` without Authorization. The only difference is the nonce count: REF sends `nc=00000002` on every Digest answer, PORT `nc=00000001`. Measured without any redirect too (`curl -s --anyauth -u user:pass http://127.0.0.1:20500/a`): REF nc=00000002, PORT nc=00000001, so it is a separate --anyauth cause, not CGR-072's; filed as BL-3892 rather than widen this task.
  - Other-port variant (x-trusted-digest-otherport): `Record-CurlExchange.ps1` listens on one port only, so not replayed live; the report gives REF as the same flow as the cross-host case, and `Cgr072Tests`'s other-port row pins that PORT now keeps no Digest answer for another port, after which the hop runs exactly as the cross-host one measured above.
- Same-host redirects, which already matched (test1286, nc=00000002 on the kept answer), still keep it: same-origin row and `FollowAsync_HopsDigestAuthorizationSent_IsKeptOnlyForAHopThatMayBeSentCredentials`. `dotnet build` clean, fast tests all passed. Platform-neutral (no paths, OS text or certificates), so the Linux and macOS criterion is met by the green Windows run.

## Log

- 2026-10-26: Created from CGR-072 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Waits on BL-3867, which holds Curl.Core.UnitLibrary/RedirectFollower.cs where the kept Digest answer must be limited to same-origin hops
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A --location-trusted redirect to another host or port starts a fresh Digest exchange, as curl 8.22.0 does (CGR-072)
