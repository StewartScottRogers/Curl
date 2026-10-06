<#
.SYNOPSIS
    Reads the TRX files an Integration test run wrote and writes one platform's result:
    a JSON file for the README badge and, on a runner, a Markdown job summary.

.DESCRIPTION
    Counts every test result in every *.trx file under -ResultsDirectory as passed, failed
    or not run, and names each failed test with its project. The JSON goes to -JsonPath;
    when GITHUB_STEP_SUMMARY is set the same result is appended to it as Markdown. Exits 0
    whatever the tests did: whether a failure fails CI is the workflow's decision
    (INTEGRATION_FAILURES_FAIL_THE_RUN in .github/workflows/integration.yml), not this
    script's.

.EXAMPLE
    ./Write-IntegrationTestSummary.ps1 -ResultsDirectory TestResults -Platform Linux -JsonPath linux.json
#>
param(
    [Parameter(Mandatory)] [string] $ResultsDirectory,
    [Parameter(Mandatory)] [string] $Platform,
    [Parameter(Mandatory)] [string] $JsonPath,
    [string] $Commit = $env:GITHUB_SHA,
    [string] $RunUrl = $(if ($env:GITHUB_RUN_ID) { "$env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID" } else { '' })
)
$ErrorActionPreference = 'Stop'

$passed = 0
$failed = 0
$notRun = 0
$failedTests = [System.Collections.Generic.List[string]]::new()
$projectsWithTests = 0
$trxFiles = @(if (Test-Path $ResultsDirectory) { Get-ChildItem $ResultsDirectory -Recurse -Filter '*.trx' })
foreach ($trx in $trxFiles) {
    [xml] $document = Get-Content -LiteralPath $trx.FullName -Raw
    $project = [System.IO.Path]::GetFileNameWithoutExtension(($document.TestRun.TestDefinitions.UnitTest | Select-Object -First 1).storage)
    $results = @($document.TestRun.Results.UnitTestResult | Where-Object { $_ })
    if ($results.Count -gt 0) { $projectsWithTests++ }
    foreach ($result in $results) {
        switch ($result.outcome) {
            'Passed' { $passed++ }
            'Failed' { $failed++; $failedTests.Add("$project`: $($result.testName)") }
            default { $notRun++ }
        }
    }
}

$summary = [ordered]@{
    platform    = $Platform
    commit      = $Commit
    runUrl      = $RunUrl
    measuredAt  = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    resultFiles = $trxFiles.Count
    projects    = $projectsWithTests
    passed      = $passed
    failed      = $failed
    notRun      = $notRun
    failedTests = @($failedTests)
}
$summary | ConvertTo-Json -Depth 3 | Set-Content -Path $JsonPath -Encoding utf8

if ($env:GITHUB_STEP_SUMMARY) {
    $lines = @("### Integration tests on $Platform", '',
        "$passed passed, $failed failed, $notRun not run, across the $projectsWithTests test projects that have Integration tests.")
    if ($trxFiles.Count -eq 0) { $lines += '', 'No results: the run wrote no TRX file, so it did not get as far as running tests.' }
    if ($failed -gt 0) { $lines += '', 'Failed:', ''; $lines += $failedTests | ForEach-Object { "- ``$_``" } }
    if ($env:INTEGRATION_FAILURES_FAIL_THE_RUN -ne 'true') { $lines += '', '*These results do not fail the run (INTEGRATION_FAILURES_FAIL_THE_RUN is not true).*' }
    $lines | Add-Content -Path $env:GITHUB_STEP_SUMMARY -Encoding utf8
}
"Integration tests on ${Platform}: $passed passed, $failed failed, $notRun not run."
$failedTests | ForEach-Object { "  FAILED $_" }
exit 0
