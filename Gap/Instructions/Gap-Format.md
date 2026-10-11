# Gap office file formats

This document defines, field by field, every file the gap analysis office's tools read and
write. The design behind it is ADR-0433
(`Documentation/Planning/Decisions/ADR-0433-a-gap-analysis-office-measures-curl-against-upstream-curl-releases.md`).
Every gap tool builds against the formats here. A tool that needs a field this document
does not define changes this document first.

## Rules for every file

- JSON is UTF-8 without a byte order mark and holds no comments. It parses with
  `ConvertFrom-Json` in Windows PowerShell 5.1 and in PowerShell 7.
- Markdown and JSON use ASCII punctuation only, so PowerShell 5.1 reads them the same as
  PowerShell 7.
- Paths inside a file are repository-relative and use `/`.
- A version is upstream's dotted release number, such as `8.21.0`. A tag is upstream's git
  tag, such as `curl-8_21_0`.
- Dates are ISO 8601: a date is `yyyy-MM-dd`, a time is `yyyy-MM-ddTHH:mm:ssZ` in UTC.
- A **run stamp** is the local start time of a gap run, `yyyy-MM-dd_HHmm`. It names the run
  folder `<repo>.gap\<stamp>\` (written `<run>` below) and the run's scorecard.
- A field shown as `null` in a table may be JSON `null`. Every other field is always present.

The ten formats:

1. [Release manifests](#1-release-manifests)
2. [Upstream inventory](#2-upstream-inventory)
3. [The item key rule](#3-the-item-key-rule)
4. [Area measurement](#4-area-measurement)
5. [Release diff](#5-release-diff)
6. [Analyst report block](#6-analyst-report-block)
7. [Gap finding](#7-gap-finding)
8. [Scorecard and history](#8-scorecard-and-history)
9. [Dashboard data](#9-dashboard-data)
10. [Against the newest version](#10-against-the-newest-version)

## 1. Release manifests

### Release manifest, `Gap/Baselines/curl-<version>.json`

Written by `Get-UpstreamRelease.ps1` the first time it fetches a release. Every later fetch
of that version must produce the same `sha256`, or the tool stops.

| Field | Type | Meaning |
| --- | --- | --- |
| `version` | string | The release version, `8.21.0`. |
| `tag` | string | The release's git tag, `curl-8_21_0`. |
| `url` | string | The URL the tarball was downloaded from. |
| `sha256` | string | The tarball's SHA-256, 64 lowercase hexadecimal digits. |
| `fetched` | string | The date of the first fetch, `yyyy-MM-dd`. |

```json
{
  "version": "8.21.0",
  "tag": "curl-8_21_0",
  "url": "https://github.com/curl/curl/releases/download/curl-8_21_0/curl-8.21.0.tar.gz",
  "sha256": "0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0",
  "fetched": "2026-10-08"
}
```

### Target, `Gap/Baselines/target.json`

The release Curl matches. Only the ADR that moves the target changes it (ADR-0433
decision 6).

| Field | Type | Meaning |
| --- | --- | --- |
| `version` | string | The targeted version. |
| `decidedBy` | string | The ADR that set it, `ADR-####`. |

```json
{ "version": "8.21.0", "decidedBy": "ADR-0433" }
```

### Newest, `Gap/Baselines/newest.json`

The latest upstream release, written by the release watch each time it checks.

| Field | Type | Meaning |
| --- | --- | --- |
| `version` | string | The newest release's version. |
| `tag` | string | Its git tag. |
| `published` | string | The date GitHub says it was published, `yyyy-MM-dd`. |
| `checked` | string | The date of the last check, `yyyy-MM-dd`. |

```json
{
  "version": "8.22.0",
  "tag": "curl-8_22_0",
  "published": "2026-11-05",
  "checked": "2026-11-09"
}
```

## 2. Upstream inventory

`Gap/Upstream/<version>/<area>.json`, one file per area and version, built from the release
tarball by the area's inventory tool. It lists what upstream has. It holds no measurement.

