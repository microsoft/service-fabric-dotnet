# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
[int] $script:Checks = 0

function Main {
    [string] $root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    [string] $framework = if ($PSEdition -eq 'Desktop') { 'net462' } else { 'netstandard2.0' }
    [string] $output = (Resolve-Path (Join-Path $root "bin\$Configuration\$framework")).Path
    [string] $modulePath = Join-Path $output 'Microsoft.ServiceFabric.Powershell.Http.dll'
    [object] $module = Import-Module $modulePath -PassThru
    AddRecorder

    [object] $recorder = New-Object ExternalRepairRequestRecorder
    [object] $builder = New-Object Microsoft.ServiceFabric.Client.ServiceFabricClientBuilder
    $null = $builder.UseEndpoints([uri]'http://unused.invalid:19080')
    $null = [Microsoft.ServiceFabric.Client.Http.ServiceFabricClientBuilderExtensions]::ConfigureHttpClientSettings(
        $builder, (New-Object System.Net.Http.HttpClientHandler), [System.Net.Http.DelegatingHandler[]]@($recorder))
    [object] $client = $builder.BuildAsync().GetAwaiter().GetResult()
    [object] $previous = Get-Variable -Name SFHttpClusterConnection -Scope Global -ErrorAction SilentlyContinue
    [object] $previousValue = if ($null -ne $previous) { $previous.Value } else { $null }
    Set-Variable -Name SFHttpClusterConnection -Scope Global -Value $client

    try {
        TestMetadata $module.Name
        TestRequests $recorder
        TestInvalidTargets $recorder
        TestTaskPayloads $recorder
        [pscustomobject]@{
            Checks = $script:Checks
            Edition = $PSEdition
            Framework = $framework
            Result = 'Passed'
        }
    }
    finally {
        $client.Dispose()
        if ($null -eq $previous) {
            Remove-Variable -Name SFHttpClusterConnection -Scope Global
        }
        else {
            Set-Variable -Name SFHttpClusterConnection -Scope Global -Value $previousValue
        }
        Remove-Module $module
    }
}

function TestMetadata([string] $moduleName) {
    [string[]] $expected = @(
        'Get-SFRepairTaskList', 'New-SFRepairTask', 'Stop-SFRepairTask', 'New-SFApproveRepairTask',
        'Remove-SFRepairTask', 'Update-SFRepairExecutionState', 'Update-SFRepairTaskHealthPolicy'
    ) | Sort-Object
    [object[]] $commands = @(Get-Command -Module $moduleName | Where-Object {
        $_.Parameters.ContainsKey('ExternalRepairManagerServiceName')
    })
    AssertEqual ($expected -join ',') (($commands.Name | Sort-Object) -join ',') 'Only seven repair commands have the selector'
    foreach ($command in $commands) {
        [object] $parameter = $command.Parameters['ExternalRepairManagerServiceName']
        AssertEqual ([string]) $parameter.ParameterType "$($command.Name): selector is a string"
        [object[]] $attributes = @($parameter.Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] })
        AssertEqual 1 $attributes.Count "$($command.Name): one selector declaration"
        AssertEqual $true $attributes[0].DontShow "$($command.Name): selector is hidden"
        AssertEqual $false $attributes[0].Mandatory "$($command.Name): selector is optional"
        AssertEqual ([int]::MinValue) $attributes[0].Position "$($command.Name): selector is named-only"
    }
    foreach ($name in @('New-SFRepairTask', 'Update-SFRepairExecutionState')) {
        [object] $command = Get-Command $name
        AssertEqual '_Node__Node_' $command.DefaultParameterSet "$name`: existing default parameter set"
        foreach ($parameterName in @('External', 'ExternalImpactLevel')) {
            [object] $attribute = $command.Parameters[$parameterName].Attributes |
                Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] }
            AssertEqual $true $attribute.DontShow "$name`: $parameterName is hidden"
        }
    }
}

