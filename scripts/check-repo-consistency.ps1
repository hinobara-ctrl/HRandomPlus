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
Require ($version -match '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') 'La versión canónica no tiene formato semántico.'

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
$releaseJob = ($workflow -split '(?m)^  release-candidate:', 2)[-1]
Require ($releaseJob -match 'uses: actions/setup-dotnet@') 'release-candidate debe instalar el SDK antes de registrar evidencia.'
$sdkChannels = [regex]::Matches($workflow, 'dotnet-version:\s*([^\r\n]+)') | ForEach-Object { $_.Groups[1].Value.Trim() }
Require (@($sdkChannels | Select-Object -Unique).Count -eq 1) 'Los jobs deben instalar el mismo canal SDK.'
foreach ($asset in @('windows-x64-framework-dependent.zip', 'linux-x64-framework-dependent.zip', 'source.zip', 'gpl-source.zip', 'SHA256SUMS.txt', 'release-evidence.txt')) {
    Require ($releaseJob.Contains($asset)) "release-candidate no incluye $asset."
}

$desktopProject = Get-Content -LiteralPath (Join-Path $root 'src/HRandomPlus.Desktop/HRandomPlus.Desktop.csproj') -Raw
Require ($desktopProject -match '<TargetFrameworks>net8\.0;net8\.0-windows</TargetFrameworks>') 'Los targets Desktop no coinciden con la distribución documentada.'
Require ($desktopProject -match "TargetFramework.*net8\.0-windows") 'OsuMemoryDataProvider debe permanecer condicionado al target Windows.'

$readme = Get-Content -LiteralPath (Join-Path $root 'README.md') -Raw
Require ($readme -match [regex]::Escape("Code candidate: **v$version**")) 'README no coincide con la versión canónica.'
Require ($readme -match [regex]::Escape("HRandomPlus-v$version-windows-x64-framework-dependent.zip")) 'README no contiene el nombre canónico del ZIP Windows.'
Require ($readme -match [regex]::Escape("HRandomPlus-v$version-linux-x64-framework-dependent.zip")) 'README no contiene el nombre canónico del ZIP Linux.'
Require ($readme -match 'require \*\*\.NET Runtime 8 x64\*\*') 'README debe describir los binarios vigentes como framework-dependent.'
Require ($readme.Contains('Guide → Parameters')) 'README debe dirigir al usuario a Guide → Parameters.'
Require ($readme.Contains('(RELEASE_CHECKLIST.md)')) 'README debe enlazar el checklist de release.'
Require ($readme.Contains('(V1_0_0_RELEASE_VALIDATION.md)')) 'README debe enlazar la validación de release de v1.0.0.'

$notices = Get-Content -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Raw
Require ($notices -notmatch 'HRandomPlus-v\d+\.\d+\.\d+') 'Los notices deben usar nombres de artefactos neutrales a la versión.'
$checklistPath = Join-Path $root 'RELEASE_CHECKLIST.md'
Require (Test-Path -LiteralPath $checklistPath -PathType Leaf) 'Falta RELEASE_CHECKLIST.md.'

$mainWindow = Get-Content -LiteralPath (Join-Path $root 'src/HRandomPlus.Desktop/MainWindow.cs') -Raw
$guide = Get-Content -LiteralPath (Join-Path $root 'src/HRandomPlus.Desktop/GuideContent.cs') -Raw
$parameterHeadings = @('TIMING / THRESHOLD', 'KEYMODE / LAYOUT', 'BPM / SNAP REFERENCE', 'SELECTION / HISTORY', 'OUTPUT', 'SCORING WEIGHTS')
$headingPattern = '(?s)' + (($parameterHeadings | ForEach-Object { [regex]::Escape('Section("' + $_ + '")') }) -join '.*')
Require ($mainWindow -match $headingPattern) 'Active parameters no contiene los seis grupos requeridos en el orden vigente.'
foreach ($editor in [regex]::Matches($mainWindow, 'AddEditor\(parameters, "([^"]+)", "([^"]+)"\)')) {
    $entry = 'new GuideEntry("' + $editor.Groups[1].Value + '", "' + $editor.Groups[2].Value + '"'
    Require ($guide.Contains($entry)) "Guide no documenta el editor $($editor.Groups[1].Value) con su etiqueta vigente."
}

$documents = Get-ChildItem -LiteralPath (Join-Path $root 'docs') -Filter '*.md' -File -Recurse
$currentDocuments = @((Get-Item -LiteralPath (Join-Path $root 'README.md')), (Get-Item -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md')))
if (Test-Path -LiteralPath $checklistPath) { $currentDocuments += Get-Item -LiteralPath $checklistPath }
$validationChecklistPath = Join-Path $root 'V1_0_0_RELEASE_VALIDATION.md'
Require (Test-Path -LiteralPath $validationChecklistPath -PathType Leaf) 'Falta V1_0_0_RELEASE_VALIDATION.md.'
if (Test-Path -LiteralPath $validationChecklistPath) { $currentDocuments += Get-Item -LiteralPath $validationChecklistPath }
foreach ($document in $documents) {
    $content = Get-Content -LiteralPath $document.FullName -Raw
    $header = ($content -split "`r?`n" | Select-Object -First 5) -join "`n"
    $match = [regex]::Match($header, '<!-- document-status: (current|historical) -->')
    if (-not $match.Success) {
        $errors.Add("$($document.Name) no declara document-status current/historical.")
        continue
    }

    if ($match.Groups[1].Value -eq 'current') {
        $currentDocuments += $document
        Require ($content -notmatch '\b\d+\s+pruebas\s+aprobadas\b') "$($document.Name) fija una cantidad efímera de tests como estado actual."
        Require ($content -notmatch '(?im)^HEAD base:') "$($document.Name) fija un HEAD mutable."
    }
}

foreach ($document in $currentDocuments) {
    $content = Get-Content -LiteralPath $document.FullName -Raw
    # Historical links may retain old version names; the surrounding current prose may not.
    $prose = [regex]::Replace($content, '\[[^\]]*\]\([^)]*historical/[^)]*\)', '')
    Require ($prose -notmatch '\bv?\d+\.\d+\.\d+-playtest\b') "$($document.Name) contiene una versión playtest en texto vigente."
    Require ($prose -notmatch '`Delete custom`|\*\*Delete custom\*\*') "$($document.Name) usa un nombre de botón retirado."
    foreach ($artifact in [regex]::Matches($prose, 'HRandomPlus-v(?<v>[0-9A-Za-z.-]+?)-(?:(?:windows|linux)-x64-framework-dependent|gpl-source|source)\.zip')) {
        Require ($artifact.Groups['v'].Value -eq $version) "$($document.Name) nombra un artefacto de otra versión."
    }
    foreach ($link in [regex]::Matches($content, '\[[^\]]*\]\(([^)]+)\)')) {
        $target = $link.Groups[1].Value
        if ($target -match '^[a-z]+:|^#') { continue }
        $target = ($target -split '#', 2)[0]
        Require (Test-Path -LiteralPath (Join-Path $document.DirectoryName $target)) "$($document.Name) contiene un enlace local inexistente: $target"
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
    $errors | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

Write-Output "Repository consistency: PASS (version $version; $($documents.Count) classified documents)."
