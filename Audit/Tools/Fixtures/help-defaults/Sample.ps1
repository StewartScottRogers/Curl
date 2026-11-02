<#
.SYNOPSIS
    Fixture for Find-HelpDefaultMismatches.ps1 -SelfTest. Nothing here is real, and nothing runs it.

.DESCRIPTION
    Up to -Lanes lanes (default 4) run at once, each costing at most -TaskBudgetUsd (default $10;
    0 for none). A -Limit (default 7) caps them although the code gives no default.
    The -Seed (default 0) is computed in code, so it cannot be checked.
    Every -HeartbeatMinutes (default 5; 0 turns publishing off) the board is published.

.PARAMETER Planted
    Defects to plant. Default 10.

.PARAMETER Ref
    The commit. Default origin/work/dark-factory.

.PARAMETER Name
    The branch name. Default: none, which the code contradicts.

.PARAMETER Quiet
    A switch. Default 1.
#>
param(
    [int]$Lanes = 4,
    [double]$TaskBudgetUsd = 10,
    [int]$Limit,
    [int]$Seed = $(Get-Random),
    [int]$HeartbeatMinutes = 3,
    [int]$Planted = 8,
    [string]$Ref = 'origin/work/dark-factory',
    [string]$Name = 'main',
    # Retries before giving up (default 4).
    [int]$Retries = 2,
    # Pauses between tries; default 1.
    [int]$PauseSeconds = 1,
    [switch]$Quiet
)

throw 'A fixture: never run.'
