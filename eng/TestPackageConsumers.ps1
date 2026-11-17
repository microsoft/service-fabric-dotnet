Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

[string] $Root = Split-Path $PSScriptRoot
[string] $PackageDirectory = Join-Path $Root 'out\packages'
[string] $Project = Join-Path $Root 'test\Consumer\Microsoft.ServiceFabric.Consumer.csproj'
[string[]] $Frameworks = @('net11.0', 'net10.0', 'net8.0')

function Main
{
    [object[]] $packages = @(Get-ChildItem $PackageDirectory -Filter '*.nupkg' | ForEach-Object { PackageMetadata $_.FullName })
    [object[]] $actors = @($packages | Where-Object Id -eq 'Microsoft.ServiceFabric.Actors')
    if ($actors.Count -ne 1)
    {
        throw "Expected one Actors package in $PackageDirectory. Pack the SDK and remove obsolete package versions first."
    }

    [string] $version = $actors[0].Version
    [string] $commit = & git -C $Root rev-parse HEAD
    if ($LASTEXITCODE -ne 0)
    {
        throw 'Cannot determine the SDK source commit.'
    }

    [string] $rid = if ($IsWindows) { 'win' } elseif ($IsLinux) { 'linux' } else { throw 'Windows or Linux is required.' }
    $rid += "-$([Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant())"
    [string] $output = if ($IsWindows) { 'bin' } else { 'test\bin\Microsoft.ServiceFabric.Consumer' }
    [string] $temporary = Join-Path $Root "tmp\consumer-$([guid]::NewGuid())"
    [string] $cache = Join-Path $temporary 'packages'
    [string] $evidencePath = Join-Path $Root 'out\tests\ConsumerAssets.json'
    [System.Collections.Generic.List[object]] $evidence = @()
    New-Item $temporary -ItemType Directory -Force | Out-Null
    New-Item (Split-Path $evidencePath) -ItemType Directory -Force | Out-Null
    try
    {
        InvokeDotNet @('restore', $Project, '--runtime', $rid, '--packages', $cache, '--force-evaluate',
            "--property:ServiceFabricSdkVersion=$version", '--configfile', (Join-Path (Split-Path $Project) 'nuget.config'))
        InvokeDotNet @('build', $Project, '--configuration', 'Release', '--runtime', $rid, '--no-restore',
            "--property:ServiceFabricSdkVersion=$version")

        [object] $assets = Get-Content (Join-Path $Root 'obj\Microsoft.ServiceFabric.Consumer\project.assets.json') -Raw |
            ConvertFrom-Json -AsHashtable
        foreach ($name in $assets.libraries.Keys | Where-Object { $_ -match '^Microsoft\.ServiceFabric[./]' })
        {
            if ($assets.libraries[$name].type -ne 'package')
            {
                throw "$name is not a PackageReference dependency."
            }
            if ($name.StartsWith('Microsoft.ServiceFabric.'))
            {
                [object[]] $built = @($packages | Where-Object { "$($_.Id)/$($_.Version)" -eq $name })
                if ($built.Count -ne 1 -or $built[0].Version -ne $version -or $built[0].Commit -ne $commit)
                {
                    throw "$name does not match the freshly packed SDK version/source commit."
                }
            }
        }
        foreach ($framework in $Frameworks)
        {
            foreach ($targetName in @($framework, "$framework/$rid"))
            {
                if (-not $assets.targets.ContainsKey($targetName))
                {
                    throw "Missing restored target $targetName."
                }
                [hashtable] $target = $assets.targets[$targetName]
                [string[]] $fabricPackages = @($target.Keys | Where-Object { $_ -match '^Microsoft\.ServiceFabric[./]' })
                if ($fabricPackages.Count -lt 3)
                {
                    throw "Missing Service Fabric dependencies for $targetName."
                }
                foreach ($name in $fabricPackages)
                {
                    foreach ($kind in @('compile', 'runtime'))
                    {
                        [string[]] $paths = @($target[$name][$kind].Keys | Where-Object { $_ -like '*.dll' })
                        if ($paths.Count -eq 0 -or @($paths | Where-Object { $_ -notlike 'lib/net8.0/*' }).Count -ne 0)
                        {
                            throw "$name did not select net8.0 $kind assets for $targetName."
                        }
                        $evidence.Add([pscustomobject] @{
                            Target = $targetName
                            Package = $name
                            Kind = $kind
                            Assets = $paths
                        })
                    }
                }
            }
        }
        foreach ($framework in $Frameworks)
        {
            InvokeDotNet @((Join-Path $Root "$output\Release\$framework\$rid\Microsoft.ServiceFabric.Consumer.dll"))
        }
        [string] $sdk = & dotnet --version
        if ($LASTEXITCODE -ne 0)
        {
            throw 'Cannot determine the selected .NET SDK.'
        }
        [pscustomobject] @{
            SourceCommit = $commit
            SdkPackageVersion = $version
            DotNetSdk = $sdk
            RuntimeIdentifier = $rid
            ExecutedFrameworks = $Frameworks
            Assets = $evidence
        } | ConvertTo-Json -Depth 8 | Set-Content $evidencePath
        Write-Host "Packaged consumer checks passed. Evidence: $evidencePath"
    }
    finally
    {
        Remove-Item -LiteralPath $temporary -Recurse -Force
    }
}

function PackageMetadata([string] $path)
{
    [System.IO.Compression.ZipArchive] $archive = [System.IO.Compression.ZipFile]::OpenRead($path)
    try
    {
        [System.IO.Compression.ZipArchiveEntry] $entry = $archive.Entries | Where-Object FullName -like '*.nuspec'
        [System.IO.StreamReader] $reader = [System.IO.StreamReader]::new($entry.Open())
        try
        {
            [xml] $nuspec = $reader.ReadToEnd()
            return [pscustomobject] @{
                Id = $nuspec.package.metadata.id
                Version = $nuspec.package.metadata.version
                Commit = $nuspec.package.metadata.repository.commit
            }
        }
        finally
        {
            $reader.Dispose()
        }
    }
    finally
    {
        $archive.Dispose()
    }
}

function InvokeDotNet([string[]] $arguments)
{
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet $($arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Main
