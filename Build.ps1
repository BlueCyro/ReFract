
param (
    [switch] $NoRun = $false,
    [switch] $ReleaseMode = $false
)

$BinaryLocation = "Debug"
if($ReleaseMode)
{
    $BinaryLocation = "Release"
    dotnet publish -c Release
}
else 
{
    dotnet build    
}

if($NoRun)
{
    exit
}

$dir = Split-Path -Path (Get-Location) -Leaf
$NeosDir = "C:\Program Files (x86)\Steam\steamapps\common\NeosVR\"
$NeosExe = "$NeosDir\Neos.exe"
$AssemblyLocation = "$(Get-Location)\bin\$BinaryLocation\net4.7.2\$dir.dll"
$LogFolder = "$NeosDir\Logs\"
$nml_mods = "$NeosDir\nml_mods\"

Copy-Item -Force -Path $AssemblyLocation -Destination $nml_mods

$LogJob = Start-Job {Start-Sleep -Seconds 8
    $Path = $(Get-ChildItem -Path $using:LogFolder | Sort-Object LastWriteTime | Select-Object -last 1)
    $Path | Get-Content -Wait
}

$NeosProc = Start-Process -FilePath $NeosExe -WorkingDirectory $NeosDir -ArgumentList "-DontAutoOpenCloudHome", "-SkipIntroTutorial", "-Screen", "-LoadAssembly `"$NeosDir\Libraries\NeosModLoader.dll`"" -passthru

while(!$NeosProc.HasExited) {
    Start-Sleep -Seconds 1
    Receive-Job $LogJob.Id
}

Stop-Job $LogJob.Id