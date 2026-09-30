Set-StrictMode -Version Latest

$script:UigRepositoryRoot = Split-Path -Parent $PSScriptRoot
$script:UigServerExecutable = Join-Path $script:UigRepositoryRoot 'Builds\LocalServer\UnityIsekaiServer.exe'
$script:UigClientExecutable = Join-Path $script:UigRepositoryRoot 'Builds\LocalClient\UnityIsekaiClient.exe'
$script:UigRuntimeLogDirectory = Join-Path $script:UigRepositoryRoot 'Logs\Runtime'
$script:UigClientProject = Join-Path $script:UigRepositoryRoot 'Projects\Client'
$script:UigServerProject = Join-Path $script:UigRepositoryRoot 'Projects\Server'
$script:UigUnityExecutable = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$script:UigDefaultPort = 7777
$script:UigAuthenticationTokenPath = Join-Path $script:UigRepositoryRoot '.uig\local-auth-token'
$script:UigServerReadyMarker = '[Local Server] Local server is listening on'

function Get-UigAuthenticationToken {
    $directory = Split-Path -Parent $script:UigAuthenticationTokenPath
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    if (-not (Test-Path -LiteralPath $script:UigAuthenticationTokenPath -PathType Leaf)) {
        $bytes = New-Object byte[] 32
        $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
        $token = ([System.BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
        Set-Content -LiteralPath $script:UigAuthenticationTokenPath -Value $token -NoNewline
    }

    $value = (Get-Content -LiteralPath $script:UigAuthenticationTokenPath -Raw).Trim()
    if ($value -notmatch '^[a-f0-9]{64}$') {
        throw "The local authentication token at '$script:UigAuthenticationTokenPath' is invalid. Delete it and rerun the command to regenerate it."
    }

    $value
}

function Assert-UigBuildExists {
    param([Parameter(Mandatory)][string]$ExecutablePath)

    if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
        throw "The game build was not found at '$ExecutablePath'. Build that target in Unity before using this command."
    }
}

function Assert-UigClientId {
    param([Parameter(Mandatory)][string]$ClientId)

    if ($ClientId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$') {
        throw "Client ID '$ClientId' is invalid. Use 1-64 letters, numbers, periods, underscores, or hyphens."
    }
}

function Get-UigProcesses {
    param([Parameter(Mandatory)][string]$ExecutablePath)

    $processName = [System.IO.Path]::GetFileNameWithoutExtension($ExecutablePath)
    @(Get-Process -Name $processName -ErrorAction SilentlyContinue | Where-Object {
        try {
            [string]::Equals($_.Path, $ExecutablePath, [System.StringComparison]::OrdinalIgnoreCase)
        }
        catch {
            $false
        }
    })
}

function Get-UigClientPidPath {
    param([Parameter(Mandatory)][string]$ClientId)

    Join-Path $script:UigRuntimeLogDirectory "client-$ClientId.pid"
}

function Get-UigTrackedClientProcess {
    param([Parameter(Mandatory)][string]$ClientId)

    $pidPath = Get-UigClientPidPath $ClientId
    if (-not (Test-Path -LiteralPath $pidPath -PathType Leaf)) {
        return
    }

    [int]$processId = 0
    $storedPid = Get-Content -LiteralPath $pidPath -Raw -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($storedPid) -or
        -not [int]::TryParse($storedPid.Trim(), [ref]$processId)) {
        Remove-Item -LiteralPath $pidPath -Force
        return
    }

    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    $isExpectedClient = $false
    if ($null -ne $process) {
        try {
            $isExpectedClient = [string]::Equals(
                $process.Path,
                $script:UigClientExecutable,
                [System.StringComparison]::OrdinalIgnoreCase)
        }
        catch {
            $isExpectedClient = $false
        }
    }

    if (-not $isExpectedClient) {
        Remove-Item -LiteralPath $pidPath -Force
        return
    }

    $process
}

