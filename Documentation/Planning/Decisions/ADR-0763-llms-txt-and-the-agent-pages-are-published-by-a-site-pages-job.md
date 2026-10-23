# ADR-0763 — llms.txt and the agent pages are published by a site-pages job

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3148
- Decided by Claude under Stewart's delegation; Stewart approved the plan 2026-10-22.

## Context

An AI agent starting from one known address should find every resource Curl offers, download the
right file, verify it and use it. The site is the `gource` branch, written only by
`.github/workflows/gource.yml`; `board-page` and `gaps-page` publish a page from a source folder.

## Decision

1. A `site-pages` job, beside those two, with its own concurrency group `gource-site`, on
   `work/dark-factory` and `master`, never gated on `decide`. It commits without force, only
   the files that changed, then asks for a Pages build.
2. Sources: the repository-root `llms.txt` (the single copy; the job copies it to the site
   root), `.github/site/mcp/index.html`, and `.github/site/download/index.html.template`.
3. `download/index.html` is rendered by `.github/site/New-DownloadPage.ps1` from the preview
   release's `resources.json`, so the page is the same table, plain HTML, no script.
4. `ai-help/` is made from the release's `ai-help.md`: `ai-help.md` raw, `index.md` and an
   `index.html` wrapping the text in an escaped `<pre>` (the site has `.nojekyll`, so Markdown is
   not rendered by Pages).
5. `llms.txt` names resource kinds and their use, not versioned file names; those change with
   every release and live in `resources.json`, which `llms.txt` points to first.
6. The `render` job carries `llms.txt`, `download/`, `mcp/` and `ai-help/` over when it
   force-pushes a new `gource` branch, as it does `board/` and `gaps/`.
7. The MCP page's Claude Desktop, Claude Code and Copilot shapes were written from knowledge of the
   clients' documentation, not fetched; the page says so.

## Consequences

The hourly run on master picks up each new preview release. Until the first run after merge, the
four URLs are not live.
