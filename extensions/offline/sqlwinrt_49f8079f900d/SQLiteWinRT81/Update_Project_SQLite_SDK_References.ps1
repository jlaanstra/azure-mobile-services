# Brian Wilhite
# bcwilhite@live.com
# TechEd 2013 NA June 5th, 2013

$FolderPath = "C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1\ExtensionSDKs\SQLite.WinRT81"
$FilePath   = ".\SQLiteWinRT81.vcxproj"
$VersionNumber = Get-ChildItem -Path $FolderPath | Where-Object {$_.Extension -ne ".deleteme"} | Select-Object -ExpandProperty Name
$FileContents = Get-Content -Path $FilePath
$FileContents = $FileContents | ForEach-Object {$_ -replace '<SDKReference Include="SQLite.WinRT81, Version=(\d.?)+', "<SDKReference Include=`"SQLite.WinRT81, Version=$VersionNumber"}
$FileContents = $FileContents | ForEach-Object {$_ -replace '<Import Project="\$\(MSBuildProgramFiles32\)\\Microsoft SDKs\\Windows\\v8.1\\ExtensionSDKs\\SQLite.WinRT81\\(\d.?)+', "<Import Project=`"`$(MSBuildProgramFiles32)\Microsoft SDKs\Windows\v8.1\ExtensionSDKs\SQLite.WinRT81\$VersionNumber"}
$FileContents | Out-File $FilePath -Encoding ascii -Force