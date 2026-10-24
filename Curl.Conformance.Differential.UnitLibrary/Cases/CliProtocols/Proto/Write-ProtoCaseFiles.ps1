<#
.SYNOPSIS
Writes one differential case file per case in the tester's cases_proto.py (BL-3424).

.DESCRIPTION
Reads each case('name', 'command', ctl=PORT, files={...}, stdin=b'...') line of the tester's
cases_proto.py as text (it never runs Python) and writes <Server>/<name>.json beside this script,
where <Server> is the loopback server whose control port the case names (ctl): Ftp for 18200,
Smtp for 18210, Pop3 for 18220 and Imap for 18230. Each file holds one case in the shape
DifferentialCaseFileReader reads. The command line is kept exactly as the tester wrote it.

.PARAMETER CasesPyPath
The path to the tester's cases_proto.py.
#>
param(
    [Parameter(Mandatory)]
    [string]$CasesPyPath
)

$ErrorActionPreference = 'Stop'

$serverByControlPort = @{ 18200 = 'Ftp'; 18210 = 'Smtp'; 18220 = 'Pop3'; 18230 = 'Imap' }
$pythonString = "b?'(?:[^'\\]|\\.)*'|b?`"(?:[^`"\\]|\\.)*`""

function ConvertFrom-PythonString([string]$literal) {
    $body = $literal.TrimStart('b').Substring(1, $literal.TrimStart('b').Length - 2)
    [regex]::Replace($body, '\\(.)', {
            param($match)
            switch -CaseSensitive ($match.Groups[1].Value) {
                'n' { "`n" } 'r' { "`r" } 't' { "`t" } default { $match.Groups[1].Value }
            }
        })
}

$names = @{}
foreach ($line in Get-Content -LiteralPath $CasesPyPath) {
    if ($line -notmatch '^case\(') { continue }
    $strings = [regex]::Matches($line, $pythonString)
    $name = ConvertFrom-PythonString $strings[0].Value
    if ($names.ContainsKey($name)) { throw "Case $name appears twice." }
    $names[$name] = $true

    $case = [ordered]@{ name = $name; command = ConvertFrom-PythonString $strings[1].Value }
    if ($line -match "stdin=($pythonString)") { $case.stdin = ConvertFrom-PythonString $Matches[1] }
    if ($line -match 'files=\{(.*?)\}') {
        $files = [ordered]@{}
        $fileStrings = [regex]::Matches($Matches[1], $pythonString)
        for ($index = 0; $index -lt $fileStrings.Count; $index += 2) {
            $files[(ConvertFrom-PythonString $fileStrings[$index].Value)] = ConvertFrom-PythonString $fileStrings[$index + 1].Value
        }
        $case.files = $files
    }

    if ($line -notmatch 'ctl=(\d+)') { throw "Case $name names no control port." }
    $server = $serverByControlPort[[int]$Matches[1]]
    $folder = Join-Path $PSScriptRoot $server
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $json = "{`n  `"cases`": [`n    " + ($case | ConvertTo-Json -Compress -Depth 4) + "`n  ]`n}`n"
    [System.IO.File]::WriteAllText((Join-Path $folder "$name.json"), $json, [System.Text.UTF8Encoding]::new($false))
}

Write-Output "Wrote $($names.Count) case files."