function Stop-UigClientProcessGracefully {
    param([Parameter(Mandatory)][System.Diagnostics.Process]$Process)

    $closeRequested = $false
    try {
        $closeRequested = $Process.CloseMainWindow()
        if ($closeRequested -and $Process.WaitForExit(5000)) {
            return
        }
    }
    catch {
        # Fall through to the bounded force-stop below if the process exited or cannot
        # accept a window-close request.
    }

    $Process.Refresh()
    if (-not $Process.HasExited) {
        $Process | Stop-Process
    }
}

function Test-UigServerReady {
    $logPath = Join-Path $script:UigRuntimeLogDirectory 'server.log'
    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
        return $false
    }

    $match = Select-String -LiteralPath $logPath -SimpleMatch $script:UigServerReadyMarker -Quiet -ErrorAction SilentlyContinue
    return [bool]$match
}

function Wait-UigServerReady {
    param(
        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 120
    )

    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $Process.Refresh()
        if ($Process.HasExited) {
            throw "The server exited before it became ready (exit code $($Process.ExitCode))."
        }

        if (Test-UigServerReady) {
            return
        }

        Start-Sleep -Milliseconds 250
    }

    throw "The server process is still running but did not become ready within $TimeoutSeconds seconds. Check 'uig server logs 100'."
}

function Start-UigServer {
    Assert-UigBuildExists $script:UigServerExecutable
    $running = @(Get-UigProcesses $script:UigServerExecutable)
    if ($running.Count -gt 0) {
        Write-Host "Server is already running (PID $($running[0].Id)) on 127.0.0.1:$script:UigDefaultPort." -ForegroundColor Yellow
        return
    }

    New-Item -ItemType Directory -Path $script:UigRuntimeLogDirectory -Force | Out-Null
    $logPath = Join-Path $script:UigRuntimeLogDirectory 'server.log'
    Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue
    $authenticationToken = Get-UigAuthenticationToken
    $arguments = @(
        '-batchmode',
        '-nographics',
        '--listen-address', '127.0.0.1',
        '--server-port', $script:UigDefaultPort,
        '--max-players', '8',
        '--auth-token', $authenticationToken,
        '-logFile', $logPath
    )
    $startOptions = @{
        FilePath = $script:UigServerExecutable
        ArgumentList = $arguments
        WorkingDirectory = Split-Path -Parent $script:UigServerExecutable
        WindowStyle = 'Hidden'
        PassThru = $true
    }
    $process = Start-Process @startOptions
    Write-Host "Server process started (PID $($process.Id)); waiting for the authoritative world to load..." -ForegroundColor Cyan
    Wait-UigServerReady -Process $process
    Write-Host "Server ready (PID $($process.Id)) on 127.0.0.1:$script:UigDefaultPort." -ForegroundColor Green
    Write-Host "Log: $logPath"
}

function Stop-UigServer {
    $running = @(Get-UigProcesses $script:UigServerExecutable)
    if ($running.Count -eq 0) {
        Write-Host 'Server is not running.' -ForegroundColor Yellow
        return
    }

    $running | Stop-Process
    Write-Host "Stopped $($running.Count) server process(es)." -ForegroundColor Green
}

function Show-UigServerStatus {
    $running = @(Get-UigProcesses $script:UigServerExecutable)
    if ($running.Count -eq 0) {
        Write-Host 'Server: stopped'
        return
    }

    if (Test-UigServerReady) {
        Write-Host "Server: ready on 127.0.0.1:$script:UigDefaultPort (PID $($running[0].Id))" -ForegroundColor Green
    }
    else {
        Write-Host "Server: process running, authoritative world still starting (PID $($running[0].Id))" -ForegroundColor Yellow
    }
}

