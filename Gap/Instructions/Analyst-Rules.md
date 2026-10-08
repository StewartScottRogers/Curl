# Gap analyst rules

Every gap analyst (`.claude/agents/gap-<area>.md`) reads this file first, then its own
method in `Gap/Instructions/<Area>.md`, and follows both. The design is ADR-0433 decision 3
(`Documentation/Planning/Decisions/ADR-0433-a-gap-analysis-office-measures-curl-against-upstream-curl-releases.md`).
Every file format named here is defined in `Gap/Instructions/Gap-Format.md`.

An analyst reads one area's measurement, `<run>/measurements/<area>.json`, groups its `gap`
items by cause, says how to close each cause, and ends with one report block. The run turns
each group into a gap finding and Claude files its tasks. The analyst decides nothing else.

## The rules

1. **Stay inside the run.** Analyse only the run folder and the commit the prompt names.
   Never change a tracked file. Scratch work goes in the temporary folder the prompt names,
   and nowhere else.
2. **The measurement is the evidence.** Never judge an item from memory of curl. A group's
   `evidence` quotes its items' `expected` and `actual` from the measurement, and gives a
   reproduction a stranger can run from the repository root: one PowerShell command,
   usually the probe the tool ran (the item's `evidence` field).
3. **Every gap in exactly one group.** Every item the measurement marks `gap` belongs to
   exactly one group, and no group holds an item that is not a `gap`. Never drop an item
   and never re-score one: the state is the tool's, not the analyst's. An item you believe
   the tool measured wrongly still goes in a group; say what you believe in `notes`.
4. **Reuse keys.** The prompt lists the area's open and closed findings, each with its `key`
   and its items. A gap item already listed by a finding goes in a group with that
   finding's `key`. A new group gets a new key, `<area>:<cause-slug>`, that is stable
   across runs: a lowercase slug naming the cause, with no counts, dates or versions in it.
5. **Every group is complete.** Each group has:
   - `key`: the group key from rule 4.
   - `title`: what differs, in one line.
   - `severity`: ADR-0433 decision 3's scale. **Critical** when an exit code or the output
     bytes differ on a common path (a plain GET, a POST, `-o`, `-L`, `-u`). **High** when
     an option, scheme, feature, write-out variable or exit code is missing outright.
     **Medium** when an argument form, alias, message text or environment behaviour
     differs. **Low** for anything else. A group takes the highest severity of its items.
   - `introducedIn`: the earliest `introducedIn` of its items, or `null` when none has one.
   - `items`: its item keys.
   - `evidence`: rule 2.
   - `suggestion`: what to change in Curl and where. Name the project, type or file and,
     where it helps, the upstream document section. The filed task's Goal is built from
     the suggestion, so write it as the change to make.
   - `touches`: the Curl project folders the fix changes, such as `Curl.Cli.UnitLibrary`
     and `Curl.Cli.UnitTests`. Never a `Gap/`, `Audit/` or `.claude/` path.
6. **Two phases.** First group and suggest, from the measurement and Curl's code only.
   Only then read the ADRs under `Documentation/Planning/Decisions/`, and only to annotate
   a group's `suggestion` ("explained by ADR-NNNN: ..."). An ADR never removes a group,
   never moves an item out of one and never lowers a severity. A gap a past ADR accepted is
   still a gap now, because the yardstick is upstream's release (ADR-0433 decision 1).
7. **End with the report block.** The reply ends with exactly one fenced `json` block in
   `Gap-Format.md`'s analyst report format (section 6), with the `analyst` name, and the
   `run` and `area` the prompt gives. It parses with `ConvertFrom-Json`. Nothing follows
   it. The run reads only that block.
8. **No Python and no network beyond loopback.** Checks and scratch scripts are PowerShell.
   Any probe you rerun talks only to `127.0.0.1` or `localhost`.

## The report block

The fields are `Gap-Format.md` section 6, plus each group's `key` (rule 4):

```json
{
  "analyst": "gap-<area>",
  "area": "<area>",
  "run": "<run stamp>",
  "groups": [
    {
      "key": "<area>:<cause-slug>",
      "title": "...",
      "severity": "High",
      "introducedIn": "8.8.0",
      "items": ["<item key>"],
      "evidence": "...",
      "suggestion": "...",
      "touches": ["Curl.Cli.UnitLibrary", "Curl.Cli.UnitTests"]
    }
  ],
  "notes": ""
}
```

A measurement with no `gap` items gives `"groups": []`.

## Checking the block before sending it

Save the block to the temporary folder and run, from the repository root:

```powershell
$report = Get-Content <temp>\report.json -Raw | ConvertFrom-Json
$measurement = Get-Content <run>\measurements\<area>.json -Raw | ConvertFrom-Json
$gaps = @($measurement.items | Where-Object state -eq 'gap' | ForEach-Object key)
$grouped = @($report.groups | ForEach-Object { $_.items })
Compare-Object $gaps $grouped                                  # prints nothing
$grouped.Count -eq ($grouped | Sort-Object -Unique).Count       # True: no item twice
```
