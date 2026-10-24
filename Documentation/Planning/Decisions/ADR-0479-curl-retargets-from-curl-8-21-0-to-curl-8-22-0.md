# ADR-0479 — Curl and the gap office retarget from curl 8.21.0 to curl 8.22.0

- **Status:** Accepted
- **Date:** 2026-10-13
- **Amends:** ADR-0433 (the targeted version it recorded as `8.21.0`)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1748.
This is the retarget decision ADR-0433 decision 6 requires before the targeted version
moves.

## Context

ADR-0433 made the targeted curl version a recorded decision: `Gap/Baselines/target.json`
says `8.21.0`, and `scope: newest` findings get no tasks until an ADR moves it. curl 8.22.0
was released on 2026-09-02 (`gh api repos/curl/curl/releases`: `curl-8_22_0` is the latest
release; `curl-8_21_0` was 2026-06-24). The figures below were measured on 2026-10-13, not
taken from memory.

### Open target-scope gaps against 8.21.0

From the latest scorecard, `Gap/Scorecards/2026-10-12_2042.md` (commit `7851aca9b`), and
`Gap/Findings` on `origin/master`:

- Options 568/568, protocols 27/27, features 18/18, write-out 138/138, exit codes 259/259 and
  environment 39/39 are all at 100%. Behaviour is 1241/1394 (89.0%), so 153 upstream cases
  still fail. Overall 2290/2443 (93.7%).
- 3 of the 66 `scope: target` findings are open: GF-0001 (in-process curl never runs
  `TcpConnector`), GF-0005 (`--anyauth` with an upload or form) and GF-0007 (`-T -` read
  whole, not chunked). The other 63 are closed.

### Newest-scope gaps 8.22.0 adds

Three `scope: newest` findings were opened from the 8.21.0 to 8.22.0 release diff:

- GF-0040 (open): 4 options added, `--httpsig-algo`, `--httpsig-headers`, `--httpsig-key`
  and `--httpsig-keyid` (RFC 9421 HTTP Message Signatures; Curl has none of them).
- GF-0039 (closed 2026-10-10_0657): 1 feature removed, `TLS-SRP`, already measured as match.
- GF-0038 (open): 154 behaviour items: 62 cases added, 89 changed and 3 removed. The 62 added
  cases are not in the vendored 8.21.0 test data, so no run has measured them; the 89 changed
  cases are measured against their 8.21.0 text, which is why the finding shows "0 of 154
  items are gaps". Its true size is unknown until the test data is re-vendored.

The scorecard's "Against the newest version" section repeats the target figures (93.7%),
because the run had no release diff for 8.22.0 to score with.

The upstream release notes (`RELEASE-NOTES` at `curl-8_22_0`) list six changes: RFC 9421
HTTP Message Signatures, TLS-SRP dropped, SPNEGO blocking the NTLM fallback, Apple GSS
Framework support, API hardening guards and Apple fast UDP for QUIC, beside the bug fixes.

### Matched reference builds

