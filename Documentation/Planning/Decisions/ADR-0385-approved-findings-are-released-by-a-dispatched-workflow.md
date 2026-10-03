# ADR-0385: Approved audit findings are released by a dispatched workflow

- Status: Accepted
- Date: 2026-10-02
- Task: BL-1249
- Decided by Claude under Stewart's delegation.

## Context

Stewart asked for buttons on the board page's Audit tab that turn his approved findings into Curl
tasks without a Claude session. The filing logic lives in one script on the `audit` branch
(`New-TasksFromAcceptedFindings.ps1`); the page is a static file that talks to the GitHub REST API
with a fine-grained token kept in the browser.

## Decision

1. The page never files tasks itself. Its "Release approved findings (N)" button and each approved
   card's "Release" button send one `workflow_dispatch` to `release-approved-findings.yml` on
   `master`, input `findings` empty (all) or the card's ID, with `return_run_details` so it can link
   the run. The workflow runs the script on `windows-latest`, previews it with `-WhatIf` into the job
   summary, commits the tasks to `work/dark-factory` (rebase, `task-board.ps1 dedupe` against the
   upstream commit, three tries), rewrites any renumbered ID in its finding, commits the findings to
   `audit`, and opens, never merges, the audit pull request.
2. The workflow passes `-Id` only when the script declares it (checked with `Get-Command` at run
   time), and otherwise releases every approved finding with a warning, so it works before and after
   the script gains that filter. It refuses to run if the script has no `-WhatIf`.
3. A 401 from the dispatch marks the token refused, as on every other request; a 403 does not, since
   it means a token without Actions: read and write that still moves findings. Both show the steps to
   edit the token. A 404 means the workflow is not on `master` yet.
4. The token needs Actions: read and write besides Contents: read and write; the page's renewal steps
   name both.

## Alternatives considered

- Filing tasks from the page through the contents API: a second implementation of the script's
  numbering and front matter, and no dedupe against parallel lanes.
- A token in the workflow (repository secret) instead of `GITHUB_TOKEN`: a secret to rotate for no
  gain; `GITHUB_TOKEN` can push both branches and open the pull request. Its one cost is that a pull
  request it opens does not start CI by itself, so the merging session reruns CI if needed.
