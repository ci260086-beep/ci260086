# Runs EditMode tests, then plays the game in the Editor and writes screenshots.
# Usage: tools\check.ps1 -Stage stage1
param(
    [Parameter(Mandatory = $true)][string]$Stage,
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.85f1\Editor\Unity.exe'
)

$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path $env:TEMP 'deepdrill-check'
Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $work | Out-Null

$p = Start-Process -FilePath $Unity -PassThru -ArgumentList "-batchmode -nographics -projectPath $root -logFile $root\Logs\tests.log -runTests -testPlatform EditMode -testResults $root\Logs\tests.xml"
if (-not $p.WaitForExit(240000)) { Write-Output 'TESTS TIMED OUT'; exit 1 }
Select-String -Path "$root\Logs\tests.log" -Pattern 'error CS' | Select-Object -First 15 | ForEach-Object { $_.Line }
$run = ([xml](Get-Content "$root\Logs\tests.xml")).'test-run'
Write-Output "tests total=$($run.total) passed=$($run.passed) failed=$($run.failed)"
([xml](Get-Content "$root\Logs\tests.xml")).SelectNodes("//test-case[@result='Failed']") | ForEach-Object { "FAIL $($_.name): $($_.failure.message.InnerText)" }
if ($run.failed -ne '0') { exit 1 }

$p = Start-Process -FilePath $Unity -PassThru -ArgumentList "-projectPath $root -logFile $root\Logs\play.log -executeMethod DeepDrill.Editor.ProjectSetup.PlayAndCapture --shots $work\shots --save $work\save.json"
if (-not $p.WaitForExit(240000)) { Write-Output "PLAY TIMED OUT pid=$($p.Id)"; exit 1 }
Select-String -Path "$root\Logs\play.log" -Pattern '\[demo\]|error CS|Exception' | Select-Object -First 20 | ForEach-Object { $_.Line }
Get-ChildItem "$work\shots" -Filter *.png | ForEach-Object {
    Copy-Item $_.FullName (Join-Path "$root\Screenshots" "$Stage`_$($_.Name)") -Force
}
Write-Output "shots: $((Get-ChildItem "$root\Screenshots" -Filter "$Stage`_*").Name -join ', ')"
if (Test-Path "$work\save.json") { Get-Content "$work\save.json" -TotalCount 12 }