function Start-UigClient {
    param([string]$ClientId = 'default')

    Assert-UigClientId $ClientId
    Assert-UigBuildExists $script:UigClientExecutable
    $running = @(Get-UigTrackedClientProcess $ClientId)
    if ($running.Count -gt 0) {
        Write-Host "Client '$ClientId' is already running (PID $($running[0].Id))." -ForegroundColor Yellow
        return
    }

    if (@(Get-UigProcesses $script:UigServerExecutable).Count -eq 0) {
        Write-Warning "The local server is not running. Start it with 'uig server start'."
    }
    elseif (-not (Test-UigServerReady)) {
        throw "The local server process is still loading its authoritative world. Wait for 'uig server status' to report ready before starting a client."
    }

    New-Item -ItemType Directory -Path $script:UigRuntimeLogDirectory -Force | Out-Null
    $logPath = Join-Path $script:UigRuntimeLogDirectory "client-$ClientId.log"
    $authenticationToken = Get-UigAuthenticationToken
    $arguments = @(
        '--local-client',
        '--server-address', '127.0.0.1',
        '--server-port', $script:UigDefaultPort,
        '--auth-token', $authenticationToken,
        '-logFile', $logPath
    )
    if ($ClientId -ne 'default') {
        $arguments += @('--account', $ClientId)
    }
    $startOptions = @{
        FilePath = $script:UigClientExecutable
        ArgumentList = $arguments
        WorkingDirectory = Split-Path -Parent $script:UigClientExecutable
        PassThru = $true
    }
    $process = Start-Process @startOptions
    Set-Content -LiteralPath (Get-UigClientPidPath $ClientId) -Value $process.Id
    Write-Host "Client '$ClientId' started (PID $($process.Id)). The app connection is authenticated; complete account Login or Create Account in the game window." -ForegroundColor Green
    Write-Host "Log: $logPath"
}

function Stop-UigClient {
    param([string]$ClientId = 'default')

    Assert-UigClientId $ClientId
    $running = @(Get-UigTrackedClientProcess $ClientId)
    if ($running.Count -eq 0) {
        Write-Host "Client '$ClientId' is not running." -ForegroundColor Yellow
        return
    }

    Stop-UigClientProcessGracefully $running[0]
    Remove-Item -LiteralPath (Get-UigClientPidPath $ClientId) -Force -ErrorAction SilentlyContinue
    Write-Host "Client '$ClientId' stopped." -ForegroundColor Green
}

function Show-UigClientStatus {
    param([string]$ClientId = 'default')

    Assert-UigClientId $ClientId
    $running = @(Get-UigTrackedClientProcess $ClientId)
    if ($running.Count -eq 0) {
        Write-Host "Client '$ClientId': stopped"
        return
    }

    Write-Host "Client '$ClientId': running (PID $($running[0].Id))" -ForegroundColor Green
}

function Show-UigLog {
    param(
        [Parameter(Mandatory)][string]$LogPath,
        [int]$LineCount = 60
    )

    if ($LineCount -lt 1 -or $LineCount -gt 10000) {
        throw 'The log line count must be between 1 and 10000.'
    }

    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
        Write-Host "No log exists yet at '$LogPath'." -ForegroundColor Yellow
        return
    }

    Write-Host "Log: $LogPath" -ForegroundColor Cyan
    Get-Content -LiteralPath $LogPath -Tail $LineCount
}

function Show-UigClients {
    if (-not (Test-Path -LiteralPath $script:UigRuntimeLogDirectory -PathType Container)) {
        Write-Host 'No tracked clients.'
        return
    }

    $pidFiles = @(Get-ChildItem -LiteralPath $script:UigRuntimeLogDirectory -Filter 'client-*.pid' -File)
    $trackedCount = 0
    foreach ($pidFile in $pidFiles) {
        $clientId = $pidFile.BaseName.Substring(('client-').Length)
        $running = @(Get-UigTrackedClientProcess $clientId)
        if ($running.Count -gt 0) {
            $trackedCount++
            Write-Host "Client '$clientId': running (PID $($running[0].Id))" -ForegroundColor Green
        }
    }

    if ($trackedCount -eq 0) {
        Write-Host 'No tracked clients are running.'
    }

    $allClients = @(Get-UigProcesses $script:UigClientExecutable)
    if ($allClients.Count -gt $trackedCount) {
        Write-Host "$($allClients.Count - $trackedCount) additional client process(es) were started outside uig." -ForegroundColor Yellow
    }
}

