param([string]$Assembly,[string]$Python,[string]$Fixture,[string]$Marker,[switch]$BeforeResume)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -Path $Assembly
if($BeforeResume){[LauncherHarness]::AbruptBeforeResume($Python,$Fixture,$Marker)}
else{[LauncherHarness]::AbruptOwner($Python,$Fixture,$Marker)}
