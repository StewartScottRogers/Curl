<#
.SYNOPSIS
    Fixture for Find-HelpDefaultMismatches.ps1 -SelfTest: a script with no param block, which is
    not scanned although its help says -Count (default 3). Nothing runs it.
#>
throw 'A fixture: never run.'