function Stop-UigClients {
    if (-not (Test-Path -LiteralPath $script:UigRuntimeLogDirectory -PathType Container)) {
        Write-Host 'No tracked clients are running.' -ForegroundColor Yellow
        return
    }

    $pidFiles = @(Get-ChildItem -LiteralPath $script:UigRuntimeLogDirectory -Filter 'client-*.pid' -File)
    $stoppedCount = 0
    foreach ($pidFile in $pidFiles) {
        $clientId = $pidFile.BaseName.Substring(('client-').Length)
        $running = @(Get-UigTrackedClientProcess $clientId)
        if ($running.Count -gt 0) {
            Stop-UigClientProcessGracefully $running[0]
            $stoppedCount++
        }

        Remove-Item -LiteralPath $pidFile.FullName -Force -ErrorAction SilentlyContinue
    }

    if ($stoppedCount -eq 0) {
        Write-Host 'No tracked clients are running.' -ForegroundColor Yellow
    }
    else {
        Write-Host "Stopped $stoppedCount tracked client process(es)." -ForegroundColor Green
    }
}

function Invoke-UigBuild {
    param([Parameter(Mandatory)][ValidateSet('client', 'server')][string]$Target)

    if (-not (Test-Path -LiteralPath $script:UigUnityExecutable -PathType Leaf)) {
        throw "Unity 6000.5.4f1 was not found at '$script:UigUnityExecutable'."
    }

    New-Item -ItemType Directory -Path $script:UigRuntimeLogDirectory -Force | Out-Null
    if ($Target -eq 'client') {
        $projectPath = $script:UigClientProject
        $method = 'UnityIsekaiGame.Editor.LocalNetworkBuildAutomation.BuildWindowsLocalClientCommandLine'
        $logPath = Join-Path $script:UigRuntimeLogDirectory 'client-build.log'
    }
    else {
        $projectPath = $script:UigServerProject
        $method = 'UnityIsekaiGame.ServerProject.Editor.ServerProjectBuildAutomation.BuildWindowsDedicatedServerCommandLine'
        $logPath = Join-Path $script:UigRuntimeLogDirectory 'server-build.log'
    }

    Write-Host "Building $Target..." -ForegroundColor Cyan
    $arguments = @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', $projectPath,
        '-executeMethod', $method,
        '-logFile', $logPath
    )
    $startOptions = @{
        FilePath = $script:UigUnityExecutable
        ArgumentList = $arguments
        WorkingDirectory = $script:UigRepositoryRoot
        WindowStyle = 'Hidden'
        Wait = $true
        PassThru = $true
    }
    $process = Start-Process @startOptions
    if ($process.ExitCode -ne 0) {
        throw "The $Target build failed with exit code $($process.ExitCode). See '$logPath'."
    }

    Write-Host "$Target build completed successfully." -ForegroundColor Green
    Write-Host "Log: $logPath"
}

function Show-UigPaths {
    Write-Host "Repository: $script:UigRepositoryRoot"
    Write-Host "Client project: $script:UigClientProject"
    Write-Host "Server project: $script:UigServerProject"
    Write-Host "Client build: $script:UigClientExecutable"
    Write-Host "Server build: $script:UigServerExecutable"
    Write-Host "Runtime logs: $script:UigRuntimeLogDirectory"
}

