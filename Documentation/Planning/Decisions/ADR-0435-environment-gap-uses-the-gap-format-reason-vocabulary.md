# ADR-0435 — The environment gap tool uses Gap-Format's reasons and measures through the probe's temporary homes

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-1727 asked `Gap/Tools/Measure-EnvironmentGap.ps1` to mark items with no recipe
`unmeasured` with `no-recipe`, items without a usable reference `no-reference`, and
another platform's config locations `excluded` with `other-platform`. None of those
three is in `Gap/Instructions/Gap-Format.md`'s reason vocabulary. That vocabulary
already has `no-probe` ("the office has no probe for this item yet") and
`platform:<os>`, and the dashboard counts reasons by name.

## Decision

1. An item with no recipe is `unmeasured` with `no-probe`. So is an item whose recipe
   has neither a matching reference nor a result the document states.
2. A config location for another platform only is `excluded` with `platform:windows`
   (Windows-only locations, off Windows) or `platform:unix` (the non-Windows getpwuid
   location, on Windows).
3. The executable-folder location (config.md item 8) is measured with copies of both
   binaries in a temporary folder, so no `.curlrc` is ever written beside an installed
   curl.
4. The proxy recipes set the variables in the recorder's process environment, clear
   every proxy variable first and point every home variable at an empty temporary
   folder. They restore the environment afterwards.

## Consequences

The measurement stays valid against Gap-Format.md without changing the format.
`no-probe` counts against the score, as ADR-0433 decision 2 requires for missing
recipes.
