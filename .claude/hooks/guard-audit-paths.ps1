<#
.SYNOPSIS
    Refuses a dark factory lane any tool call that reads or changes an audit path, and any
    Edit, Write or MultiEdit of one of the four guard files.

.DESCRIPTION
    A PreToolUse hook for Edit, Write, MultiEdit, NotebookEdit, Read, Grep, Glob, Bash and
    PowerShell, in every Claude Code session in this repository. It acts only inside a dark
    factory shift, where RunDarkFactory.ps1 sets CURL_DARK_FACTORY_LANE on every process
    (BL-995); anywhere else it exits 0 before reading its input, so interactive sessions
    are never affected. Lanes run claude -p --dangerously-skip-permissions, which skips
    permission rules but still runs hooks, and a lane's shell cannot unset the variable
    for the hook, whose process Claude Code starts.

    Audit paths are outside the factory's reach: the audit office's Audit folder and
    auditor agents .claude/agents/audit-* (ADR-0267), and the gap analysis office's Gap
    folder and gap analyst agents .claude/agents/gap-* (ADR-0433). Reading counts as well
    as changing: a lane that can read the planted-defect catalogue, the auditors'
    instructions or the gap office's yardstick can write code that passes them. It looks
    at:

      Edit, Write, MultiEdit   tool_input.file_path
      NotebookEdit             tool_input.notebook_path
      Read                     tool_input.file_path
      Grep                     tool_input.path, tool_input.glob, and the two joined
      Glob                     tool_input.pattern, tool_input.path, and the two joined
      Bash, PowerShell         tool_input.command: a path into Audit or Gap, a path to
                               an auditor or gap analyst agent, or the bare word Audit
                               or Gap as an argument

    A refused call exits 2 with the reason on standard error, which Claude Code shows the
    model instead of running the tool. It fails closed: inside a shift, input it cannot
    parse is refused. A false positive in a command (a commit message quoting "Audit/" or
    "Gap/") is accepted; the lane rewords it. The bare words are matched case-sensitively,
    so "audit" and "gap" in prose ("gap analysis") pass.

    Not covered: a Grep or Glob over a folder that contains Audit or Gap, such as the
    repository root, still searches it.
#>
if ([string]::IsNullOrEmpty($env:CURL_DARK_FACTORY_LANE)) { exit 0 }

$Refusal = 'Refused by the audit guard: dark factory lanes may not read or change Audit/, .claude/agents/audit-* (ADR-0267), Gap/ or .claude/agents/gap-* (ADR-0433), nor change the guard files (guard-audit-paths.ps1, settings.json, ci.yml, task-board.ps1). Leave this to an interactive session.'

# The guarded folders and the agent-name prefixes that go with them.
$GuardedFolders = 'Audit|Gap'
$GuardedAgents = 'audit|gap'

function Test-AuditFilePath([string]$Path) {
    # A file path or glob, either slash: into the Audit or Gap folder, or an auditor or
    # gap analyst agent.
    if (-not $Path) { return $false }
    $p = $Path.Replace([string][char]92, '/')
    return ($p -match "(?i)(^|/)($GuardedFolders)(/|$)") -or ($p -match "(?i)(^|/)\.claude/agents/($GuardedAgents)-[^/]*$")
}

function Test-GuardFilePath([string]$Path) {
    # One of the guard files that protect the audit paths. Lanes run and read them (every
    # lane runs task-board.ps1), so only a change to one is refused (BL-1876 slipped through).
    if (-not $Path) { return $false }
    $p = $Path.Replace([string][char]92, '/')
    return $p -match '(?i)(^|/)(\.claude/hooks/guard-audit-paths\.ps1|\.claude/settings\.json|\.github/workflows/ci\.yml|\.claude/skills/task-board/task-board\.ps1)$'
}

function Test-AuditCommand([string]$Command) {
    # A shell command naming a path into Audit or Gap, an auditor or gap analyst agent,
    # or Audit or Gap itself as an argument (case-sensitive, so the words "audit" and
    # "gap" in prose pass).
    if (-not $Command) { return $false }
    return ($Command -match "(?i)(^|[\s`"'=(:/\\])($GuardedFolders)[\\/]") -or
        ($Command -match "(?i)\.claude[\\/]agents[\\/]($GuardedAgents)-") -or
        ($Command -cmatch "(^|[\s`"'])($GuardedFolders)($|[\s`"';|&)])")
}

function Join-SearchPath([string]$Folder, [string]$Pattern) {
    if (-not $Folder -or -not $Pattern) { return '' }
    return $Folder.TrimEnd('/', [char]92) + '/' + $Pattern
}

try {
    $hook = [Console]::In.ReadToEnd() | ConvertFrom-Json -ErrorAction Stop
    if (-not $hook) { throw 'the hook input is empty' }
}
catch {
    [Console]::Error.WriteLine("$Refusal (The hook input could not be read: $($_.Exception.Message))")
    exit 2
}

$in = $hook.tool_input
$refuse = switch ("$($hook.tool_name)") {
    'Read' { Test-AuditFilePath "$($in.file_path)"; break }
    { $_ -in 'Edit', 'Write', 'MultiEdit' } { (Test-AuditFilePath "$($in.file_path)") -or (Test-GuardFilePath "$($in.file_path)"); break }
    'NotebookEdit' { Test-AuditFilePath "$($in.notebook_path)"; break }
    'Grep' {
        @("$($in.path)", "$($in.glob)", (Join-SearchPath "$($in.path)" "$($in.glob)")) |
            Where-Object { Test-AuditFilePath $_ } | Select-Object -First 1 | ForEach-Object { $true }
        break
    }
    'Glob' {
        @("$($in.pattern)", "$($in.path)", (Join-SearchPath "$($in.path)" "$($in.pattern)")) |
            Where-Object { Test-AuditFilePath $_ } | Select-Object -First 1 | ForEach-Object { $true }
        break
    }
    { $_ -in 'Bash', 'PowerShell' } { Test-AuditCommand "$($in.command)"; break }
    default { $false }
}

if ($refuse) {
    [Console]::Error.WriteLine($Refusal)
    exit 2
}
exit 0
