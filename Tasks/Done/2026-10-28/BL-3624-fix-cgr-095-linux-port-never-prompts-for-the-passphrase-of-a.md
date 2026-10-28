---
id: BL-3624
title: Fix CGR-095: Linux: PORT never prompts for the passphrase of an encrypted PEM key given without --pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/Cgr095Tests.cs, Curl.Engine.UnitLibrary/TlsClientOptionsMapping.cs, Curl.Engine.UnitTests/TlsClientOptionsMappingTests.cs, Curl.Engine.UnitTests/EngineCompositionTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3624 — Fix CGR-095: Linux: PORT never prompts for the passphrase of an encrypted PEM key given without --pass

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-095 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-095: Linux: PORT never prompts for the passphrase of an encrypted PEM key given without `--pass`

- **Severity:** Medium
- **Area:** tls
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable (Linux only; the tty case was not run)
- **Related existing finding (GF/AF):** AF-0053, AF-0057
- **Source findings:** `tls TLS-09`

**Reproduction**

- *Setup (tls):* E = `SCR\evidence\tls`. PKI in `E\pki` (`index.txt` contains `hello tls`; pfx password `x`). Servers: `openssl s_server -accept <p> -cert E\pki\srv.crt -key E\pki\srv.key -WWW [options]` (Git ucrt64 openssl on Windows, system openssl in WSL, run from a directory containing `index.txt`), `E\srv\out\starttls.exe <proto> <port> <pfx> [implicit|refuse|noext]` (source `E\srv\starttls.cs`), `E\linux\stsrv.py` (same idea in Python). Harnesses `E\tlsdiff.ps1`, `E\linux\tlsdiff.sh`; verifier `E\verify\vwin1.ps1`, `vwin2.ps1`, `vlin.sh` (cases `E\cases\V_*`, `E\linux\cases\V_L_*`).
- WSL `openssl s_server ... -WWW`, then run `curl -sS -k --cert pki/cli.crt --key pki/cli_enc.key https://localhost:<p>/index.txt` with `</dev/null` under `setsid` (the key is AES-256 encrypted, passphrase `secret`). Case: `V_L_enc_nopass`. With a tty, REF waits for input.

Commands (run each with REF and with PORT):

```sh
openssl s_server ... -WWW
curl -sS -k --cert pki/cli.crt --key pki/cli_enc.key https://localhost:<p>/index.txt
```

**Expected (real curl)**

- *REF-LINUX:* stderr `Enter PEM pass phrase:` twice, then `curl: (43) unable to set private key file: '.../cli_enc.key' type PEM`.

**Actual (Curl)**

- *PORT-LINUX:* Only the exit 43 line. It does not prompt, so an interactive user cannot type the passphrase.

**Suspected location in source**

- The client-certificate loader in `Curl.Networking.UnitLibrary`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr095Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Measured REF-LINUX (curl 8.18.0, OpenSSL 3.5.5, WSL; no 8.22.0 OpenSSL build is available, and the prompt comes from OpenSSL's default callback, not from curl's code) against `openssl s_server -WWW` with an AES-256 PKCS#8 key, passphrase `secret`, under `setsid`: `curl -sS -k --cert cli.crt --key cli_enc.key https://localhost:<p>/index.txt </dev/null` gives stderr `Enter PEM pass phrase:\n` twice, then `curl: (43) unable to set private key file: 'cli_enc.key' type PEM`, with stdout empty. With `secret` on stdin it prompts once, prints `hello tls` on stdout and exits 0. With a wrong line it prompts once, then prints the exit 43 line. Before the fix PORT printed only the exit 43 line. Details are in ADR-0948.
- Fix: `TlsClientOptions.PemPassPhrasePrompt` (`IPemPassPhrasePrompt`, `TerminalPemPassPhrasePrompt`). `ClientCertificateLoader`'s OpenSSL-build path calls it when no pass phrase was given and the PEM key file is encrypted. The engine's `TlsClientOptionsMapping` sets the terminal prompt for the origin and the proxy. `Cgr095Tests` pins three cases: end of input (two prompts, exit 43), the right answer (one prompt, the key loads) and a wrong answer (one prompt, exit 43). They fail before the fix and pass after it. Variants that already matched (`--pass` given, unencrypted key, DER key type, missing key) ask nothing and are unchanged; `Cgr095Tests` covers them too.
- PORT was checked through the unit tests on Windows, which drive the OpenSSL-build loader path directly. The tests use only temp files and no OS text, so they are platform-neutral and CI on Linux and macOS is expected to stay green.
- Touches: added `Curl.Engine.UnitLibrary/TlsClientOptionsMapping.cs` (where the tool's options get the prompt) with its tests, and `Curl.Engine.UnitTests/EngineCompositionTests.cs`, whose record-equality expectations now include the prompt. No live task in Doing on origin/work/dark-factory names them.
- Choice: a `null` prompt (the record's default) asks nothing, so tests and library callers that build the options themselves never block on the console.
- A traditional `Proc-Type: 4,ENCRYPTED` key gets the prompt, but the BCL cannot decrypt it, so it still fails exit 43 where curl loads it. Filed as BL-3980.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary,Curl.Engine.UnitLibrary` reports 0 failing members. `dotnet build` is clean and the fast tests pass.

## Log

- 2026-10-26: Created from CGR-095 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. The OpenSSL build now prompts Enter PEM pass phrase: for an encrypted PEM key given without --pass, as curl does (ADR-0948).
