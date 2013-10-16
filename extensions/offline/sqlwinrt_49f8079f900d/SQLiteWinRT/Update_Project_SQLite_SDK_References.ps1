# Brian Wilhite
# bcwilhite@live.com
# TechEd 2013 NA June 5th, 2013

$FolderPath = "C:\Program Files (x86)\Microsoft SDKs\Windows\v8.0\ExtensionSDKs\SQLite.WinRT"
$FilePath   = ".\SQLiteWinRT.vcxproj"
$VersionNumber = Get-ChildItem -Path $FolderPath | Where-Object {$_.Extension -ne ".deleteme"} | Select-Object -ExpandProperty Name
$FileContents = Get-Content -Path $FilePath
$FileContents = $FileContents | ForEach-Object {$_ -replace '<SDKReference Include="SQLite.WinRT, Version=(\d.?)+', "<SDKReference Include=`"SQLite.WinRT, Version=$VersionNumber"}
$FileContents = $FileContents | ForEach-Object {$_ -replace '<Import Project="\$\(MSBuildProgramFiles32\)\\Microsoft SDKs\\Windows\\v8.0\\ExtensionSDKs\\SQLite.WinRT\\(\d.?)+', "<Import Project=`"`$(MSBuildProgramFiles32)\Microsoft SDKs\Windows\v8.0\ExtensionSDKs\SQLite.WinRT\$VersionNumber"}
$FileContents | Out-File $FilePath -Encoding ascii -Force