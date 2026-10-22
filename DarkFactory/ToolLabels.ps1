# The short labels the trace gives a run's tool calls, and -TestToolLabels. Dot-sourced by
# RunDarkFactory.ps1 in its order; see the loader there.

function Get-ToolFileName {
    # The leaf of the file an Edit or Write call names: its file_path, else path or
    # notebook_path, else '' (BL-2036: BL-2035's Edit named its file as path, and Split-Path
    # of the empty file_path ended lane 1 with "Cannot bind argument to parameter 'Path'").
    param($ToolInput)
    foreach ($key in 'file_path', 'path', 'notebook_path') {
        $value = if ($ToolInput -and $ToolInput.PSObject.Properties[$key]) { "$($ToolInput.$key)" } else { '' }
        if ($value) { return Split-Path $value -Leaf }
    }
    return ''
}

function Get-ToolLabel {
    param($Tool)
    $in = $Tool.input
    switch ($Tool.name) {
        'Agent' { return @('agent', "$($in.subagent_type): $($in.description)") }
        'Task' { return @('agent', "$($in.subagent_type): $($in.description)") }
        'Skill' { return @('skill', "$($in.skill) $($in.args)") }
        'Edit' { return @('edit', (Get-ToolFileName $in)) }
        'Write' { return @('write', (Get-ToolFileName $in)) }
        { $_ -in 'Bash', 'PowerShell' } {
            $c = "$($in.command)"
            if ($c -match 'task-board\.ps1\s+move\b.*-To\s+(\w+)') { return @('move', $Matches[1]) }
            if ($c -match 'dotnet\s+build') { return @('build', '') }
            if ($c -match 'dotnet\s+test') { return @('test', '') }
            if ($c -match 'dotnet\s+format') { return @('format', '') }
            if ($c -match 'Measure-CodeQuality') { return @('quality', '') }
            if ($c -match 'git\s+commit') { return @('commit', '') }
            if ($c -match 'git\s+push') { return @('push', '') }
        }
    }
    return $null
}

if ($TestToolLabels) {
    $failed = 0
    $check = { param($Name, $Expected, $Got)
        if ($Expected -ceq $Got) { Write-Host "PASS ${Name}: $Got" -ForegroundColor Green }
        else { Write-Host "FAIL ${Name}: expected $Expected, got $Got" -ForegroundColor Red; $script:failed++ } }
    $tool = { param($Name, $Json) "$((Get-ToolLabel ([pscustomobject]@{ name = $Name; input = ($Json | ConvertFrom-Json) }))[1])" }
    & $check 'Edit naming file_path' 'A.cs' (& $tool 'Edit' '{"file_path":"Z:\\x\\A.cs"}')
    & $check 'Edit naming path (BL-2035)' 'B.cs' (& $tool 'Edit' '{"path":"Z:\\x\\B.cs","old_string":"a"}')
    & $check 'Write naming notebook_path' 'C.ipynb' (& $tool 'Write' '{"notebook_path":"/x/C.ipynb"}')
    & $check 'Edit naming no file' '' (& $tool 'Edit' '{}')
    & $check 'Edit with an empty file_path' '' (& $tool 'Edit' '{"file_path":""}')
    & $check 'Edit with no input at all' '' "$((Get-ToolLabel ([pscustomobject]@{ name = 'Edit'; input = $null }))[1])"
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