function Test-UigSetup {
    $checks = @(
        [pscustomobject]@{ Name = 'Unity editor'; Path = $script:UigUnityExecutable },
        [pscustomobject]@{ Name = 'Client project'; Path = $script:UigClientProject },
        [pscustomobject]@{ Name = 'Server project'; Path = $script:UigServerProject },
        [pscustomobject]@{ Name = 'Client build'; Path = $script:UigClientExecutable },
        [pscustomobject]@{ Name = 'Server build'; Path = $script:UigServerExecutable }
    )

    $allPassed = $true
    foreach ($check in $checks) {
        $exists = Test-Path -LiteralPath $check.Path
        $allPassed = $allPassed -and $exists
        $color = if ($exists) { 'Green' } else { 'Red' }
        $result = if ($exists) { 'OK' } else { 'MISSING' }
        Write-Host ("{0,-16} {1,-7} {2}" -f $check.Name, $result, $check.Path) -ForegroundColor $color
    }

    Show-UigServerStatus
    Show-UigClients
    if (-not $allPassed) {
        throw "One or more UIG setup checks failed. Run 'uig paths' for the configured locations."
    }
}

function Show-UigServerHelp {
    Write-Host 'Unity Isekai Game - server commands' -ForegroundColor Cyan
    Write-Host ''
    Write-Host '  uig server start'
    Write-Host '  uig server stop'
    Write-Host '  uig server restart'
    Write-Host '  uig server status'
    Write-Host '  uig server logs [lines]'
    Write-Host '  uig server help'
}

function Show-UigClientHelp {
    Write-Host 'Unity Isekai Game - client commands' -ForegroundColor Cyan
    Write-Host ''
    Write-Host '  uig client start'
    Write-Host '  uig client end'
    Write-Host '  uig client restart'
    Write-Host '  uig client status'
    Write-Host '  uig client logs [lines]'
    Write-Host '  uig client <username-or-userID> start'
    Write-Host '  uig client help'
    Write-Host ''
    Write-Host 'Examples:' -ForegroundColor Cyan
    Write-Host '  uig client start'
    Write-Host '  uig client jhand start       # prefill username; password is still required'
    Write-Host '  uig client <64-char-userID> start'
}

function Show-UigHelp {
    Write-Host 'Unity Isekai Game launcher' -ForegroundColor Cyan
    Write-Host ''
    Write-Host 'Server:' -ForegroundColor Cyan
    Write-Host '  uig server start'
    Write-Host '  uig server stop'
    Write-Host '  uig server restart'
    Write-Host '  uig server status'
    Write-Host '  uig server logs [lines]'
    Write-Host '  uig server help'
    Write-Host ''
    Write-Host 'Client:' -ForegroundColor Cyan
    Write-Host '  uig client start|end|restart|status|logs [lines]'
    Write-Host '  uig client <username-or-userID> start|end|restart|status|logs [lines]'
    Write-Host '  uig client help'
    Write-Host ''
    Write-Host 'Other:' -ForegroundColor Cyan
    Write-Host '  uig status'
    Write-Host '  uig clients [status|end]'
    Write-Host '  uig logs [lines]'
    Write-Host '  uig build <client|server|all>'
    Write-Host '  uig doctor'
    Write-Host '  uig paths'
    Write-Host '  uig help'
}

