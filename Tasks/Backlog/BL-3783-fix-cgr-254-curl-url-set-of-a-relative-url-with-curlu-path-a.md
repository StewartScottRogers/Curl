---
id: BL-3783
title: Fix CGR-254: curl_url_set of a relative URL with CURLU_PATH_AS_IS keeps the dot segments; libcurl still resolves them
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/UrlPartSetter.cs, Curl.Engine.UnitTests/Cgr254Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3783 — Fix CGR-254: curl_url_set of a relative URL with CURLU_PATH_AS_IS keeps the dot segments; libcurl still resolves them

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-254 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-254: curl_url_set of a relative URL with CURLU_PATH_AS_IS keeps the dot segments; libcurl still resolves them

- **Severity:** Low
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-21`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe repro` (curl_url section), or `probe.exe url` for all the REL lines. It calls `curl_url_set(u, CURLUPART_URL, "http://a/b/c/d", 0)`, then `curl_url_set(u, CURLUPART_URL, "../g/./h", CURLU_PATH_AS_IS)`, then `curl_url_get(CURLUPART_URL)`.

Commands (run each with REF and with PORT):

```sh
probe.exe repro
probe.exe url
```

**Expected (real curl)**

- `http://a/b/g/h`.

**Actual (Curl)**

- `http://a/b/c/../g/./h`. The Linux run has 104 such REL differences. Transfers that follow redirects with PATH_AS_IS match REF.

**Suspected location in source**

- `Curl.Engine.UnitLibrary` (ICurlUrlHandle relative resolution honours PathAsIs).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr254Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-254 of the third-party gap report.
