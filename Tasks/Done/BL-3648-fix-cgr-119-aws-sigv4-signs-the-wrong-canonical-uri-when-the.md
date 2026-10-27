---
id: BL-3648
title: Fix CGR-119: --aws-sigv4 signs the wrong canonical URI when the URL path holds raw non-ASCII bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/AwsSigV4Signer.cs, Curl.Authentication.UnitLibrary/AwsSigV4UriEncoding.cs, Curl.Authentication.UnitTests/Cgr119Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3648 — Fix CGR-119: --aws-sigv4 signs the wrong canonical URI when the URL path holds raw non-ASCII bytes

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-119 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-119: --aws-sigv4 signs the wrong canonical URI when the URL path holds raw non-ASCII bytes

- **Severity:** Medium (was High: it needs raw non-ASCII bytes in the URL path)
- **Area:** upstream-cases
- **Platform(s):** Linux. Detail: Linux verified. On Windows, UC-5 hides the problem.
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `upstream-cases UC-4`

**Reproduction**

- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- `verify\v2.sh`, server `verify\conf\ok.json`.
- Run ``curl -s "http://fake.fake.fake:8000/472/a=$(printf '\xe3\x81\x82')" -u user:secret --aws-sigv4 aws:amz:us-east-2:es --connect-to fake.fake.fake:8000:127.0.0.1:21353``.
- `verify\sigv4.py` recomputes each Signature from the recorded X-Amz-Date and tries candidate canonical URIs.

Commands (run each with REF and with PORT):

```sh
curl -s "http://fake.fake.fake:8000/472/a=$(printf '\xe3\x81\x82')" -u user:secret --aws-sigv4 aws:amz:us-east-2:es --connect-to fake.fake.fake:8000:127.0.0.1:21353
```

**Expected (real curl)**

- Request line `GET /472/a=%E3%81%82`. The Signature matches canonical URI `/472/a%3D%25E3%2581%2582` (encoded twice). This agrees with upstream 8.22.0 test472's expected signature, so the result does not depend on the curl version.

**Actual (Curl)**

- Same request line, but the Signature matches canonical URI `/472/a%3D%E3%81%82` (encoded once), so the signature differs from curl's. A pre-encoded ASCII URL gives the same signature on both.

**Suspected location in source**

- `Curl.Authentication.UnitLibrary\AwsSigV4Signer.cs:92` -> `AwsSigV4UriEncoding.CanonicalPath` (`AwsSigV4UriEncoding.cs:26`).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr119Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 4): Already fixed at 5425545d by BL-3607's b32dab50 (CGR-078). That commit made `AwsSigV4UriEncoding.CanonicalPath` start from the path as the request line sends it (each byte above `~`, in the argument encoding, as `%XX`) and only then percent-encode it again. On Linux the argument encoding is UTF-8 (`InvalidUtf8ByteEscapingEncoding`), so `/472/a=あ` is signed as `/472/a%3D%25E3%2581%2582`. This run changed no production code and made no decision that needs an ADR.
- Repro (WSL Ubuntu): real curl 8.18.0 OpenSSL against a self-contained linux-x64 publish of Curl at 5425545d, each against an `nc -l` canned `200 OK` (`Content-Length: 3`, body `ok\n`). Command: `curl -s "http://fake.fake.fake:8000/472/a=$(printf '\xe3\x81\x82')" -u user:secret --aws-sigv4 aws:amz:us-east-2:es --connect-to fake.fake.fake:8000:127.0.0.1:<port>`, run once with `-H "X-Amz-Date: 19700101T000000Z"` added so the signatures can be compared, and once as the issue gives it.
  - With the date pinned, both: exit 0, stdout `ok`, stderr empty, no files written, request line `GET /472/a=%E3%81%82 HTTP/1.1`, `Authorization: AWS4-HMAC-SHA256 Credential=user/19700101/us-east-2/es/aws4_request, SignedHeaders=host;x-amz-date, Signature=b8783c8387a5249b084642126fe1f8e07e12a2847820fd5b6cd64b2047149da4`. That is upstream test472's signature. The request bytes are identical apart from `User-Agent` (curl/8.18.0 against curl/8.22.0, normalised as the report's section 1 says).
  - As the issue gives it, both: exit 0, stdout `ok`, stderr empty, same request line and header order. The signatures differ only because each run signed a different `X-Amz-Date` second.
- Focused test `Curl.Authentication.UnitTests/Cgr119Tests.cs`: the raw `あ` path under UTF-8 fails on the code from before b32dab50 (canonical URI encoded once) and passes now. The pre-encoded variant, which already matched, passes on both.
- Linux and macOS CI: the test is platform-neutral (it passes the encoding explicitly, with no paths and no OS calls). `dotnet build` is clean (0 warnings) and the fast tests pass on Windows (46 of 46 test projects). No production code changed, so no Measure-CodeQuality run was needed.

## Log

- 2026-10-26: Created from CGR-119 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. --aws-sigv4 signs a raw non-ASCII URL path as curl does on Linux (test472 signature), pinned by Cgr119Tests
