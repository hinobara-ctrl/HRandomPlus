param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()

function Require([bool]$condition, [string]$message) {
    if (-not $condition) { $script:errors.Add($message) }
}

[xml]$props = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
$version = [string]$props.Project.PropertyGroup.Version
Require (-not [string]::IsNullOrWhiteSpace($version)) 'Directory.Build.props no contiene una versión canónica.'

$workflowPath = Join-Path $root '.github/workflows/build.yml'
$workflow = Get-Content -LiteralPath $workflowPath -Raw
Require ($workflow -match '\$props\.Project\.PropertyGroup\.Version') 'El workflow no deriva la versión desde Directory.Build.props.'
Require ($workflow -notmatch [regex]::Escape($version)) 'El workflow contiene la versión canónica copiada literalmente.'
Require ([regex]::Matches($workflow, '--self-contained false').Count -eq 2) 'El workflow debe contener exactamente dos publishes framework-dependent.'
Require ($workflow -notmatch '--self-contained true') 'El workflow vigente no debe publicar paquetes self-contained.'
Require ($workflow -match '-f net8\.0-windows -r win-x64') 'Falta el target Windows net8.0-windows/win-x64.'
Require ($workflow -match '-f net8\.0 -r linux-x64') 'Falta el target Linux net8.0/linux-x64.'
Require ($workflow -match 'release-evidence\.txt') 'El workflow no publica release-evidence.txt.'
Require ($workflow -match 'scripts/check-repo-consistency\.ps1') 'El consistency checker no está integrado en CI.'

$desktopProject = Get-Content -LiteralPath (Join-Path $root 'src/HRandomPlus.Desktop/HRandomPlus.Desktop.csproj') -Raw
Require ($desktopProject -match '<TargetFrameworks>net8\.0;net8\.0-windows</TargetFrameworks>') 'Los targets Desktop no coinciden con la distribución documentada.'
Require ($desktopProject -match "TargetFramework.*net8\.0-windows") 'OsuMemoryDataProvider debe permanecer condicionado al target Windows.'

$readme = Get-Content -LiteralPath (Join-Path $root 'README.md') -Raw
Require ($readme -match [regex]::Escape("Versión de desarrollo: **v$version**")) 'README no coincide con la versión canónica.'
Require ($readme -match [regex]::Escape("HRandomPlus-v$version-windows-x64-framework-dependent.zip")) 'README no contiene el nombre canónico del ZIP Windows.'
Require ($readme -match [regex]::Escape("HRandomPlus-v$version-linux-x64-framework-dependent.zip")) 'README no contiene el nombre canónico del ZIP Linux.'
Require ($readme -match 'requieren \*\*\.NET Runtime 8 x64\*\*') 'README debe describir los binarios vigentes como framework-dependent.'

$documents = Get-ChildItem -LiteralPath (Join-Path $root 'docs') -Filter '*.md' -File -Recurse
foreach ($document in $documents) {
    $content = Get-Content -LiteralPath $document.FullName -Raw
    $header = ($content -split "`r?`n" | Select-Object -First 5) -join "`n"
    $match = [regex]::Match($header, '<!-- document-status: (current|historical) -->')
    if (-not $match.Success) {
        $errors.Add("$($document.Name) no declara document-status current/historical.")
        continue
    }

    if ($match.Groups[1].Value -eq 'current') {
        Require ($content -notmatch '\b\d+\s+pruebas\s+aprobadas\b') "$($document.Name) fija una cantidad efímera de tests como estado actual."
        Require ($content -notmatch '(?im)^HEAD base:') "$($document.Name) fija un HEAD mutable."
        foreach ($artifactMatch in [regex]::Matches($content, 'HRandomPlus-v(?<v>\d+\.\d+\.\d+(?:-playtest)?)')) {
            Require ($artifactMatch.Groups['v'].Value -eq $version) "$($document.Name) contiene un nombre de artefacto HRandomPlus de otra versión."
        }
    }
}

foreach ($requiredCurrent in @('templates/PRE_PUSH_CHECKLIST.md', 'templates/RELEASE_NOTES_TEMPLATE.md')) {
    $path = Join-Path $root "docs/$requiredCurrent"
    Require (Test-Path -LiteralPath $path -PathType Leaf) "Falta docs/$requiredCurrent."
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        $content = Get-Content -LiteralPath $path -Raw
        Require ($content -match '<!-- document-status: current -->') "$requiredCurrent debe ser documentación current."
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output "Repository consistency: PASS (version $version; $($documents.Count) classified documents)."
