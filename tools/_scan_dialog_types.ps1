$path = 'D:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll'
$text = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($path))
[regex]::Matches($text, 'N[A-Za-z0-9_]*(Dialog|Popup|Modal|YesNo|Confirm)[A-Za-z0-9_]*') | ForEach-Object { $_.Value } | Sort-Object -Unique | Select-Object -First 50