| Field | Type | Meaning |
| --- | --- | --- |
| `area` | string | One of the seven area names (section 3). |
| `version` | string | The release the inventory was read from. |
| `sources` | string[] | The tarball files read, relative to the tarball root, such as `docs/cmdline-opts/ech.md`. A glob such as `docs/cmdline-opts/*.md` is allowed. |
| `items` | object[] | One entry per upstream item, sorted by `key`. |
| `items[].key` | string | The item's key (section 3). |
| `items[].name` | string | The item as upstream writes it: `--ech`, `mqtts`, `HTTP3`, `time_queue`, `CURLE_ECH_REQUIRED`, `NO_PROXY`, `test1234`. |
| `items[].introducedIn` | string or `null` | The version upstream says introduced the item, or `null` when upstream does not say. |
| `items[].attributes` | object | The area's attributes, below. |

Attributes by area:

| Area | Attribute | Type | Meaning |
| --- | --- | --- | --- |
| `options` | `short` | string or `null` | The short alias, `-k`, or `null`. |
| | `arg` | string or `null` | The argument placeholder, `<config>`, or `null` for an option that takes none. |
| | `protocols` | string[] | The protocols the option applies to, empty when it applies to all. |
| | `boolean` | boolean | `true` when the option is a boolean switch. |
| | `noForm` | boolean | `true` when the option has a `--no-` form. |
| `protocols` | `scheme` | string | The URL scheme, lowercase. |
| | `tls` | boolean | `true` for the `(S)` variant that runs over TLS. |
| `features` | `name` | string | The feature name as `curl -V` prints it. |
| `writeout` | `name` | string | The variable name inside `%{...}`. |
| `exitcodes` | `number` | integer | The exit code. |
| | `curleName` | string | The `CURLE_*` name. |
| | `strerror` | string or `null` | The `curl_easy_strerror` text from `lib/strerror.c`. |
| | `exitText` | string or `null` | The description in `docs/cmdline-opts/_EXITCODES.md`. |
| `environment` | `name` | string | The variable name, config path or config syntax element. |
| | `kind` | string | `variable`, `config-path` or `config-syntax`. |
| `behaviour` | `number` | integer | The test case number. |
| | `sha256` | string | The SHA-256 of the case's `tests/data/test<number>` file. |
| | `keywords` | string[] | The case's `<keywords>`. |
| | `tool` | string or `null` | The case's `<tool>`, or `null` when the case runs curl itself. |
| | `features` | string[] | The case's `<features>` requirements. |

```json
{
  "area": "options",
  "version": "8.21.0",
  "sources": ["docs/cmdline-opts/*.md"],
  "items": [
    {
      "key": "options:--ech",
      "name": "--ech",
      "introducedIn": "8.8.0",
      "attributes": { "short": null, "arg": "<config>", "protocols": ["HTTPS"], "boolean": false, "noForm": false }
    },
    {
      "key": "options:--insecure",
      "name": "--insecure",
      "introducedIn": "7.10",
      "attributes": { "short": "-k", "arg": null, "protocols": ["TLS", "SFTP", "SCP"], "boolean": true, "noForm": true }
    }
  ]
}
```

```json
{
  "area": "behaviour",
  "version": "8.21.0",
  "sources": ["tests/data/test*"],
  "items": [
    {
      "key": "behaviour:test1234",
      "name": "test1234",
      "introducedIn": null,
      "attributes": {
        "number": 1234,
        "sha256": "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90",
        "keywords": ["HTTP", "globbing"],
        "tool": null,
        "features": []
      }
    }
  ]
}
```

## 3. The item key rule

Every item has one key, `<area>:<item>[:<facet>]`.

- `<area>` is one of exactly seven names: `options`, `protocols`, `features`, `writeout`,
  `exitcodes`, `environment` and `behaviour`.
- `<item>` is the item as upstream names it: the long option, the scheme, the feature, the
  write-out variable, the exit code's number, the environment variable or config element,
  or `test<number>`.
- `<facet>` is optional and names one measured aspect of the item, such as `argument`,
  `alias`, `no-form` or `strerror`. An item key with a facet is measured on its own and has
  its own state.
- A key is stable across runs and across versions. It never holds a measured value, a date
  or a line number, so the same thing has the same key in every file and every run.

One example per area:

