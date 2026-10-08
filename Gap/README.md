# Gap analysis office

The gap analysis office measures how far Curl is from upstream curl, area by area, against
upstream curl's own release data, and narrows that distance over time (ADR-0433). Today this
folder holds only its project files (`Gap.shproj`, `Gap.projitems`) and this map; every row
below is planned.

| Path | What it holds | Built by |
| --- | --- | --- |
| `Instructions/` | The file formats, the analysts' rules, and one method per area | planned, BL-1720, BL-1737 to BL-1739 |
| `Baselines/` | The targeted upstream curl version and the pinned tarball hashes | planned, BL-1721 |
| `Upstream/<version>/` | Upstream curl's inventories, one per area, for that version | planned, BL-1723 to BL-1727 |
| `Findings/` | Gap findings, `GF-####-*.md` | planned, BL-1720, BL-1731 |
| `Scorecards/` | One scorecard per run, plus `history.json` | planned, BL-1720, BL-1732 |
| `Tools/` | The office's scripts | planned, BL-1721 to BL-1735 |
| `Triage.md` | How findings become tasks | planned, BL-1734 |
| `RunGapAnalysis.cmd`, `RunGapAnalysis.ps1` | The entry point that runs a gap analysis | planned, BL-1740, BL-1741 |
| `../.claude/agents/gap-*.md` | The gap analyst agents | planned, BL-1737 to BL-1739 |
