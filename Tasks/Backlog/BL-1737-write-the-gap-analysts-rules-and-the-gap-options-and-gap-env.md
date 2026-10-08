---
id: BL-1737
title: Write the gap analysts' rules and the gap-options and gap-environment analysts
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1720, BL-1723, BL-1727]
touches: [Gap/Instructions/Analyst-Rules.md, Gap/Instructions/Options.md, Gap/Instructions/Environment.md, .claude/agents/gap-options.md, .claude/agents/gap-environment.md]
requirement: none
created: 2026-10-08
completed:
---
# BL-1737 — Write the gap analysts' rules and the gap-options and gap-environment analysts

## Goal

`Gap/Instructions/Analyst-Rules.md` holds the rules every gap analyst follows. The first two
analysts exist: `.claude/agents/gap-options.md` and `.claude/agents/gap-environment.md`,
both Sonnet, each with its method in `Gap/Instructions/Options.md` and
`Gap/Instructions/Environment.md`.

## Context

This is ADR-0433 decision 3: one analyst agent per area, with the model chosen by cost. The
report block an analyst ends with, and the finding fields its groups become, are in
`Gap/Instructions/Gap-Format.md` (BL-1720). An analyst reads the measurement its area's tool
wrote: `Measure-OptionGap.ps1` (BL-1723) and `Measure-EnvironmentGap.ps1` (BL-1727).

The audit office's auditors are the pattern (`.claude/agents/audit-*.md` and
`Audit/Instructions/Auditor-Rules.md`), but a lane cannot open them, because the hook refuses
audit paths. What they establish is restated here.

**`Analyst-Rules.md` states these rules:**

1. Analyse only the run folder and the commit the prompt names. Never change a tracked file.
   Scratch work goes in the temporary folder the prompt names.
2. The measurement is the evidence. Never judge an item from memory of curl. A group's
   evidence quotes the items' `expected` and `actual`, and a reproduction a stranger can run
   from the repository root (one PowerShell command, usually the probe the tool ran).
3. Every `gap` item in the measurement belongs to exactly one group. Never drop or
   re-score an item: the state is the tool's, not the analyst's.
4. Reuse keys. The prompt lists the area's open and closed findings (key and items). A gap
   item already listed by a finding goes in a group with that finding's `key`. A new group
   gets a new key, `<area>:<cause-slug>`, that is stable across runs (no counts, dates or
   versions in it).
5. Each group has: `title` (what differs, in one line), `severity` (the scale in ADR-0433
   decision 3), `introducedIn` (the earliest of its items'), `suggestion` (what to change
   in Curl and where: name the project, type or file and, where it helps, the upstream
   document section; the suggestion is what the filed task's Goal is built from),
   `touches` (the Curl project folders the fix changes, never a `Gap/`, `Audit/` or
   `.claude/` path).
6. Two phases. Group and suggest from the measurement and Curl's code first. Only then read
   ADRs under `Documentation/Planning/Decisions/`, and only to annotate a group ("explained
   by ADR-NNNN: ..."). An ADR never removes a group. A gap a past ADR accepted is still a
   gap now, because the yardstick is upstream's release (ADR-0433 decision 1).
7. End with exactly one fenced `json` report block in `Gap-Format.md`'s analyst format,
   with the `run` and `area` the prompt gives.
8. No Python and no network beyond loopback.

**The agents.** Front matter in the shape of the other agents under `.claude/agents/`
(`name`, `description`, `tools: Read, Grep, Glob, Bash`, `model`). The body tells the agent
to read `Gap/Instructions/Analyst-Rules.md` first, then its own method, to follow them, and
to end with the report block.

- `gap-options` (`model: sonnet`). `Options.md`'s method covers grouping. Facets of one
  option go in one group when they share a cause. Options refused the same way (for
  example many options unknown to Curl) are grouped by the Curl component that parses them,
  so one task can close them. Suggestions name where `Curl.Cli.UnitLibrary` parses the
  option and remind the implementer to keep `--ai-help` right (CLAUDE.md).
- `gap-environment` (`model: sonnet`). `Environment.md`'s method covers grouping by
  mechanism: the proxy variables, the config-file locations and the config syntax. The
  `unmeasured` items are listed in the report's `notes` as recipes worth adding, and are
  never made into groups, because only measured gaps become findings.

## Acceptance criteria

- [ ] `Gap/Instructions/Analyst-Rules.md` states rules 1 to 8 above.
- [ ] `Gap/Instructions/Options.md` and `Gap/Instructions/Environment.md` each give their area's grouping method, the severity mapping for their items, and an example report block that parses with `ConvertFrom-Json` and satisfies `Gap-Format.md`.
- [ ] `.claude/agents/gap-options.md` and `.claude/agents/gap-environment.md` exist with `model: sonnet`, tools limited to `Read, Grep, Glob, Bash`, and a body that names their two instruction files and the report block.
- [ ] No file in this task tells an analyst to read anything under `Audit/`.

## Notes

## Log

- 2026-10-08: Created.
