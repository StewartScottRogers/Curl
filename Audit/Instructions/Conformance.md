# Conformance auditor: method

You are the conformance auditor (`.claude/agents/audit-conformance.md`). Curl's whole promise is
that a script cannot tell which binary it invoked. Read [Auditor-Rules.md](Auditor-Rules.md)
first; it binds you. Report in [Report-Format.md](Report-Format.md), with
`"auditor": "conformance"`.

## The standard

The factory has its own on-demand conformance check, `.claude/agents/conformance-auditor.md`,
used inside the `/feature` and `/protocol` pipelines for one area at a time. You are the
independent, periodic check across everything. Apply its standard, not your own:

- its section **"Establish the upstream truth first"**: never judge from memory of what curl
  does; the reference curl's own behaviour and output decide;
- its section **"What to compare"**: option names and aliases, exit codes, stdout and stderr
  bytes, and `--write-out` variables.

Read those two sections; do not use the rest of that file, and never change it.

## 1. Run the differential tool

From the audited tree's root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-DifferentialConformance.ps1 -Count <n> -Seed <seed> -OutDirectory <temp folder>\differential
```

- `-Count`: the number the prompt gives, or 300.
- `-Seed`: a fresh one you choose (for example the audited commit's first eight hex digits as
  a number), recorded in your summary and in every finding's evidence.
- `-OutDirectory`: in the temporary folder the prompt names, never in the audited tree.

It builds Curl.Console in Release, runs each generated case through the reference curl and
Curl against the same loopback exchange, and writes `summary.json` (with the reference curl's
version) and a `repro.ps1` for every differing case. Report `differentialCases` and
`differentialDifferences` in `metrics`.

## 2. Reduce each difference

For each differing case, find the smallest command line that still differs: drop one option
(with its value) at a time and rerun the case's two `Record-CurlExchange.ps1` commands from its
`repro.ps1`; keep a drop when the difference persists. The finding names that smallest command
line, and its reproduction is the reduced pair of commands, with curl's output as the expected
result and Curl's as the actual.

## 3. One finding per cause

Several cases often share one cause - the same option refused, the same message worded
differently. Group them: one finding, one `key`, per cause, listing every case number it
covers in the evidence.

## 4. Transfers under load

Curl never aims to match real curl's speed, memory or binary size - C curl will always be
faster and smaller (Stewart, 2026-10-13, BL-2132) - so never report a time, a memory figure,
a size or a ratio against curl as a finding. Performance matters only when it breaks
behaviour: a hang, a run that never finishes, quadratic blow-up on a large input, or a
transfer that fails under load where curl succeeds. A script can tell those apart, so they
are conformance failures. From the audited tree's root:

```powershell
powershell -NoProfile -File Audit/Tools/Test-TransfersUnderLoad.ps1 -OutDirectory <temp folder>\load
```

It publishes Curl.Console as the native AOT binary that ships, runs four fixed heavy transfers
(`large-get`, `many-chunks`, `many-headers`, `many-redirects`) through it and through the
reference curl, once each under a generous time limit, and writes `load.json`, `load.md`,
`publish.log` and each run's folder. It takes several minutes: the recorder spends about a
minute preparing the 50 MiB body for each `large-get` run.

- A scenario whose `differences` is not empty is a finding. Curl's exit `timeout` where curl
  finished is kind `hang` (High; Critical for `large-get`, a plain download); a different exit
  code is kind `exit-code`; the same exit code with different output bytes is kind `output`.
  The evidence gives both outcomes from `load.json` and the cause, from the code along that
  scenario's path: a loop that re-reads, re-copies or re-scans everything received so far on
  each chunk, header or hop; a wait for an event that never comes; a limit curl does not have.
  If you cannot find the cause, report the finding anyway and say so. The reproduction is the
  tool's command with `-Scenario <name>`, curl's outcome as the expected result and Curl's as
  the actual.
- If the publish fails, the tool exits 1 and runs nothing. That is a **Critical** finding,
  kind `publish-failed`: the native AOT binary is what ships. Its evidence is the error lines
  in `publish.log`.
- Each distinct `warning IL2` or `warning IL3` line in `publish.log` is a **Medium** finding,
  kind `aot-warning`: trimmed or AOT-compiled code can behave differently from the build the
  differential run tested. Evidence: the warning line; reproduction: the publish.

Report `loadDifferences` (`load.json`'s `different`) in `metrics`.

## 5. Platform differences an ADR decided

Some differences from the reference curl are kept on purpose on one platform only, by an
accepted ADR: ADR-0786 keeps HTTP/2 (`--http2`, `--http2-prior-knowledge`, `--proxy-http2`)
working on Windows, where the Schannel curl refuses it. They are listed in
`Gap/Baselines/platform-exclusions.json` in the audited tree, one item per difference with its
`key` (`<area>:<item>`, such as `options:--http2` or `behaviour:test1704`), its `platform`
(`windows`, `linux` or `macos`) and its reason (`decided:ADR-NNNN`). Read it before writing any
finding (BL-3501; AF-0381, AF-0423 and AF-0432 refiled ADR-0786's difference and were rejected).

- Do not report a difference the list names for the platform you measured on - the same
  option, or the same upstream case. Name it in your summary instead, with its ADR ("excluded
  by ADR-0786 on windows: `--http2`"), so the reader sees it was measured and set aside.
- A difference the list does not name for that platform is still reported, even when it looks
  like a listed one: the same option on another platform, or another option with the same
  symptom. Step 6 may then annotate it with an ADR, but it stays a finding.
- The list changes only by an ADR. Never treat a difference as decided because you think an
  ADR would allow it, and never edit the file.

## 6. Deliberate divergences (phase 2 only)

After every phase-1 finding is written down, check `Documentation/Planning/Decisions/` for an
ADR that records the divergence as deliberate (for example, a feature real curl's Schannel build
lacks that Curl implements). Annotate the finding with it ("explained by ADR-NNNN: ...") - never
delete it. An undocumented divergence stays a finding as it is.

## 7. Name the reference

State the reference curl's `--version` first line in your summary and in every finding's
evidence: a conformance claim means nothing without the curl it was measured against.

## Severity

`conformance-auditor.md` grades with Blocker, Major and Minor. Map them to the finding scale:

| conformance-auditor | Finding severity |
| --- | --- |
| Blocker | High; Critical when it changes an exit code on a common path (a plain GET, a POST, `-o`, `-L`, `-u`) |
| Major | Medium |
| Minor | Low |

## Keys

Follow the key rule in [Report-Format.md](Report-Format.md). `<where>` is the option or area
(`--libcurl`, `exit-codes`, `write-out`), and kinds are `exit-code`, `stdout`, `stderr`,
`request` and `refused-option`. For step 4, `<where>` is the scenario (`large-get`,
`many-chunks`, `many-headers`, `many-redirects`) or `publish`, and the kinds are `hang`,
`exit-code`, `output`, `publish-failed` and `aot-warning`.

## Method counts

Run every step above on every audit; re-audits come on top, never instead. Report `method.casesRun` and `method.loadScenariosRun` in `metrics` ([Report-Format.md](Report-Format.md#method-counts)): a report without them marks you unreliable (BL-1364).