| Area | Example keys |
| --- | --- |
| `options` | `options:--ech`, `options:--ech:argument`, `options:-k:alias` |
| `protocols` | `protocols:mqtts` |
| `features` | `features:HTTP3` |
| `writeout` | `writeout:time_queue` |
| `exitcodes` | `exitcodes:101:strerror` |
| `environment` | `environment:NO_PROXY` |
| `behaviour` | `behaviour:test1234` |

A finding's group key, `<area>:<cause-slug>` (section 7), uses the same area names but is
not an item key: it names a cause, never one item.

## 4. Area measurement

`<run>/measurements/<area>.json`, written by the area's `Measure-*` tool. It gives every
inventory item of the targeted version exactly one state on one platform.

| Field | Type | Meaning |
| --- | --- | --- |
| `area` | string | The area name. |
| `targetVersion` | string | The version measured against. |
| `candidateCommit` | string | The full commit hash of the Curl tree measured. |
| `platform` | string | `windows`, `linux` or `macos`. |
| `reference` | string or `null` | The first line of the reference curl's `--version`, or `null` when no reference names the targeted version. |
| `referenceFallback` | string | Present only when `reference` is `null`, and then `"docs"`: answers came from the release's documents. |
| `measuredAt` | string | When the measurement finished, UTC time. |
| `items` | object[] | One entry per item, sorted by `key`. |
| `items[].key` | string | The item key. |
| `items[].state` | string | `match`, `gap`, `unmeasured` or `excluded`. |
| `items[].reason` | string or `null` | Required for `unmeasured` and `excluded`, from the area's vocabulary below; `null` otherwise. |
| `items[].expected` | string or `null` | What the reference or the documents answer. |
| `items[].actual` | string or `null` | What Curl answered. |
| `items[].evidence` | string or `null` | The command line, file or case that shows it, enough to reproduce a gap. |
| `items[].introducedIn` | string or `null` | Copied from the inventory. |
| `counts` | object | `match`, `gap`, `unmeasured`, `excluded`, `x`, `y`, all integers. |
| `reasons` | object | `behaviour` only: the number of items per reason, keyed by reason in ordinal order. Its values add up to `unmeasured + excluded`. |

`x` is `match`. `y` is `match + gap + unmeasured`. An `excluded` item counts in neither.

### Reasons

A reason is one of the words below for its area and state. A reason ending in `:<...>`
carries a value after the colon: `needs-server:<protocol>` is written `needs-server:smtp`,
`reference-lacks:<feature>` is written `reference-lacks:HTTP3`, and `platform:<os>` names
the platform the item belongs to, such as `platform:windows`. A tool that meets a case no
reason fits adds the reason here first.

Every area may use these two:

| Reason | State | Meaning |
| --- | --- | --- |
| `no-probe` | `unmeasured` | The office has no probe for this item yet. |
| `reference-lacks:<feature>` | `excluded` | The matched reference build on this platform lacks the feature or protocol, read from its `curl -V`. |

The rest, by area:

