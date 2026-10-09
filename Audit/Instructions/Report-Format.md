# Auditor report format

Every auditor ends its reply with one machine-readable report block. The audit run
saves each reply, and the audit tools read the block:
`Audit/Tools/Write-AuditFindings.ps1` (BL-1016) turns it into finding files
under `Audit/Findings/`, and `Audit/Tools/Write-AuditScorecard.ps1` (BL-1017)
turns it into the scorecard's rows. The tools read nothing else from the reply, so
anything not in the block is lost to them. The rules every auditor follows are in
[Auditor-Rules.md](Auditor-Rules.md); the finding file is described in
[Findings/README.md](../Findings/README.md) and the scorecard in
[Scorecards/README.md](../Scorecards/README.md).

## The block

- Exactly one fenced code block whose info string is `json`, and it is the last thing in
  the reply. The tools take the last `json` block, so an earlier one is ignored, not
  merged.
- The block is one JSON object that parses with PowerShell's `ConvertFrom-Json`: no
  comments, no trailing commas, strings escaped (`\"`, `\\`, `\n`).
- Every top-level key below is present. An auditor with no findings writes
  `"findings": []`; one given nothing to re-audit writes `"reaudits": []`; every
  auditor's `metrics` holds at least its [method counts](#method-counts).

```
{ "auditor", "commit", "fingerprint",
  "findings": [ { "key", "title", "severity", "location", "evidence", "reproduction" } ],
  "reaudits": [ { "finding", "reproduces", "evidence" } ],
  "metrics": { ... } }
```

### Top level

| Key | Type | Value |
| --- | --- | --- |
| `auditor` | string | The auditor's name: `quality`, `security`, `performance`, `conformance`, `truthfulness` or `process`. |
| `commit` | string | The full 40-character SHA of the commit the prompt names as audited, copied from the prompt. |
| `fingerprint` | string | The auditor fingerprint the prompt gives (64 lowercase hex characters, from `Audit/Tools/Get-AuditorFingerprint.ps1`, BL-1002), copied from the prompt. |
| `findings` | array | One object per finding, in the order found. |
| `reaudits` | array | One object per finding the prompt listed for re-audit, each exactly once. |
| `metrics` | object | The auditor's measurements, named as in [Metrics](#metrics). |

### A finding

