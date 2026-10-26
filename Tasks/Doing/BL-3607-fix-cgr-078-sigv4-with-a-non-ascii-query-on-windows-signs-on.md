---
id: BL-3607
title: Fix CGR-078: SigV4 with a non-ASCII query on Windows signs one byte form and sends another
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3840]
touches: [Curl.Authentication.UnitLibrary/AwsSigV4Request.cs, Curl.Authentication.UnitLibrary/AwsSigV4UriEncoding.cs, Curl.Authentication.UnitTests/Cgr078Tests.cs, Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3607 — Fix CGR-078: SigV4 with a non-ASCII query on Windows signs one byte form and sends another

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-078 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-078: SigV4 with a non-ASCII query on Windows signs one byte form and sends another

- **Severity:** Medium
- **Area:** http-stateful
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-stateful HS-17`

**Reproduction**

- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- `curl -sS -g --aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRET -H 'X-Amz-Date: 20250101T000000Z' 'http://127.0.0.1:20500/p?k=ä'`, and the same with the path `/ü`. Cases: `EV\cases\svq-k__-396`, `svp-__-299`.

Commands (run each with REF and with PORT):

```sh
curl -sS -g --aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRET -H 'X-Amz-Date: 20250101T000000Z' 'http://127.0.0.1:20500/p?k=ä'
```

**Expected (real curl)**

- the request line is `GET /p?k=\xE4` (an ANSI byte) with `Signature=251a1c08...`. Path case: `GET /%FC` with `Signature=3ff4f5d1...`.

**Actual (Curl)**

- The request line is `GET /p?k=\xC3\xA4` (UTF-8), yet the signature is the same `Signature=251a1c08...`. So the signature covers different bytes from the ones sent.
- Path case: `GET /%C3%BC` with `Signature=8c86e160...`.

**Notes**

- *Note:* the argv code-page part overlaps the url-parsing area.

**Suspected location in source**

- `Curl.Authentication.UnitLibrary/AwsSigV4Request.cs` / `AwsSigV4UriEncoding.cs`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr078Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 4, before any edit): the wire half of both repros is BL-3840's work (HTTP percent-encodes the path, and sends the query bytes, in `HttpRequestOptions.CommandLineTextEncoding`: `/ü` -> `/%FC`, `?k=ä` -> raw `0xE4`). The signer already signs the query in the argument encoding (`AwsSigV4Signer` passes `argumentEncoding` to `CanonicalQuery`), which is why the query signature already matches real curl's `251a1c08...`; only the bytes sent differ, so the query case is fixed entirely by BL-3840. The path case also needs the signed path to follow: `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs` `SigningRequestOf` sets `Path = UrlPathEscapes.WithUpperCaseHexDigits(request.Url.AbsolutePath)`, and `System.Uri` escapes `ü` as UTF-8 `%C3%BC`, so `s3` (which signs the path as given) signs `/%C3%BC` (`8c86e160...`) instead of `/%FC` (`3ff4f5d1...`). Plan for the next claim: once BL-3840 gives `HttpUrlText.Path` an `Encoding`, build the signed path from the same function with the command-line text encoding (the signer's `argumentEncoding`), so the signed path is the path on the request line; add `AwsSigV4HttpAuthenticator.cs` to touches (added; no task in Doing held it) and pin both repros in `Cgr078Tests.cs` with a cp1252 encoding. Requeued on BL-3840.

## Log

- 2026-10-26: Created from CGR-078 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Waits on BL-3840: the request line must carry the path and query in the command-line encoding before the signed path can follow it
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