| Area | Reason | State | Meaning |
| --- | --- | --- | --- |
| `options` | `needs-server:<protocol>` | `unmeasured` | The option's effect shows only against a server the probe cannot stand up. |
| | `platform:<os>` | `excluded` | The option exists only on another platform. |
| | `debug-build-only` | `excluded` | The option needs a debug build of curl. |
| | `intended-difference:<ADR-
| `protocols` | `needs-server:<protocol>` | `unmeasured` | The scheme probe needs a server the probe cannot stand up. |
| `features` | `debug-build-only` | `excluded` | The feature appears only in a debug build (`Debug`, `TrackMemory`). |
| `writeout` | `needs-server:<protocol>` | `unmeasured` | The variable has a value only after a transfer to a server the probe cannot stand up. |
| | `no-reference` | `unmeasured` | The `:value` facet, when no reference names the targeted version: the documents give no value to compare. |
| `exitcodes` | `source-not-found` | `unmeasured` | `CurlExitCode` or `CurlEasyErrorText.cs` could not be read as source text. |
| | `obsolete-code` | `excluded` | Upstream marks the code obsolete and never returns it. |
| `environment` | `needs-server:<protocol>` | `unmeasured` | The variable acts only on a transfer to a server the probe cannot stand up. |
| | `platform:<os>` | `excluded` | The variable or config path applies only to another platform. |
| | `debug-build-only` | `excluded` | The variable is read only by a debug build. |
| `behaviour` | `needs-server:<protocol>` | `unmeasured` | The case needs a server the harness cannot stand up. |
| | `harness-unsupported` | `unmeasured` | The case uses a harness feature the in-process harness does not support. |
| | `unknown-variable` | `unmeasured` | The case uses a test-data variable the harness cannot expand. |
| | `timeout` | `unmeasured` | The case did not finish within the harness's time limit. |
| | `libcurl-api` | `excluded` | A libcurl API case (`<tool>lib...`). |
| | `libcurl-unit-test` | `excluded` | A libcurl unit test (`<tool>unit...`). |
| | `debug-build-only` | `excluded` | The case needs a debug build (`Debug`, `TrackMemory`, `unittest`). |
| | `platform:<os>` | `excluded` | The case runs only on another platform. |
| | `disabled-upstream` | `excluded` | The release's `tests/data/DISABLED` lists the case, so upstream's own test suite does not run it. |

```json
{
  "area": "options",
  "targetVersion": "8.21.0",
  "candidateCommit": "d15d115db0a1b2c3d4e5f60718293a4b5c6d7e8f",
  "platform": "windows",
  "reference": "curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel",
  "measuredAt": "2026-10-09T14:30:00Z",
  "items": [
    {
      "key": "options:--ech",
      "state": "gap",
      "reason": null,
      "expected": "exit 0",
      "actual": "curl: option --ech: is unknown (exit 2)",
      "evidence": "curl --ech true https://localhost:8443/",
      "introducedIn": "8.8.0"
    },
    {
      "key": "options:--insecure",
      "state": "match",
      "reason": null,
      "expected": "exit 0",
      "actual": "exit 0",
      "evidence": "curl -k https://localhost:8443/",
      "introducedIn": "7.10"
    },
    {
      "key": "options:--mail-from",
      "state": "unmeasured",
      "reason": "needs-server:smtp",
      "expected": null,
      "actual": null,
      "evidence": null,
      "introducedIn": "7.20.0"
    }
  ],
  "counts": { "match": 1, "gap": 1, "unmeasured": 1, "excluded": 0, "x": 1, "y": 3 }
}
```

With no matching reference, `reference` is `null` and `referenceFallback` is present:

```json
{
  "area": "writeout",
  "targetVersion": "8.21.0",
  "candidateCommit": "d15d115db0a1b2c3d4e5f60718293a4b5c6d7e8f",
  "platform": "linux",
  "reference": null,
  "referenceFallback": "docs",
  "measuredAt": "2026-10-09T14:31:00Z",
  "items": [
    {
      "key": "writeout:time_queue",
      "state": "match",
      "reason": null,
      "expected": "a number of seconds",
      "actual": "0.000012",
      "evidence": "curl -s -o /dev/null -w %{time_queue} http://127.0.0.1:8080/",
      "introducedIn": "8.21.0"
    }
  ],
  "counts": { "match": 1, "gap": 0, "unmeasured": 0, "excluded": 0, "x": 1, "y": 1 }
}
```

## 5. Release diff

`<run>/measurements/release-<new>.json`, written by `Compare-UpstreamReleases.ps1`
(BL-1735). It compares the upstream inventories of two versions, item by item and key by
key. An item present in both with equal `name`, `introducedIn` and `attributes` is not
listed.

| Field | Type | Meaning |
| --- | --- | --- |
| `fromVersion` | string | The older version, normally the targeted one. |
| `toVersion` | string | The newer version. |
| `items` | object[] | One entry per changed key, sorted by `key`. |
| `items[].key` | string | The item key. |
| `items[].change` | string | `added`, `removed` or `changed`. |
| `items[].before` | object or `null` | The item's inventory entry in `fromVersion`; `null` for `added`. |
| `items[].after` | object or `null` | The item's inventory entry in `toVersion`; `null` for `removed`. |

```json
{
  "fromVersion": "8.21.0",
  "toVersion": "8.22.0",
  "items": [
    {
      "key": "behaviour:test1234",
      "change": "changed",
      "before": { "key": "behaviour:test1234", "name": "test1234", "introducedIn": null, "attributes": { "number": 1234, "sha256": "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90", "keywords": ["HTTP"], "tool": null, "features": [] } },
      "after": { "key": "behaviour:test1234", "name": "test1234", "introducedIn": null, "attributes": { "number": 1234, "sha256": "ffeeddccbbaa99887766554433221100ffeeddccbbaa99887766554433221100", "keywords": ["HTTP"], "tool": null, "features": [] } }
    },
    {
      "key": "writeout:time_example",
      "change": "added",
      "before": null,
      "after": { "key": "writeout:time_example", "name": "time_example", "introducedIn": "8.22.0", "attributes": { "name": "time_example" } }
    }
  ]
}
```

## 6. Analyst report block

The last fenced `json` block of a gap analyst's reply. The run reads only that block.

| Field | Type | Meaning |
| --- | --- | --- |
| `analyst` | string | The agent's name, `gap-<area>`. |
| `area` | string | The area analysed. |
| `run` | string | The run stamp. |
| `groups` | object[] | One entry per cause. |
| `groups[].key` | string | The group key `<area>:<cause-slug>` (section 7): an existing finding's key when the group holds that finding's items, else a new key stable across runs. |
| `groups[].title` | string | The cause in one line, as a finding title. |
| `groups[].severity` | string | `Critical`, `High`, `Medium` or `Low` (section 7). |
| `groups[].introducedIn` | string or `null` | The version that introduced the cause's items, the oldest when they differ. |
| `groups[].items` | string[] | The item keys of the group. |
| `groups[].evidence` | string | What was measured and how to reproduce it. |
| `groups[].suggestion` | string | How to close the gap. |
| `groups[].touches` | string[] | The Curl project folders the fix would touch, such as `Curl.Console`. Never a `Gap/` path. |
| `notes` | string | Anything the analyst wants the run to know; empty when nothing. |

Every item the measurement marks `gap` belongs to exactly one group. No group holds an item
that is not a `gap`.

```json
{
  "analyst": "gap-options",
  "area": "options",
  "run": "2026-10-09_1430",
  "groups": [
    {
      "key": "options:encrypted-client-hello",
      "title": "Encrypted Client Hello options are missing",
      "severity": "High",
      "introducedIn": "8.8.0",
      "items": ["options:--ech", "options:--ech:argument"],
      "evidence": "curl --ech true https://localhost:8443/ exits 0 on the reference and 2 on Curl.",
      "suggestion": "Parse --ech and its four argument forms in Curl.Console and pass them to the TLS layer.",
      "touches": ["Curl.Console", "Curl.Console.UnitTests"]
    }
  ],
  "notes": ""
}
```

## 7. Gap finding

`Gap/Findings/GF-####-<slug>.md`, one file per cause, written from an analyst's group (or
from the release diff for `scope: newest`). `Gap/Findings/README.md` describes the folder and
`Gap/Findings/FINDING-TEMPLATE.md` is the file to copy.

