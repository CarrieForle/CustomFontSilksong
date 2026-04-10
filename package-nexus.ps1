# Used for Nexux CD. Pass BUILD_DIR envvar for dll location

$name = "CarrieForle-CustomFont"
$modUrl = "https://thunderstore.io/package/download/silksong_modding/I18N/1.0.3/"
$licenseUrl = "https://raw.githubusercontent.com/silksong-modding/Silksong.I18N/refs/heads/main/LICENSE"
$depName = "silksong_modding-I18N"
$binDir = "$PSScriptRoot/bin"
$nexusDir = "$binDir/nexus"
$bepinExDir = "$nexusDir/BepInEx/plugins"
$myDir = "$bepinExDir/$name"

if (!($?))
{
	exit 1
}

if (Test-Path $binDir) 
{ 
	Remove-Item $binDir -Recurse -Force 
}

if (!(Test-Path $bepinExDir)) 
{ 
	New-Item -ItemType Directory $bepinExDir
}

if (!(Test-Path $myDir))
{
	New-Item -ItemType Directory $myDir
}

$depPath = "$bepinExDir/$depName"
$zipPath = "$binDir/$depName.zip"
Invoke-WebRequest -Uri $modUrl -OutFile $zipPath
Expand-Archive -Path $zipPath -DestinationPath $depPath
Remove-Item $zipPath
Invoke-WebRequest -Uri $licenseUrl -OutFile "$depPath/LICENSE"

Copy-Item "$env:BUILD_DIR/*" -Recurse $myDir
Compress-Archive -Path "$nexusDir/*" -DestinationPath "$nexusDir/$name.zip"