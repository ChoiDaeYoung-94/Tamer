param(
    [Parameter(Mandatory)][string]$EmulatorPath,
    [string]$OutputPath = (Join-Path $PSScriptRoot '../../Logs/revival/host-16kb-readiness.json')
)
# Read-only host inventory. Does not enable Windows features, edit BCD, boot an AVD or reboot.
$ErrorActionPreference = 'Stop'
$hostReadinessFullOutput = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $hostReadinessFullOutput) { throw 'Choose a new output path; existing files are preserved.' }
$hostReadinessOs = Get-CimInstance Win32_OperatingSystem
$hostReadinessComputer = Get-CimInstance Win32_ComputerSystem
$hostReadinessCpu = @(Get-CimInstance Win32_Processor)
$hostReadinessFeatures = @(Get-CimInstance Win32_OptionalFeature -Filter "Name='HypervisorPlatform' OR Name='VirtualMachinePlatform' OR Name='Microsoft-Hyper-V-All'")
$hostReadinessServices = @(Get-CimInstance Win32_Service -Filter "Name='aehd' OR Name='gvm'")
$hostReadinessBcd = & bcdedit /enum '{current}' 2>&1
$hostReadinessBcdExit = $LASTEXITCODE
$hostReadinessLaunch = if ($hostReadinessBcdExit -eq 0) {
    (($hostReadinessBcd | Select-String 'hypervisorlaunchtype') -replace '^.*hypervisorlaunchtype\s+','')
} else { $null }
$hostReadinessAccel = & (Resolve-Path -LiteralPath $EmulatorPath).Path -accel-check 2>&1
$hostReadinessAccelExit = $LASTEXITCODE
$hostReadinessReport = [ordered]@{
    schema = 1
    observedUtc = [DateTime]::UtcNow.ToString('o')
    os = $hostReadinessOs.Caption
    build = $hostReadinessOs.BuildNumber
    hypervisorPresent = $hostReadinessComputer.HypervisorPresent
    cpu = @($hostReadinessCpu | Select-Object VirtualizationFirmwareEnabled,SecondLevelAddressTranslationExtensions,VMMonitorModeExtensions)
    features = @($hostReadinessFeatures | Select-Object Name,InstallState)
    alternativeDrivers = @($hostReadinessServices | Select-Object Name,State)
    bcdReadExitCode = $hostReadinessBcdExit
    hypervisorLaunchType = $hostReadinessLaunch
    accelerationExitCode = $hostReadinessAccelExit
    accelerationOutput = ($hostReadinessAccel -join "`n")
    changesApplied = $false
    rebootPerformed = $false
    runtime16KBVerified = $false
}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($hostReadinessFullOutput)) | Out-Null
$hostReadinessJson = $hostReadinessReport | ConvertTo-Json -Depth 6
$hostReadinessStream = [IO.File]::Open($hostReadinessFullOutput, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
try {
    $hostReadinessBytes = [Text.UTF8Encoding]::new($false).GetBytes($hostReadinessJson)
    $hostReadinessStream.Write($hostReadinessBytes, 0, $hostReadinessBytes.Length)
} finally { $hostReadinessStream.Dispose() }
$hostReadinessJson
