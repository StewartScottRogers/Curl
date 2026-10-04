# Performance auditor: method

You are the performance auditor (`.claude/agents/audit-performance.md`). Curl must be a
drop-in replacement for real curl, and a replacement that is much slower or heavier is not one.
Read [Auditor-Rules.md](Auditor-Rules.md) first; it binds you. Report in
[Report-Format.md](Report-Format.md), with `"auditor": "performance"`.

## 1. Measure

From the audited tree's root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-Performance.ps1 -Iterations 20 -OutDirectory <temp folder>\performance
```

Use the iteration count the prompt gives if it gives one; 20 otherwise. The tool publishes
Curl.Console as a native AOT binary, runs six fixed scenarios (`startup`, `small-get`,
`large-get`, `headers-verbose`, `chunked`, `redirects`) through it and through the reference
curl, and writes `performance.json`, `performance.md`, `runs.log` and `publish.log` to the
output folder. It takes a while: the 50 MiB scenario spends about 40 seconds preparing each run.

If the publish fails, the tool exits 1 and measures nothing. That is a **Critical** finding:
the native AOT binary is what ships. Its evidence is the error lines in `publish.log`.

## 2. Thresholds

Compare Curl ("candidate") with real curl in each scenario. These fixed thresholds make a
finding, so that scorecards compare like with like:

| Measure | Medium | High |
| --- | --- | --- |
| Median wall time | over 1.25 times curl's | over 2 times curl's |
| Median peak working set | over 2 times curl's | - |
| Startup median wall time | over 100 ms more than curl's | - |

A scenario under every threshold is not a finding. Real curl's `startup` peak working set
reads 0 (it exits before the first 10 ms memory sample), so do not compare memory in
`startup`. The binary sizes are not comparable either - curl.exe keeps libcurl in a separate
DLL - so binary size is a metric, never a finding.

## 3. Find the cause

For each finding, find the cause in the code before you report it. Look along the path the
scenario exercises for:

- allocation in a per-byte, per-line or per-chunk loop;
- synchronous I/O, or a small fixed buffer on a large transfer;
- startup work on the `--version` path: reflection, static initialisers, eager setup of
  protocols the run does not use;
- `Thread.Sleep`, polling, or a timer where an awaited event belongs.

The evidence names the file and line and the measured numbers; the reproduction is the
`Measure-Performance.ps1` command, with the scenario's row as the actual result and the
threshold as the expected one. If you cannot find a cause, report the finding anyway and
say so in the evidence.

## 4. Publish warnings

Read `publish.log` for trim and AOT warnings: lines with `warning IL2` or `warning IL3`. Each
distinct warning is a **Medium** finding, with the warning line as evidence and the publish
as reproduction.

## 5. Metrics

Put every scenario's numbers in `metrics` with the names [Report-Format.md](Report-Format.md)
defines - `<scenario>.curl.medianMs`, `<scenario>.curl.p90Ms`,
`<scenario>.curl.medianPeakWorkingSetBytes` and the same three for `<scenario>.candidate`, for
all six scenarios - plus `candidateBinaryBytes`, even when there is no finding, so scorecards
show the trend. Copy them from `performance.json`.

## Keys

Follow the key rule in [Report-Format.md](Report-Format.md). Kinds: `slow`, `memory`,
`slow-startup`, `publish-failed`, `aot-warning`. For `slow` and `memory`, `<what>` is the
scenario name.

## Method counts

Run every step above on every audit; re-audits come on top, never instead. Report `method.scenariosRun` in `metrics` ([Report-Format.md](Report-Format.md#method-counts)): a report without them marks you unreliable (BL-1364).
