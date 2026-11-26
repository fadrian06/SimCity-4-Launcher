$ErrorActionPreference = 'Stop'

$projectDirectory = $PSScriptRoot
$sourcePath = Join-Path $projectDirectory 'Source\SimCity 4 Launcher.cs'
$outputPath = Join-Path $projectDirectory 'SimCity 4 Launcher.exe'
$iconPath = 'C:\Games\SimCity 4\SimCity4.ico'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}

if (-not (Test-Path -LiteralPath $compilerPath)) {
    throw 'No se encontró el compilador de .NET Framework (csc.exe).'
}

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "No se encontró el código fuente: $sourcePath"
}

if (-not (Test-Path -LiteralPath $iconPath)) {
    throw "No se encontró el icono para incrustar en el ejecutable: $iconPath"
}

$launcherName = [IO.Path]::GetFileNameWithoutExtension($outputPath)
$launcherProcesses = @(Get-Process -Name $launcherName -ErrorAction SilentlyContinue)
foreach ($launcherProcess in $launcherProcesses) {
    $processPath = $null
    try {
        $processPath = $launcherProcess.Path
    }
    catch {
    }

    if ($processPath -and -not [string]::Equals(
        [IO.Path]::GetFullPath($processPath),
        [IO.Path]::GetFullPath($outputPath),
        [StringComparison]::OrdinalIgnoreCase)) {
        continue
    }

    try {
        if (-not $launcherProcess.CloseMainWindow() -or -not $launcherProcess.WaitForExit(5000)) {
            Stop-Process -Id $launcherProcess.Id -Force -ErrorAction Stop
        }
    }
    catch {
        if (Get-Process -Id $launcherProcess.Id -ErrorAction SilentlyContinue) {
            Stop-Process -Id $launcherProcess.Id -Force -ErrorAction Stop
        }
    }
}

try {
    & $compilerPath `
        '/nologo' `
        '/target:winexe' `
        ("/out:$outputPath") `
        ("/win32icon:$iconPath") `
        '/reference:System.Windows.Forms.dll' `
        '/reference:System.Drawing.dll' `
        '/reference:System.Core.dll' `
        ("/reference:" + (Join-Path $projectDirectory 'Libraries\Imazen.WebP.dll')) `
        $sourcePath

    if ($LASTEXITCODE -ne 0) {
        throw "La compilación falló con código de salida $LASTEXITCODE."
    }

    $desktopPath = [Environment]::GetFolderPath('Desktop')
    $shortcutPath = Join-Path $desktopPath 'SimCity 4 Launcher.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $outputPath
    $shortcut.WorkingDirectory = $projectDirectory
    $shortcut.Save()

    Write-Host "Compilación completada y acceso directo actualizado: $shortcutPath"
}
finally {
    if (Test-Path -LiteralPath $outputPath) {
        Start-Process -FilePath $outputPath -WorkingDirectory $projectDirectory
        Write-Host 'Launcher iniciado.'
    }
}