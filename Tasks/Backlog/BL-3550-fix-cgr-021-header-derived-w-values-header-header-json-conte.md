---
id: BL-3550
title: Fix CGR-021: Header-derived -w values (%header{}, %{header_json}, %{content_type}, %header{location}) re-encode header bytes Latin-1 to UTF-8, corrupt...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Output.UnitLibrary/WriteOutJson.cs, Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs, Curl.Output.UnitTests/Cgr021Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3550 — Fix CGR-021: Header-derived -w values (%header{}, %{header_json}, %{content_type}, %header{location}) re-encode header bytes Latin-1 to UTF-8, corrupt...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-021 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-021: Header-derived `-w` values (`%header{}`, `%{header_json}`, `%{content_type}`, `%header{location}`) re-encode header bytes Latin-1 to UTF-8, corrupting bytes 0x80 and above (UTF-8 values come out double-encoded)

- **Severity:** High
- **Area:** writeout-verbose, http-responses
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [writeout-verbose WV-03] Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `writeout-verbose WV-03`, `http-responses HR-7`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- **[writeout-verbose WV-03]** Header-derived -w values are re-encoded Latin-1 to UTF-8, corrupting bytes 0x80 and above
  - `resp/hibit.txt` contains `X-Hi: caf\xc3\xa9 \xff\xfe end` and `Content-Type: text/\xe9`.
    - `curl -s -o NUL -w '%{content_type}|%header{x-hi}|%{header_json}\n' http://127.0.0.1:20700/raw/hibit`
- **[http-responses HR-7]** -w %header{}, %{header_json} and %{content_type} re-encode header bytes as UTF-8, double-encoding UTF-8 values
  - `m_hdr_w_high.bin` = `HTTP/1.1 200 OK\r\nX-A: caf\xe9 \xc3\xa9\r\nContent-Type: t/\xe9\r\nContent-Length: 2\r\n\r\nok`.
    Command: `curl -s -S -o NUL -w "[%header{x-a}][%{content_type}]" http://127.0.0.1:20400/m_hdr_w_high`
  - `m_hdr_json_utf8v` (`X-A: caf\xc3\xa9`), run with `-w %{header_json}`.
  - `m_hdr_utf8_name` (header name `X-\xc3\xa9`), run with `-w %{header_json}`.

Commands (run each with REF and with PORT):

```sh
# [writeout-verbose WV-03]
curl -s -o NUL -w '%{content_type}|%header{x-hi}|%{header_json}\n' http://127.0.0.1:20700/raw/hibit
# [http-responses HR-7]
curl -s -S -o NUL -w "[%header{x-a}][%{content_type}]" http://127.0.0.1:20400/m_hdr_w_high
```

**Expected (real curl)**

- **[writeout-verbose WV-03]** Writes the raw bytes: `text/\xe9|caf\xc3\xa9 \xff\xfe end`. `%{header_json}` and `%{json}` carry the same raw bytes.
- **[http-responses HR-7]** writes the raw bytes: `[caf\xe9 \xc3\xa9][t/\xe9]`. header_json gives `"caf\xc3\xa9"`, and the name gives `"x-\xc3\xa9"`.

**Actual (Curl)**

- **[writeout-verbose WV-03]** `text/\xc3\xa9|caf\xc3\x83\xc2\xa9 \xc3\xbf\xc3\xbe end`. Every byte at or above 0x80 is UTF-8 encoded, so UTF-8 header values come out double-encoded (mojibake). This also affects `%header{location}`. `-v` and `-D` output match REF.
- **[http-responses HR-7]** treats the bytes as Latin-1 and writes them as UTF-8: `[caf\xc3\xa9 \xc3\x83\xc2\xa9][t/\xc3\xa9]`. header_json gives `"caf\xc3\x83\xc2\xa9"` (shown as `cafÃ©`), and the name is mangled to `x-\xc3\xa3\xc2\xa9`. Same on Linux.

**Suspected location in source**

- **[writeout-verbose WV-03]** `Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs:101`, where `Encode()` uses `Encoding.UTF8.GetBytes` on text whose header values were decoded as Latin-1.
- **[http-responses HR-7]** `Curl.Output.UnitLibrary\TransferWriteOutVariables.cs` / `WriteOutJson.cs`. Head bytes are held as Latin-1 strings (`HttpResponseHeadBuilder`) and encoded as UTF-8 on output.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr021Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-021 of the third-party gap report.
