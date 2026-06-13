Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$settingsDir = Join-Path $env:APPDATA "SlayTheSpire2\mods\DwellTargeting"
$settingsFile = Join-Path $settingsDir "settings.json"
New-Item -ItemType Directory -Force -Path $settingsDir | Out-Null

$hideEndTurn = $false
if (Test-Path $settingsFile) {
    try {
        $json = Get-Content $settingsFile -Raw | ConvertFrom-Json
        if ($null -ne $json.hideEndTurnButton) {
            $hideEndTurn = [bool]$json.hideEndTurnButton
        }
    }
    catch {
        [System.Windows.Forms.MessageBox]::Show("Could not read settings file. Defaults will be used.")
    }
}

$form = New-Object System.Windows.Forms.Form
$form.Text = "Dwell Targeting Settings"
$form.Size = New-Object System.Drawing.Size(560, 300)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox = $false
$form.MinimizeBox = $false

$title = New-Object System.Windows.Forms.Label
$title.Text = "Dwell Targeting"
$title.Font = New-Object System.Drawing.Font("Segoe UI", 18, [System.Drawing.FontStyle]::Bold)
$title.AutoSize = $true
$title.Location = New-Object System.Drawing.Point(24, 16)
$form.Controls.Add($title)

$chk = New-Object System.Windows.Forms.CheckBox
$chk.Text = "Hide End Turn button during combat"
$chk.Checked = $hideEndTurn
$chk.AutoSize = $true
$chk.Font = New-Object System.Drawing.Font("Segoe UI", 14)
$chk.Location = New-Object System.Drawing.Point(24, 64)
$form.Controls.Add($chk)

$hint = New-Object System.Windows.Forms.Label
$hint.Text = "When checked, the mod E END overlay stays hidden. Use the game's End Turn button instead."
$hint.Font = New-Object System.Drawing.Font("Segoe UI", 11)
$hint.AutoSize = $false
$hint.Size = New-Object System.Drawing.Size(500, 48)
$hint.Location = New-Object System.Drawing.Point(44, 96)
$form.Controls.Add($hint)

$save = New-Object System.Windows.Forms.Button
$save.Text = "SAVE"
$save.Size = New-Object System.Drawing.Size(180, 64)
$save.Font = New-Object System.Drawing.Font("Segoe UI", 16, [System.Drawing.FontStyle]::Bold)
$save.Location = New-Object System.Drawing.Point(24, 168)
$save.Add_Click({
    $payload = @{ hideEndTurnButton = $chk.Checked }
    $payload | ConvertTo-Json | Set-Content $settingsFile -Encoding UTF8
    [System.Windows.Forms.MessageBox]::Show("Saved.`n`nIf STS2 is running, changes apply within about a second.")
})
$form.Controls.Add($save)

$close = New-Object System.Windows.Forms.Button
$close.Text = "CLOSE"
$close.Size = New-Object System.Drawing.Size(180, 64)
$close.Font = New-Object System.Drawing.Font("Segoe UI", 16, [System.Drawing.FontStyle]::Bold)
$close.Location = New-Object System.Drawing.Point(220, 168)
$close.Add_Click({ $form.Close() })
$form.Controls.Add($close)

[void]$form.ShowDialog()
