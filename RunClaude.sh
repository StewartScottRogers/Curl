#!/usr/bin/env bash
# Linux and macOS twin of RunClaude.cmd.
# Always run the best available coding model. "opus" is a moving alias that Claude
# Code resolves to the newest Opus release at launch, so this file does not go stale
# when a newer model ships. Honour CLAUDE_MODEL if the caller set one (e.g.
# hrdrClaudeNative.sh forwards it into the herdr pane), which allows pinning an exact
# id such as claude-opus-5-5 or claude-sonnet-5.
cd "$(dirname "${BASH_SOURCE[0]}")" || exit 1
exec claude --model "${CLAUDE_MODEL:-opus}" --dangerously-skip-permissions --verbose