function TestRequests([object] $recorder) {
    [string] $taskId = [guid]::NewGuid().ToString()
    [object[]] $cases = @(
        @{ Path = 'GetRepairTaskList'; Method = 'GET'; Invoke = {
            param($selector)
            Get-SFRepairTaskList -TaskIdFilter $taskId -StateFilter 2 -ExecutorFilter XKulfi @selector
        } },
        @{ Path = 'CreateRepairTask'; Method = 'POST'; Invoke = {
            param($selector)
            New-SFRepairTask -External -TaskId $taskId -State Created -Action External.RestartNode -Version '0' @selector
        } },
        @{ Path = 'CancelRepairTask'; Method = 'POST'; Invoke = {
            param($selector)
            Stop-SFRepairTask -TaskId $taskId -Version '123' -RequestAbort $true @selector
        } },
        @{ Path = 'ForceApproveRepairTask'; Method = 'POST'; Invoke = {
            param($selector)
            New-SFApproveRepairTask -TaskId $taskId -Version '123' @selector
        } },
        @{ Path = 'DeleteRepairTask'; Method = 'POST'; Invoke = {
            param($selector)
            Remove-SFRepairTask -TaskId $taskId -Version '123' -Force @selector
        } },
        @{ Path = 'UpdateRepairExecutionState'; Method = 'POST'; Invoke = {
            param($selector)
            Update-SFRepairExecutionState -External -ExternalImpactLevel RestartNode -TaskId $taskId `
                -State Claimed -Action External.RestartNode -Version '123' -Executor XKulfi @selector
        } },
        @{ Path = 'UpdateRepairTaskHealthPolicy'; Method = 'POST'; Invoke = {
            param($selector)
            Update-SFRepairTaskHealthPolicy -TaskId $taskId -Version '123' -PerformPreparingHealthCheck $false @selector
        } }
    )
    [string] $service = 'fabric:/System/RepairManagerService/ext01'
    foreach ($case in $cases) {
        [int] $before = $recorder.Urls.Count
        $null = & $case.Invoke @{}
        $null = & $case.Invoke @{ ExternalRepairManagerServiceName = $service }
        AssertEqual ($before + 2) $recorder.Urls.Count "$($case.Path): exactly two requests"
        [uri] $original = $recorder.Urls[$before]
        [uri] $named = $recorder.Urls[$before + 1]
        AssertEqual ('/$/' + $case.Path) $named.AbsolutePath "$($case.Path): unchanged path"
        AssertEqual $case.Method $recorder.Methods[$before + 1] "$($case.Path): HTTP method"
        AssertEqual $recorder.Bodies[$before] $recorder.Bodies[$before + 1] "$($case.Path): unchanged body"
        AssertEqual ('?ExternalRepairManagerServiceUri=' + [uri]::EscapeDataString($service) + '&' + $original.Query.Substring(1)) `
            $named.Query "$($case.Path): one encoded query selector"
        if ($case.Method -eq 'POST') {
            [object] $body = ConvertFrom-Json $recorder.Bodies[$before + 1]
            AssertEqual $taskId $body.TaskId "$($case.Path): task ID"
        }
    }

    [int] $start = $recorder.Urls.Count
    $null = Get-SFRepairTaskList -ExternalRepairManagerServiceName 'fabric:/System/RepairManagerService/RM2'
    $null = Get-SFRepairTaskList
    AssertEqual '?api-version=6.0' ([uri]$recorder.Urls[$start + 1]).Query 'Named invocation does not change default connection'
}

function TestInvalidTargets([object] $recorder) {
    foreach ($name in @($null, '', ' ', 'fabric:/System/RepairManagerService',
        'fabric:/System/RepairManagerService/', 'fabric:/System/RepairManagerService/ext-1',
        'fabric:/System/RepairManagerService/ext1/other', 'fabric:/System/ERM/ext')) {
        AssertRejected { Get-SFRepairTaskList -ExternalRepairManagerServiceName $name } $recorder 'Invalid selector'
    }
    AssertRejected {
        New-SFRepairTask -Node -External -TaskId example -State Created -Action Restart
    } $recorder 'Node and External parameter sets cannot be combined'
    AssertRejected {
        New-SFRepairTask -External -ExternalImpactLevel Unknown -TaskId example -State Created -Action Restart
    } $recorder 'Unknown External impact level'
}

function TestTaskPayloads([object] $recorder) {
    [string] $taskId = [guid]::NewGuid().ToString()
    $null = New-SFRepairTask -TaskId $taskId -State Created -Action Restart
    [object] $body = ConvertFrom-Json $recorder.Bodies[$recorder.Bodies.Count - 1]
    AssertEqual $taskId $body.TaskId 'Existing invocation without a kind still binds'

    $null = New-SFRepairTask -Node -NodeNames ([string[]]@('node1')) -TaskId $taskId -State Created -Action Restart
    $body = ConvertFrom-Json $recorder.Bodies[$recorder.Bodies.Count - 1]
    AssertEqual 'Node' $body.Target.Kind 'Existing Node target'
    AssertEqual 'node1' $body.Target.NodeNames[0] 'Existing Node name'

    [string] $service = 'fabric:/System/RepairManagerService/ext01'
    $null = New-SFRepairTask -ExternalRepairManagerServiceName $service -External `
        -TaskId $taskId -State Created -Action External.RestartNode
    $body = ConvertFrom-Json $recorder.Bodies[$recorder.Bodies.Count - 1]
    AssertEqual 'External' $body.Target.Kind 'External target without mandatory impact'
    AssertEqual $null $body.PSObject.Properties['Impact'] 'Omitted impact is not synthesized'

    foreach ($command in @('New-SFRepairTask', 'Update-SFRepairExecutionState')) {
        $null = & $command -ExternalRepairManagerServiceName $service -External -ExternalImpactLevel RestartNode `
            -TaskId $taskId -State Claimed -Action External.RestartNode -Executor XKulfi -Version '123'
        $body = ConvertFrom-Json $recorder.Bodies[$recorder.Bodies.Count - 1]
        AssertEqual 'External' $body.Target.Kind "$command`: External target"
        AssertEqual 'External' $body.Impact.Kind "$command`: External impact"
        AssertEqual 'RestartNode' $body.Impact.ExternalImpactInfo.ImpactLevel "$command`: nested impact level"
        AssertEqual 'XKulfi' $body.Executor "$command`: executor"
    }

    $recorder.ListResponse = ConvertTo-Json -Depth 10 -Compress @(@{
        TaskId = $taskId; Version = '123'; State = 'Claimed'; Action = 'External.RestartNode'; Executor = 'XKulfi'
        Target = @{ Kind = 'External' }
        Impact = [ordered]@{ Kind = 'External'; ExternalImpactInfo = @{ ImpactLevel = 'RestartNode' } }
    })
    [object[]] $tasks = @(Get-SFRepairTaskList -ExternalRepairManagerServiceName $service)
    AssertEqual 1 $tasks.Count 'GET deserializes External task'
    AssertEqual $taskId $tasks[0].TaskId 'GET preserves task identity'
    AssertEqual 'External' $tasks[0].Target.Kind.ToString() 'GET preserves External target kind'
    AssertEqual 'External' $tasks[0].Impact.Kind.ToString() 'GET preserves External impact kind'
}

function AssertEqual([object] $expected, [object] $actual, [string] $description) {
    if ($expected -cne $actual) {
        throw "$description`: expected '$expected', actual '$actual'."
    }
    $script:Checks++
}

function AssertRejected([scriptblock] $invoke, [object] $recorder, [string] $description) {
    [int] $before = $recorder.Urls.Count
    [bool] $rejected = $false
    try {
        $null = & $invoke
    }
    catch [System.Management.Automation.RuntimeException] {
        $rejected = $true
    }
    AssertEqual $true $rejected "$description rejected"
    AssertEqual $before $recorder.Urls.Count "$description sends no request"
}

function AddRecorder {
    # Windows PowerShell compiles this fixture with its C# 5 compiler.
    [string] $source = @'
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public sealed class ExternalRepairRequestRecorder : DelegatingHandler
{
    public readonly List<string> Urls = new List<string>();
    public readonly List<string> Methods = new List<string>();
    public readonly List<string> Bodies = new List<string>();
    public string ListResponse = "[]";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Urls.Add(request.RequestUri.AbsoluteUri);
        Methods.Add(request.Method.Method);
        Bodies.Add(request.Content == null ? null : request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.Method == HttpMethod.Get ? ListResponse : "{\"Version\":\"123\"}")
        });
    }
}
'@
    [string[]] $references = @([System.Net.Http.HttpClient].Assembly.Location)
    if ($PSEdition -eq 'Core') {
        $references += @('System.Runtime', 'System.Collections', 'System.Net.Primitives')
    }
    Add-Type -TypeDefinition $source -ReferencedAssemblies $references
}

Main