Front matter, in this order:

| Field | Type | Meaning |
| --- | --- | --- |
| `id` | string | `GF-####`, four digits, never reused. |
| `title` | string | The cause in one line. |
| `area` | string | The area name. |
| `key` | string | The group key `<area>:<cause-slug>`, stable across runs, used to match a later run's group to this finding. |
| `severity` | string | `Critical`, `High`, `Medium` or `Low`. |
| `status` | string | `open`, `closed` or `rejected`. |
| `scope` | string | `target` or `newest`. |
| `introduced-in` | string | The version that introduced the cause, or empty when upstream does not say. |
| `opened` | string | The run stamp that filed it. |
| `closed` | string | The run stamp that closed it, or empty. |
| `regression` | boolean | `true` once a closed finding has reopened. |
| `items` | list | The item keys. |
| `touches` | list | The Curl project folders a fix would touch. Never a `Gap/` path. |
| `task` | string | The open task working on it, `BL-####`, or empty. |
| `tasks` | list | Every task ever filed for it, oldest first. |

Severity (ADR-0433 decision 3): **Critical** when an exit code or the output bytes differ
on a common path (a plain GET, a POST, `-o`, `-L`, `-u`). **High** when an option, scheme,
feature, write-out variable or exit code is missing outright. **Medium** when an argument
form, alias, message text or environment behaviour differs. **Low** for anything else.

Sections, in this order: `Summary`, `Evidence`, `Suggestion`, `Measurements` (one line per
run: the stamp and how many of the items are still gaps), `Log`.

