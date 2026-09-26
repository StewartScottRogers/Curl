# Curl — Documentation

This folder is the `Documentation` shared project (`Documentation.shproj`), loaded
by `Curl.slnx`. It produces no build output; it exists so the product and planning
material is visible and editable from Solution Explorer rather than living as loose
files nobody opens.

## Layout

| Path | Holds |
| --- | --- |
| `Product/` | What Curl is and what it must do — overview, requirements. |
| `Planning/` | How and when it gets built — roadmap and decisions. Work items live on the task board in `Tasks/`, not here. |
| `Planning/Decisions/` | Architecture Decision Records (ADRs), one file per decision. |

## Adding a document

Drop a `.md` (or `.puml` / `.drawio`) file anywhere under this folder. The item
globs in `Documentation.projitems` are recursive, so it appears in Solution
Explorer on the next project reload — no project file edit needed.

## Status of these documents

The files under `Product/` and `Planning/` are **scaffolds**: correct structure,
headings and prompts, but the product-specific content is not filled in. They were
created alongside the solution and deliberately contain no invented requirements,
dates or scope. Sections awaiting real content are marked `> **TODO**`.

`Planning/Decisions/ADR-0001` is the exception — it records an actual decision and
is complete.
