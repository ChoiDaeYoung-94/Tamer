param([string]$Checkout)
$ErrorActionPreference='Stop'
function Get-LauncherEditorState {
    param([Parameter(Mandatory=$true)][AllowEmptyCollection()][object[]]$Processes,[Parameter(Mandatory=$true)][string]$Wanted)
    $root=[IO.Path]::GetFullPath($Wanted).TrimEnd('\','/')
    $owned=0;$other=0;$cli=0
    foreach($entry in $Processes){
        if($entry.description -ceq 'unity' -and $entry.version -cmatch '^1\.0\.0-beta\.\d+$'){$cli++;continue}
        if($entry.description -cne 'Unity Editor' -or $entry.version -cnotmatch '^\d+\.\d+\.\d+[abfp]\d+(?:[._-][A-Za-z0-9._-]+)?$' -or
           [string]::IsNullOrWhiteSpace($entry.command)){throw 'Unknown Unity process identity'}
        $matchesFound=[regex]::Matches($entry.command,'(?i)(?:^|\s)"?-projectPath"?\s+(?:"([^"\r\n]+)"|([^\s"]+))(?=\s|$)')
        if($matchesFound.Count -ne 1){throw 'Unknown or ambiguous Editor project path'}
        $project=if($matchesFound[0].Groups[1].Success){$matchesFound[0].Groups[1].Value}else{$matchesFound[0].Groups[2].Value}
        if(![IO.Path]::IsPathFullyQualified($project) -or $project.StartsWith('\\') -or ($project -split '[\\/]') -contains '..'){
            throw 'Nonlocal Editor project path'
        }
        if([IO.Path]::GetFullPath($project).TrimEnd('\','/') -ieq $root){$owned++}else{$other++}
    }
    if($owned -ne 0){throw 'Owned checkout Editor is running'}
    # Never emit complete command lines, executable paths or credentials.
    return @{owned=$owned;other=$other;cli=$cli}
}
if($MyInvocation.InvocationName -ne '.'){
    if([string]::IsNullOrWhiteSpace($Checkout)){throw 'Explicit checkout required'}
    $records=@(foreach($entry in @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction Stop)){
        if(!$entry.ExecutablePath){throw 'Unknown Unity executable'}
        $info=[Diagnostics.FileVersionInfo]::GetVersionInfo($entry.ExecutablePath)
        @{description=$info.FileDescription;version=$info.ProductVersion;command=$entry.CommandLine}
    })
    $null=Get-LauncherEditorState -Processes $records -Wanted $Checkout
}