function uig {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments)]
        [string[]]$Command
    )

    if ($null -eq $Command -or $Command.Count -eq 0) {
        Show-UigHelp
        return
    }

    $target = $Command[0].ToLowerInvariant()
    if ($target -in @('help', '-h', '--help', '/?')) {
        Show-UigHelp
        return
    }

    if ($target -eq 'status') {
        Show-UigServerStatus
        Show-UigClients
        return
    }

    if ($target -eq 'clients') {
        $action = if ($Command.Count -gt 1) { $Command[1].ToLowerInvariant() } else { 'status' }
        switch ($action) {
            { $_ -in @('status', 'list') } { Show-UigClients; return }
            { $_ -in @('stop', 'end') } { Stop-UigClients; return }
            default { throw "Unknown clients command '$action'. Run 'uig help' for usage." }
        }
        return
    }

    if ($target -eq 'paths') {
        Show-UigPaths
        return
    }

    if ($target -eq 'doctor') {
        Test-UigSetup
        return
    }

    if ($target -eq 'build') {
        if ($Command.Count -lt 2) {
            throw "Usage: uig build <client|server|all>"
        }

        $buildTarget = $Command[1].ToLowerInvariant()
        switch ($buildTarget) {
            'client' { Invoke-UigBuild client; return }
            'server' { Invoke-UigBuild server; return }
            'all' { Invoke-UigBuild client; Invoke-UigBuild server; return }
            default { throw "Unknown build target '$($Command[1])'. Run 'uig help' for usage." }
        }
    }

    if ($target -eq 'logs') {
        if ($Command.Count -gt 1) {
            [int]$lineCount = $Command[1]
            Show-UigLog (Join-Path $script:UigRuntimeLogDirectory 'server.log') $lineCount
        }
        else {
            Show-UigLog (Join-Path $script:UigRuntimeLogDirectory 'server.log')
        }
        return
    }

    if ($target -eq 'server') {
        if ($Command.Count -lt 2) {
            Show-UigServerHelp
            return
        }

        $action = $Command[1].ToLowerInvariant()
        switch ($action) {
            { $_ -in @('help', '-h', '--help', '/?') } { Show-UigServerHelp; return }
            'start' { Start-UigServer; return }
            { $_ -in @('stop', 'end') } { Stop-UigServer; return }
            'restart' { Stop-UigServer; Start-UigServer; return }
            'status' { Show-UigServerStatus; return }
            { $_ -in @('log', 'logs') } {
                if ($Command.Count -gt 2) {
                    [int]$lineCount = $Command[2]
                    Show-UigLog (Join-Path $script:UigRuntimeLogDirectory 'server.log') $lineCount
                }
                else {
                    Show-UigLog (Join-Path $script:UigRuntimeLogDirectory 'server.log')
                }
                return
            }
            default { throw "Unknown server command '$($Command[1])'. Run 'uig server help' for usage." }
        }
    }

    if ($target -eq 'client') {
        if ($Command.Count -gt 1 -and $Command[1].ToLowerInvariant() -in @('help', '-h', '--help', '/?')) {
            Show-UigClientHelp
            return
        }

        $directActions = @('start', 'stop', 'end', 'restart', 'status', 'log', 'logs')
        if ($Command.Count -lt 2) { Show-UigClientHelp; return }
        if ($Command[1].ToLowerInvariant() -in $directActions) {
            $clientId = 'default'
            $action = $Command[1].ToLowerInvariant()
            $argumentOffset = 2
        }
        elseif ($Command.Count -ge 3) {
            $clientId = $Command[1]
            $action = $Command[2].ToLowerInvariant()
            $argumentOffset = 3
        }
        else { Show-UigClientHelp; return }
        switch ($action) {
            'start' { Start-UigClient $clientId; return }
            { $_ -in @('stop', 'end') } { Stop-UigClient $clientId; return }
            'restart' { Stop-UigClient $clientId; Start-UigClient $clientId; return }
            'status' { Show-UigClientStatus $clientId; return }
            { $_ -in @('log', 'logs') } {
                Assert-UigClientId $clientId
                $logPath = Join-Path $script:UigRuntimeLogDirectory "client-$clientId.log"
                if ($Command.Count -gt $argumentOffset) {
                    [int]$lineCount = $Command[$argumentOffset]
                    Show-UigLog $logPath $lineCount
                }
                else {
                    Show-UigLog $logPath
                }
                return
            }
            default { throw "Unknown client command '$($Command[2])'. Run 'uig client help' for usage." }
        }
    }

    throw "Unknown UIG command '$($Command -join ' ')'. Run 'uig help' for usage."
}
