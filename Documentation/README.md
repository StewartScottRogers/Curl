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
| `Wiki/` | Cross-cutting explanations and the glossary: one term, one meaning, one name in code. Kept in the repository, not the GitHub Wiki, so it changes with the code. Owned by the `align-and-document` agent. |

## Adding a document

Drop a `.md` (or `.puml` / `.drawio`) file anywhere under this folder. The item
globs in `Documentation.projitems` are recursive, so it appears in Solution
Explorer on the next project reload — no project file edit needed.

## Status of these documents

The files under `Product/` and `Planning/` are filled in and kept current with the
code:

- `Product/Requirements.md` lists the functional requirements (`FR-` rows), each
  naming the built behaviour and the tests that show it.
- `Planning/Decisions/` holds one Architecture Decision Record per decision
  (`ADR-*.md`), almost all of them Accepted; its `README.md` indexes them.
- `Planning/Roadmap.md` and `Product/Product-Overview.md` describe the plan and
  the product.

A section still awaiting real content is marked `> **TODO**` (for example, the
non-functional requirement `NFR-001` in `Product/Requirements.md`); search for that
marker to find what is left.
