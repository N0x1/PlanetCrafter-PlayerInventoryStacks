param([string]$GameManaged = 'C:\Program Files (x86)\Steam\steamapps\common\The Planet Crafter\Planet Crafter_Data\Managed')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet build .\PlayerInventoryStacks.csproj -c Release --ignore-failed-sources "-p:GameManaged=$GameManaged"
    if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
    dotnet build .\tests\Tests.csproj -c Release --ignore-failed-sources "-p:GameManaged=$GameManaged"
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & .\tests\bin\Release\net472\Tests.exe | Tee-Object -FilePath .\tests\results.txt
    if ($LASTEXITCODE -ne 0) { throw 'Verification failed.' }

    New-Item -ItemType Directory -Force -Path .\dist\package\plugins\PlayerInventoryStacks | Out-Null
    Copy-Item -LiteralPath .\bin\Release\net472\PlayerInventoryStacks.dll -Destination .\dist\package\plugins\PlayerInventoryStacks\PlayerInventoryStacks.dll
    Copy-Item -LiteralPath .\README.md,.\CHANGELOG.md,.\manifest.json,.\icon.png -Destination .\dist\package
    Compress-Archive -Path .\dist\package\* -DestinationPath .\dist\PlayerInventoryStacks-1.0.1.zip -Force
    Copy-Item -LiteralPath .\bin\Release\net472\PlayerInventoryStacks.dll -Destination .\dist\PlayerInventoryStacks.dll
    New-Item -ItemType Directory -Force -Path .\dist\source\src,.\dist\source\tests,.\dist\source\docs | Out-Null
    Copy-Item -Path .\docs\*.md -Destination .\dist\source\docs
    Copy-Item -Path .\src\*.cs -Destination .\dist\source\src
    Copy-Item -LiteralPath .\tests\Program.cs,.\tests\Tests.csproj -Destination .\dist\source\tests
    Copy-Item -LiteralPath .\PlayerInventoryStacks.csproj,.\build.ps1,.\README.md,.\CHANGELOG.md,.\manifest.json,.\icon.png -Destination .\dist\source
    Compress-Archive -Path .\dist\source\* -DestinationPath .\dist\PlayerInventoryStacks-1.0.1-source.zip -Force
    Get-FileHash -Algorithm SHA256 -LiteralPath .\dist\PlayerInventoryStacks-1.0.1.zip,.\dist\PlayerInventoryStacks.dll,.\dist\PlayerInventoryStacks-1.0.1-source.zip |
        ForEach-Object { '{0}  {1}' -f $_.Hash.ToLowerInvariant(), (Split-Path $_.Path -Leaf) } |
        Set-Content .\dist\SHA256.txt
    Write-Output 'Packaged in dist/.'
}
finally { Pop-Location }