Statuses and closing (ADR-0433 decisions 4 and 5):

- A finding is filed `open`, which means accepted. Claude files its tasks.
- It closes only when a run measures every one of its items as `match`, or as `excluded`
  with a reason the run states. It never closes because its task reached Done.
- A `closed` finding whose item measures `gap` again reopens: `status: open`,
  `regression: true`, `closed` emptied, and the run that saw it in the Log.
- Only Stewart sets `rejected`. A rejected finding gets no new tasks.
- There is no won't-fix status.

```markdown
---
id: GF-0001
title: Encrypted Client Hello options are missing
area: options
key: options:encrypted-client-hello
severity: High
status: open
scope: target
introduced-in: 8.8.0
opened: 2026-10-09_1430
closed:
regression: false
items: [options:--ech, options:--ech:argument]
touches: [Curl.Console, Curl.Console.UnitTests]
task: BL-1800
tasks: [BL-1800]
---
# GF-0001 - Encrypted Client Hello options are missing

## Summary

Curl does not know `--ech`, which curl 8.8.0 added.

## Evidence

`curl --ech true https://localhost:8443/` exits 0 on the reference and 2 on Curl.

## Suggestion

Parse `--ech` and its four argument forms in Curl.Console and pass them to the TLS layer.

## Measurements

- 2026-10-09_1430: 2 of 2 items are gaps.

## Log

- 2026-10-09_1430: Opened by gap-options.
- 2026-10-09_1430: Filed BL-1800.
```

## 8. Scorecard and history

### Scorecard, `Gap/Scorecards/<yyyy-MM-dd_HHmm>.md`

One per run, named by its run stamp. `Gap/Scorecards/README.md` describes the folder. Its
fixed sections, in this order:

| Section | Holds |
| --- | --- |
| `Run` | The candidate commit, the platform, the targeted version, the newest version, and the reference (its `--version` first line, or "documents" for a fallback). |
| `Scores` | A table, one row per area and an overall row: X, Y, the percentage, unmeasured, excluded, and the change in percentage points since the last run on the same platform. |
| `Against the newest version` | The same table computed as in section 10, and the release diff's version. |
| `New gaps` | The findings this run opened, with id, title and severity. |
| `Closed` | The findings this run closed. |
| `Regressions` | The findings this run reopened. |
| `Unmeasured by reason` | A table of area, reason and count, for `unmeasured` and `excluded` items. |

A section with nothing to list says `None.`

```markdown
# Gap scorecard 2026-10-09_1430

## Run

- Commit: d15d115db0a1b2c3d4e5f60718293a4b5c6d7e8f
- Platform: windows
- Target: 8.21.0
- Newest: 8.22.0
- Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel

## Scores

| Area | X | Y | % | Unmeasured | Excluded | Change |
| --- | --- | --- | --- | --- | --- | --- |
| options | 250 | 260 | 96.2 | 4 | 2 | +1.5 |
| overall | 250 | 260 | 96.2 | 4 | 2 | +1.5 |

## Against the newest version

Release 8.22.0.

| Area | X | Y | % |
| --- | --- | --- | --- |
| options | 249 | 263 | 94.7 |
| overall | 249 | 263 | 94.7 |

## New gaps

- GF-0001 Encrypted Client Hello options are missing (High)

## Closed

None.

## Regressions

None.

## Unmeasured by reason