| Key | Type | Value |
| --- | --- | --- |
| `key` | string | The stable dedupe key, see [The key](#the-key). |
| `title` | string | One line: what is wrong and where. |
| `severity` | string | `Critical`, `High`, `Medium` or `Low`, as defined in [Findings/README.md](../Findings/README.md#severities). |
| `location` | string | Where the defect is: a repository-relative path with `/` separators and a line, `path:line`, or `path` alone when the defect is the whole file. Evidence outside the audited tree uses the same shape: a file in the log folder the prompt names as `logs/<file>:<line>`, a commit as `commit:<sha>`. Never an absolute path. |
| `evidence` | string | What was observed and why it is a defect: the quoted code, command output or measurement. |
| `reproduction` | object | `{ "command", "expected", "actual" }`: one command a stranger runs from the repository root (PowerShell, no Python), what a correct tree gives, and what the audited tree gave. A surviving-mutant finding also carries `"mutation": "<file>:<line>:<operator>"`, the mutant as `Audit/Tools/Invoke-MutationTest.ps1` printed it: the audit run reruns it itself, and the finding's key is built from it ([Findings/README.md](../Findings/README.md#closing)). |

### A re-audit

| Key | Type | Value |
| --- | --- | --- |
| `finding` | string | The finding's ID, `AF-####`, as the prompt gives it. |
| `reproduces` | boolean or `null` | `true` when running the finding's reproduction still gives its actual (defective) result, `false` when it gives the expected one, `null` when it could not be run or could not tell (recorded as "not re-audited"; never `false` for a reproduction not run). |
| `evidence` | string | What running the reproduction showed. |

## The key

`key` names one issue so that the same issue reported by a later audit is recognised as
the same finding, and a different issue is never mistaken for it. It has four parts,
joined by `:`:

```
<auditor>:<where>:<what>:<kind>
quality:Curl.Cli.UnitTests/ParserTests.cs:Parse_Empty_Throws:weak-assertion
```

| Part | Holds |
| --- | --- |
| `<auditor>` | The auditor's name, the same as the block's `auditor`. |
| `<where>` | The repository-relative path of the file the defect is in, with `/` separators, or `logs` or `commit` when the evidence is not in the audited tree. |
| `<what>` | The stable name of the thing that is wrong: a type, member, test method, option, scenario or task ID. |
| `<kind>` | What is wrong with it, a short lower-case kebab-case word from the vocabulary in the auditor's own instructions (`weak-assertion`, `non-constant-time-compare`, `slower-than-curl`, `exit-code-differs`). |

The key is stable across runs: it never contains a line number, a date, a measured
value, a commit or anything else that changes while the issue stays the same. No part
contains `:` or a space. Paths and member names keep their case; the auditor name and
the kind are lower case. Two issues in the same member get different kinds; the same
issue found again gets the same key, character for character.

## Metrics

`metrics` is one flat object: each name is a single string key (the dots are part of the
name, not nesting) and each value is a JSON number. A required metric the auditor could
not measure is `null`, and a finding says why. The tools read only the names below; an
auditor may add others, which are kept in its saved reply and nowhere else.

### Method counts

Every auditor reports how much of its method it ran, so a run that did only its re-audits
shows. `Write-AuditScorecard.ps1` marks an auditor **unreliable** when any of its counts
is missing, `null` or 0 (BL-1364). Re-audits never count toward them.

| Auditor | Names | Counts |
| --- | --- | --- |
| quality | `method.librariesMutated`, `method.testsRead` | Libraries mutation-tested; tests read in full. |
| security | `method.fuzzTargets`, `method.timingSitesRead` | Parsers fuzzed; secret-handling sites read for timing. |
| performance | `method.scenariosRun` | Scenarios measured (six in a full run). |
| conformance | `method.casesRun` | Generated command lines run through both binaries. |
| truthfulness | `method.names`, `method.docComments`, `method.documentStatements`, `method.adrs`, `method.scriptStatements` | Items checked in steps 1 to 5. |
| process | `method.rulesChecked` | Rules in its method checked against the measurements. |

### performance

From `Audit/Tools/Measure-Performance.ps1` (BL-1007). For each scenario, in this
order: `startup`, `small-get`, `large-get`, `headers-verbose`, `chunked`, `redirects`:

| Name | Unit | Meaning |
| --- | --- | --- |
| `<scenario>.curl.medianMs` | milliseconds | Median wall time of real curl over the run's iterations. |
| `<scenario>.curl.p90Ms` | milliseconds | 90th-percentile wall time of real curl. |
| `<scenario>.curl.medianPeakWorkingSetBytes` | bytes | Median peak working set of real curl. |
| `<scenario>.candidate.medianMs` | milliseconds | The same, for Curl's native AOT build. |
| `<scenario>.candidate.p90Ms` | milliseconds | The same, for Curl's native AOT build. |
| `<scenario>.candidate.medianPeakWorkingSetBytes` | bytes | The same, for Curl's native AOT build. |

Plus one name for the whole run:

| Name | Unit | Meaning |
| --- | --- | --- |
| `candidateBinaryBytes` | bytes | Size of the published native AOT `Curl.Console` binary. |

### process

From `Audit/Tools/Measure-FactoryProcess.ps1` (BL-1008), whose header help is the
authoritative definition of each; units and a short meaning here:

| Name | Unit | Meaning |
| --- | --- | --- |
| `tasksDone` | count | Tasks that reached Done in the measured period. |
| `medianTaskMinutes` | minutes | Median time from claim to Done. |
| `p90TaskMinutes` | minutes | 90th-percentile time from claim to Done. |
| `tasksClaimedMoreThanOnce` | count | Tasks claimed more than once (redone work). |
| `requeues` | count | `Doing -> Backlog` moves in task logs. |
| `resumedRuns` | count | Task runs that resumed an earlier, stopped run. |
| `ciRedMinutes` | minutes | Time `CI` was red on `work/dark-factory`: from the first failed run after a success to the next success, cancelled runs ignored. |
| `laneIdleMinutes` | minutes | Sum of the lanes' `wait` phases. |
| `waitOverlapMinutes` | minutes | The part of `laneIdleMinutes` spent waiting because a ready task overlapped one in Doing. |
| `waitNothingReadyMinutes` | minutes | The rest of `laneIdleMinutes`: waiting with nothing ready. |
| `laneMinutes` | minutes | Every lane's time from its first to its last log line, summed: what the idle shares are of (BL-1366). |
| `tokensInput` | tokens | Input tokens of all runs. |
| `tokensOutput` | tokens | Output tokens of all runs. |
| `costUsd` | US dollars | Cost of all runs. |
| `costUsdPerTaskDone` | US dollars | `costUsd` divided by `tasksDone`. |

### quality

| Name | Unit | Meaning |
| --- | --- | --- |
| `mutationScore.<Library>` | fraction, 0 to 1 | One per library mutated, for example `mutationScore.Curl.Cli.UnitLibrary`: the `score` `Audit/Tools/Invoke-MutationTest.ps1` (BL-1004) reports, `(killed + timedOut) / (killed + timedOut + survived)`. |

### security

| Name | Unit | Meaning |
| --- | --- | --- |
| `fuzzIterations.<target>` | count | One per fuzz target run (`tls-handshake`, `cli`, `ssh`): inputs `Audit/Tools/Fuzz/Fuzz.cs` (BL-1005) fed that target. |
| `fuzzCrashes.<target>` | count | Distinct inputs the harness saved for that target: an unexpected exception or a hang, one per exception type and top stack frame. |

### conformance

| Name | Unit | Meaning |
| --- | --- | --- |
| `differentialCases` | count | Generated command lines run through both binaries by `Audit/Tools/Invoke-DifferentialConformance.ps1` (BL-1006). |
| `differentialDifferences` | count | Cases whose request bytes, standard output, standard error or exit code differed. |

### truthfulness

None required: `"metrics": {}`.

## Example

A complete report block from the quality auditor. It is illustrative: the files, tests,
IDs and numbers in it are not real findings.

```json
{
  "auditor": "quality",
  "commit": "b2bb89da5c0e4f1a9d3b7e6c2a8f0d4e1b3c5a79",
  "fingerprint": "3f9a1c0e7b2d4f6a8c1e3b5d7f9a2c4e6b8d0f1a3c5e7b9d2f4a6c8e0b1d3f5a",
  "findings": [
    {
      "key": "quality:Curl.Cli.UnitTests/ParserTests.cs:Parse_Empty_Throws:weak-assertion",
      "title": "Parse_Empty_Throws asserts only that a result exists, not the refusal it names",
      "severity": "Medium",
      "location": "Curl.Cli.UnitTests/ParserTests.cs:41",
      "evidence": "Line 41 is the test's only assertion: Assert.IsNotNull(result);. The name promises a refusal, but a parse that accepted the empty argument would also pass.",
      "reproduction": {
        "command": "Select-String -Path Curl.Cli.UnitTests/ParserTests.cs -Pattern \"Assert\\.IsNotNull\\(result\\)\"",
        "expected": "No match: the test asserts the refusal and its exit code 2.",
        "actual": "ParserTests.cs:41:        Assert.IsNotNull(result);"
      }
    }
  ],
  "reaudits": [
    {
      "finding": "AF-0003",
      "reproduces": false,
      "evidence": "Ran the reproduction: Select-String found no match; the test now asserts the refusal's exit code 2."
    }
  ],
  "metrics": {
    "mutationScore.Curl.Cli.UnitLibrary": 0.82,
    "mutationScore.Curl.Protocol.Dict.UnitLibrary": 0.94
  }
}
```

## From report to finding

How a reported finding fills [FINDING-TEMPLATE.md](../Findings/FINDING-TEMPLATE.md):

| Placeholder | Filled from |
| --- | --- |
| `{{ID}}` | The next `AF-####` in `Audit/Findings/`. |
| `{{TITLE}}` | `title` |
| `{{AUDITOR}}` | `auditor` |
| `{{SEVERITY}}` | `severity` |
| `{{KEY}}` | `key`; with `reproduction.mutation`, the key built from the mutant: `quality:<file>:<member>-<operator word>:surviving-mutant`. |
| `{{REPRODUCTION}}` | `mutation <file>:<line>:<operator>` from `reproduction.mutation`, or `none`. |
| `{{FOUND}}` | The audit's date, `yyyy-MM-dd`. |
| `{{FOUND_AT}}` | `commit` |
| `{{SCORECARD}}` | The file name of the audit's scorecard. |
| `{{SUMMARY}}` | Written by the tool from `title`, `auditor`, `severity` and `location`, with the notes [Findings/README.md](../Findings/README.md#rules) requires. |
| `{{LOCATION}}` | `location` |
| `{{EVIDENCE}}` | `evidence` |
| `{{REPRODUCTION_COMMAND}}` | `reproduction.command`; with `reproduction.mutation`, the targeted `Invoke-MutationTest.ps1 -Site` command for it. |
| `{{REPRODUCTION_EXPECTED}}` | `reproduction.expected` |
| `{{REPRODUCTION_ACTUAL}}` | `reproduction.actual` |

A re-audit entry becomes one line under the finding's `## Re-audits`, in the shape
[Findings/README.md](../Findings/README.md#body) gives.
