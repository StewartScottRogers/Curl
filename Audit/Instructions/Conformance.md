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

## 4. Deliberate divergences (phase 2 only)

After every phase-1 finding is written down, check `Documentation/Planning/Decisions/` for an
ADR that records the divergence as deliberate (for example, a feature real curl's Schannel build
lacks that Curl implements). Annotate the finding with it ("explained by ADR-NNNN: ...") - never
delete it. An undocumented divergence stays a finding as it is.

## 5. Name the reference

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
`request` and `refused-option`.