| Area | Reason | Count |
| --- | --- | --- |
| options | needs-server:smtp | 4 |
| options | platform:linux | 2 |
```

### History, `Gap/Scorecards/history.json`

An array with one entry per run, oldest first. Each run appends one.

| Field | Type | Meaning |
| --- | --- | --- |
| `stamp` | string | The run stamp. |
| `commit` | string | The candidate commit. |
| `platform` | string | `windows`, `linux` or `macos`. |
| `targetVersion` | string | The targeted version. |
| `newestVersion` | string | The newest version. |
| `areas` | object | One property per measured area: `{ "x": int, "y": int }`. |
| `overall` | object | `{ "x": int, "y": int }`, the sums across the areas. |
| `newest` | object | `{ "x": int, "y": int }`, the overall score against the newest version. |

```json
[
  {
    "stamp": "2026-10-09_1430",
    "commit": "d15d115db0a1b2c3d4e5f60718293a4b5c6d7e8f",
    "platform": "windows",
    "targetVersion": "8.21.0",
    "newestVersion": "8.22.0",
    "areas": { "options": { "x": 250, "y": 260 }, "writeout": { "x": 60, "y": 62 } },
    "overall": { "x": 310, "y": 322 },
    "newest": { "x": 309, "y": 326 }
  }
]
```

## 9. Dashboard data

`data.json`, written by `Export-GapDashboardData.ps1` (BL-1733) and published beside the
dashboard page.

| Field | Type | Meaning |
| --- | --- | --- |
| `generated` | string | When the file was written, UTC time. |
| `target` | string | The targeted version. |
| `newest` | string | The newest version. |
| `latest` | object | The last `history.json` entry. |
| `history` | object[] | The whole of `history.json`. |
| `open` | object[] | Every `open` finding. |
| `open[].id` | string | `GF-####`. |
| `open[].title` | string | The finding's title. |
| `open[].area` | string | Its area. |
| `open[].severity` | string | Its severity. |
| `open[].scope` | string | `target` or `newest`. |
| `open[].introducedIn` | string or `null` | Its `introduced-in`, or `null` when empty. |
| `open[].suggestion` | string | Its Suggestion section. |
| `open[].items` | integer | How many item keys it lists. |
| `open[].tasks` | string[] | Its `tasks`. |
| `open[].url` | string | The finding's page on GitHub. |
| `closed` | object[] | Findings closed in the last 5 runs, with the same fields as `open`. |
| `regressions` | object[] | Open findings with `regression: true`, with the same fields as `open`. |
| `release` | object or `null` | `{ "version": string, "newGaps": int }` for the newest release when it is newer than the target, or `null`. |

```json
{
  "generated": "2026-10-09T15:00:00Z",
  "target": "8.21.0",
  "newest": "8.22.0",
  "latest": {
    "stamp": "2026-10-09_1430",
    "commit": "d15d115db0a1b2c3d4e5f60718293a4b5c6d7e8f",
    "platform": "windows",
    "targetVersion": "8.21.0",
    "newestVersion": "8.22.0",
    "areas": { "options": { "x": 250, "y": 260 } },
    "overall": { "x": 250, "y": 260 },
    "newest": { "x": 249, "y": 263 }
  },
  "history": [
    {
      "stamp": "2026-10-09_1430",
      "commit": "d15d115db0a1b2c3d4e5f60718293a4b5c6d7e8f",
      "platform": "windows",
      "targetVersion": "8.21.0",
      "newestVersion": "8.22.0",
      "areas": { "options": { "x": 250, "y": 260 } },
      "overall": { "x": 250, "y": 260 },
      "newest": { "x": 249, "y": 263 }
    }
  ],
  "open": [
    {
      "id": "GF-0001",
      "title": "Encrypted Client Hello options are missing",
      "area": "options",
      "severity": "High",
      "scope": "target",
      "introducedIn": "8.8.0",
      "suggestion": "Parse --ech and its four argument forms in Curl.Console and pass them to the TLS layer.",
      "items": 2,
      "tasks": ["BL-1800"],
      "url": "https://github.com/StewartScottRogers/Curl/blob/gap/Gap/Findings/GF-0001-encrypted-client-hello-options-are-missing.md"
    }
  ],
  "closed": [],
  "regressions": [],
  "release": { "version": "8.22.0", "newGaps": 3 }
}
```

## 10. Against the newest version

The score against the newest version is computed from the targeted version's measurements
and the release diff (section 5); nothing is measured against the newest version itself.

For an inventory area (every area but `behaviour`):

- **Y** is the size of the newest version's inventory for the area, less its items the
  run's measurement marks `excluded`.
- **X** is the items the run measured `match` against the target whose key the release diff
  neither removes nor changes.
- Items the release diff adds count as gaps: in Y, never in X.

For `behaviour`: test cases the release diff adds or changes count as `unmeasured` (in Y,
not in X) until a run targets that version. Removed cases leave both X and Y. The rest keep
their target measurement.

The overall score against the newest version is the sum of X over the sum of Y across the
areas, and is recorded as `newest` in `history.json`.