- **Windows.** The machine's Git for Windows 2.55.0 ships `curl 8.21.0 (x86_64-w64-mingw32)
  libcurl/8.21.0 Schannel ...` (Release-Date 2026-06-24). Git for Windows 2.56.0.windows.1
  (2026-09-28) and every 2.56 release candidate "Comes with cURL v8.22.0", and the latest is
  2.56.0.windows.2 (2026-10-05). The `windows-latest` runner image (Windows 2025, image
  20261004.281.1) already carries Git 2.56.0.windows.1. A matched 8.22.0 Schannel reference
  exists, and the 8.21.0 one is no longer shipped: the next Git for Windows update on any
  machine replaces it, after which every 8.21.0 probe falls back to the documents (ADR-0433
  decision 1).
- **Linux and macOS.** The `ubuntu-latest` image (Ubuntu 24.04, 20261004.327.1) has curl
  `8.5.0-2ubuntu10.15`; the `macos-15-arm64` image (20260907.0337.1) has Curl 8.7.1. Neither
  is 8.21.0 or 8.22.0, so neither version has a matched build there today and the choice is
  neutral on those platforms.

### What moving costs

- `Curl.Conformance.UnitTests/CurlTestCases/Original` (2018 files) re-vendored with
  `Update-UpstreamTestData.ps1 -Tag curl-8_22_0`, `UpstreamCaseRunner.CurlVersion`, and the
  passing-list ratchet for the 89 changed and 62 added cases.
- The version strings Curl prints or sends: `CurlVersionText` (`-V`), the default
  User-Agent (`LibcurlSourceCode.DefaultUserAgent`, `HttpProxyTunnelOptions.Default`),
  `TransferWriteOutVariables` (`libcurl/8.21.0`), `DictRequest.ClientLine` and the
  `--ai-help` text. 394 non-comment C# lines name 8.21.0; 383 of the `curl/8.21.0`
  User-Agent hits are spread over 106 files, mostly tests.
- `Gap/Baselines/target.json` and the scope of GF-0038, GF-0039 and GF-0040.
- Not a cost: 1428 C# files and 343 ADRs name 8.21.0 as the release a behaviour was measured
  on. Those statements stay true of the measurement and are not rewritten (see below).

## Decision

Curl and the gap office retarget to curl 8.22.0 now.

1. **The reason is the reference build.** Curl matches the platform's curl, and on Windows
   that is Git for Windows' mingw64 build. Every Git for Windows release since 2026-09-28
   ships 8.22.0, CI's Windows image already has it, and 8.21.0 can be kept only by pinning an
   outdated Git. Staying on 8.21.0 would turn every reference probe into a documents-only
   fallback at the next routine Git update.
2. **The added work is small beside what is already matched.** Six of seven areas are at
   100% against 8.21.0, and 8.22.0 adds 4 options and drops 1 feature there. The 3 open
   target findings and 153 failing behaviour cases carry over: the cases are re-measured
   against their 8.22.0 text, not abandoned.
3. **Order of the move.**
   1. The factory machine's Git for Windows is upgraded to 2.56.0 or later, so
      `Record-CurlExchange.ps1` and the gap runs find curl 8.22.0. From then on new
      measurements are 8.22.0's.
   2. Curl reports itself as curl 8.22.0 in every string listed above, measured from the
      8.22.0 reference before it is pinned, and `UpstreamCaseRunner.CurlVersion` becomes
      `8.22.0` in the same commit, because the upstream cases expand `%VERSION` from it
      (lane-eligible).
   3. The upstream test data is re-vendored at `curl-8_22_0` (lane-eligible).
   4. `Gap/Baselines/target.json` becomes `8.22.0`, GF-0038, GF-0039 and GF-0040 turn from
      `scope: newest` to `scope: target`, a gap run re-measures, and
      `Gap/Tools/New-TasksFromGaps.ps1` files the `Close GF-####` tasks, including the four
      `--httpsig-*` options (interactive only, because it writes `Gap/`).
4. **Measurements recorded on 8.21.0 stay.** A doc comment, test or ADR that says "as curl
   8.21.0 does" states what was measured and remains true. It is re-measured and rewritten
   only when an 8.22.0 upstream case, a gap run or a new recording shows 8.22.0 answers
   differently. New measurements name 8.22.0.
5. **Linux and macOS are unchanged.** Their builds stay the distribution and Homebrew
   OpenSSL builds of ADR-0433 decision 1; the runner images' curl matches neither version, so
   the move does not change what is matched there.

## Follow-up tasks

- BL-2125 — Upgrade Git for Windows on the factory machine so the reference curl is 8.22.0
  (interactive only).
- BL-2127 — Curl reports itself as curl 8.22.0 in -V, the User-Agent, write-out, DICT and
  --ai-help, and `UpstreamCaseRunner.CurlVersion` moves with them in the same commit, because
  upstream cases expand `%VERSION` from it (after BL-2125).
- BL-2126 — Re-vendor the upstream test data at curl-8_22_0 (after BL-2127).
- BL-2128 — Move the gap office's target to curl 8.22.0 and file the tasks of the 8.22.0
  findings (interactive only, writes `Gap/`; after the other three).

## Consequences

- The dashboard's "against the newest version" figures and the target figures become the
  same release again, until curl 8.23.0 is released.
- The behaviour score is expected to drop at the first 8.22.0 run, because the 62 added cases
  are measured for the first time and the 89 changed ones against new text. That is the gap
  becoming visible, not a regression in Curl.
- Between the Git upgrade and the end of the move, a reference probe names 8.22.0 while
  `target.json` says 8.21.0, so the gap office records documents-only fallbacks for that
  window. The move is ordered to keep it short.
- When curl 8.23.0 is released, the same weighing is done in its own ADR (ADR-0433
  decision 6).
