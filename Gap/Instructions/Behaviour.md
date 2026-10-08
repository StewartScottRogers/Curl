# Gap analyst method: behaviour

The `gap-behaviour` analyst follows `Gap/Instructions/Analyst-Rules.md` first and this
method second. It reads `<run>/measurements/behaviour.json`, written by
`Gap/Tools/ConvertTo-BehaviourMeasurement.ps1` (BL-1729) and amended by
`Gap/Tools/Measure-ReferenceCrossCheck.ps1` (BL-1730); each script's header says how it
decides a case's state.

## What the measurement holds

One item per upstream `tests/data` case, keyed `behaviour:test<N>`:

| State | Means |
| --- | --- |
| `match` | The in-process harness ran the case and it passed, or the cross-check found the reference curl agreeing with Curl where the case's `<verify>` does not (`reason` is `reference-diverges`). |
| `gap` | The case failed. `actual` holds the harness's first difference, `expected` is `upstream test<N> passes`, and `attributes.keywords` holds the case's `<info><keywords>`. |
| `unmeasured` | The harness could not run the case; `reason` says why (`Gap-Format.md` section 4, area `behaviour`). |
| `excluded` | The case is not a curl tool case on this platform (`libcurl-api`, `libcurl-unit-test`, `debug-build-only`, `platform:<os>`, `reference-lacks:<feature>`). |

The harness's first difference is one of these shapes (`Curl.Conformance.UnitLibrary`,
`UpstreamFirstDifference` and `UpstreamCaseVerification`):

- `<verify><stdout> differs at byte <offset> (line <line>): expected "...", got "..."`, and
  the same for `<verify><protocol>` (the request bytes), `<verify><file>` and the other
  `<verify>` parts;
- `<verify><errorcode>: expected exit code <n>, got <m>`;
- `the case did not finish within <s> seconds`, or `the harness threw <Type>: <message>`.

A case's file is `<release>/tests/data/test<N>`, in the release folder the prompt names.
Its format is upstream's `docs/tests/FILEFORMAT.md`.

## Method

Behaviour is the costly area: a release has about two thousand cases and a run can hold
hundreds of failures. Spend tokens on causes, not on cases.

### 1. Pre-group mechanically

Normalise each failing case's first difference into a signature: cut the quoted lines after
`: expected`, then strip paths, test numbers, ports and every other number. Bucket by
signature, then by the case's first keyword. Run this from the repository root, with
`$file` set to the measurement:

```powershell
$file = '<run>\measurements\behaviour.json'; (Get-Content $file -Raw | ConvertFrom-Json).items | Where-Object state -eq 'gap' | ForEach-Object { [pscustomobject]@{ Signature = ($_.actual -replace ':\s*expected.*$', '' -replace '[A-Za-z]:\\\S*|/\S*/\S*', '<path>' -replace 'test\d+', 'test<N>' -replace ':\d{2,5}\b', ':<port>' -replace '\d+', 'N'); Keyword = (@($_.attributes.keywords) + '(none)')[0]; Case = $_.key } } | Group-Object Signature, Keyword | Sort-Object Count -Descending | Format-Table Count, Name, @{ n = 'Cases'; e = { ($_.Group.Case | Select-Object -First 5) -join ' ' } } -AutoSize -Wrap
```

It runs under Windows PowerShell 5.1 and PowerShell 7. Against BL-1729's fixture
(`Gap/Tools/Fixtures/behaviour/`, converted with
`ConvertTo-BehaviourMeasurement.ps1 -RawFile Gap\Tools\Fixtures\behaviour\raw.json -TestsData Gap\Tools\Fixtures\behaviour\tests\data -OutFile $env:TEMP\behaviour.json`),
whose one failing case is `test9`, it prints:

```text
Count Name                           Cases
----- ----                           -----
    1 stdout differs at byte N, HTTP behaviour:test9
```

(The fixture's raw detail has no `<verify>` label; a real run's signature reads
`<verify><stdout> differs at byte N (line N)`.) Each line is a bucket: its signature, its
first keyword, and up to five of its cases.

### 2. Find the cause per bucket

For each bucket, open two or three of its cases' files and read what upstream expects: the
`<client><command>`, the `<reply>` the server sends and the `<verify>` part the first
difference names. Then find in Curl's code what produces the difference - start from the
library that owns the keyword's protocol (`Curl.Protocol.<Name>.UnitLibrary`), the option's
parser and applier in `Curl.Cli.UnitLibrary`, or the output writer in `Curl.Console`. A
reproduction is the case rerun alone through the harness:
`dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <release>/tests/data <temp>/raw.json <N>`.

Merge buckets that share one cause (the same missing header, under several keywords), and
split a bucket that holds two (an `<errorcode>` signature caused by two different
options).

### 3. One group per cause

Each cause is one group, keyed `behaviour:<cause-slug>` (rule 4 of the analyst rules).
Its `suggestion` names the project and the type or file to change, and what to change, so
that one filed task closes every case in the group. Its `touches` names the project
folders, usually one `Curl.<Area>.UnitLibrary` and its `.UnitTests`. A group of more than
40 cases is split by sub-cause where one exists (by option, by protocol, by `<verify>`
part), so a filed task stays sized for one `/task-run`; where none exists, say so in
`notes`.

### 4. Unmeasured cases are not groups

Only `gap` items go in groups. Report the measurement's `reasons` counts in `notes`,
together with the two or three harness extensions that would measure the most cases, for
example "an FTP server emulation would measure 512 cases (`needs-server:ftp`)". Those
extensions are office work, filed interactively after BL-1746, not Curl tasks: they never
become groups.

### 5. Reference-diverges cases are not gaps

An item that is `match` with `reason` `reference-diverges` is a case where the reference
curl agrees with Curl and not with the case's `<verify>`. It is not a gap; name their count
in `notes`.

### 6. Severity

ADR-0433 decision 3:

| Group | Severity |
| --- | --- |
| It includes a plain GET, POST, `-o`, `-L` or `-u` case whose exit code or output bytes differ | Critical |
| Only message text differs (a `<verify><stderr>` difference, or an error message with the right exit code) | Medium |
| Anything else | High |

A group takes the highest severity of its cases.

### 7. Example report block

```json
{
  "analyst": "gap-behaviour",
  "area": "behaviour",
  "run": "2026-10-09_1430",
  "groups": [
    {
      "key": "behaviour:http-head-response-body-written",
      "title": "A HEAD request's response headers are followed by body bytes on stdout",
      "severity": "High",
      "introducedIn": null,
      "items": ["behaviour:test9"],
      "evidence": "behaviour:test9 expected 'upstream test9 passes', actual 'stdout differs at byte 4'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <release>/tests/data $env:TEMP/raw.json 9",
      "suggestion": "In Curl.Protocol.Http.UnitLibrary, stop the response reader from writing a body after a HEAD request's headers, as upstream's tests/data/test9 <verify><stdout> expects.",
      "touches": ["Curl.Protocol.Http.UnitLibrary", "Curl.Protocol.Http.UnitTests"]
    }
  ],
  "notes": "Unmeasured by reason: unknown-variable 3, harness-unsupported 1, needs-server:ftp 1, needs-server:https 1. Extensions that would measure most: an HTTPS server in the harness (1 case), an FTP server emulation (1 case). reference-diverges: 0."
}
```

Check it with the analyst rules' check before sending it.
