Set-StrictMode -Version Latest

$script:UigRepositoryRoot = Split-Path -Parent $PSScriptRoot
$script:UigServerExecutable = Join-Path $script:UigRepositoryRoot 'Builds\LocalServer\UnityIsekaiServer.exe'
$script:UigClientExecutable = Join-Path $script:UigRepositoryRoot 'Builds\LocalClient\UnityIsekaiClient.exe'
$script:UigRuntimeLogDirectory = Join-Path $script:UigRepositoryRoot 'Logs\Runtime'
$script:UigServerCaptureHistoryPath = Join-Path $script:UigRuntimeLogDirectory 'server-captures.log'
$script:UigClientProject = Join-Path $script:UigRepositoryRoot 'Projects\Client'
$script:UigServerProject = Join-Path $script:UigRepositoryRoot 'Projects\Server'
$script:UigUnityExecutable = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$script:UigDefaultPort = 7777
$script:UigAuthenticationTokenPath = Join-Path $script:UigRepositoryRoot '.uig\local-auth-token'
$script:UigTraceControlPath = Join-Path $script:UigRepositoryRoot '.uig\movement-trace.enabled'
$script:UigServerReadyMarker = '[Local Server] Local server is listening on'
$script:UigMovementMonitorScript = Join-Path $PSScriptRoot 'UIG-MovementLatencyMonitor.ps1'
$script:UigMovementMonitorPidPath = Join-Path $script:UigRuntimeLogDirectory 'movement-latency-monitor.pid'
$script:UigMovementMonitorReadyPath = Join-Path $script:UigRuntimeLogDirectory 'movement-latency-monitor.ready'
$script:UigMovementLatencyCsvPath = Join-Path $script:UigRuntimeLogDirectory 'action-trace.csv'
$script:UigServerFrameStallCsvPath = Join-Path $script:UigRuntimeLogDirectory 'server-frame-stalls.csv'
$script:UigMovementMonitorStartupErrorPath = Join-Path $script:UigRuntimeLogDirectory 'movement-latency-monitor-startup-error.log'

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
    Archive-UigServerCaptureMetrics
    Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue
    $authenticationToken = Get-UigAuthenticationToken
    $arguments = @(
        '-batchmode',
        '-nographics',
        '--listen-address', '127.0.0.1',
        '--server-port', $script:UigDefaultPort,
        '--max-players', '8',
        '--auth-token', $authenticationToken,
        '--movement-trace-control', ('"' + $script:UigTraceControlPath + '"'),
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
    $ClientId = 'default'
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
    Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue
    $authenticationToken = Get-UigAuthenticationToken
    $arguments = @(
        '--local-client',
        '--server-address', '127.0.0.1',
        '--server-port', $script:UigDefaultPort,
        '--auth-token', $authenticationToken,
        '--movement-trace-control', ('"' + $script:UigTraceControlPath + '"'),
        '-logFile', $logPath
    )
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

function Get-UigMovementMonitorProcess {
    if (-not (Test-Path -LiteralPath $script:UigMovementMonitorPidPath -PathType Leaf)) {
        return
    }

    [int]$processId = 0
    $storedPid = Get-Content -LiteralPath $script:UigMovementMonitorPidPath -Raw -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($storedPid) -or
        -not [int]::TryParse($storedPid.Trim(), [ref]$processId)) {
        Remove-Item -LiteralPath $script:UigMovementMonitorPidPath -Force -ErrorAction SilentlyContinue
        return
    }

    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if ($null -eq $process -or $process.ProcessName -notin @('powershell', 'pwsh')) {
        Remove-Item -LiteralPath $script:UigMovementMonitorPidPath -Force -ErrorAction SilentlyContinue
        return
    }

    $processRecord = Get-CimInstance Win32_Process -Filter "ProcessId = $processId" -ErrorAction SilentlyContinue
    if ($null -eq $processRecord -or
        $processRecord.CommandLine -notlike '*UIG-MovementLatencyMonitor.ps1*') {
        Remove-Item -LiteralPath $script:UigMovementMonitorPidPath -Force -ErrorAction SilentlyContinue
        return
    }

    $process
}

function Start-UigMovementMonitor {
    param([switch]$Reset)

    if (-not (Test-Path -LiteralPath $script:UigMovementMonitorScript -PathType Leaf)) {
        throw "The action trace monitor was not found at '$script:UigMovementMonitorScript'."
    }

    $running = @(Get-UigMovementMonitorProcess)
    if ($running.Count -gt 0) {
        if (-not $Reset) {
            Write-Host "Action trace graph is already running (PID $($running[0].Id))." -ForegroundColor Yellow
            return
        }
        Stop-UigMovementMonitor
    }

    New-Item -ItemType Directory -Path $script:UigRuntimeLogDirectory -Force | Out-Null
    if ($Reset) {
        Remove-Item -LiteralPath $script:UigMovementLatencyCsvPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $script:UigServerFrameStallCsvPath -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $script:UigMovementMonitorReadyPath -Force -ErrorAction SilentlyContinue

    $windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $quotedScript = '"' + $script:UigMovementMonitorScript + '"'
    $quotedRoot = '"' + $script:UigRepositoryRoot + '"'
    $arguments = @(
        '-NoProfile',
        '-NonInteractive',
        '-ExecutionPolicy', 'Bypass',
        '-STA',
        '-File', $quotedScript,
        '-RepositoryRoot', $quotedRoot
    )
    Remove-Item -LiteralPath $script:UigMovementMonitorStartupErrorPath -Force -ErrorAction SilentlyContinue
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $windowsPowerShell
    $startInfo.Arguments = $arguments -join ' '
    $startInfo.WorkingDirectory = $script:UigRepositoryRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw 'The action trace graph process could not be started.'
    }
    Set-Content -LiteralPath $script:UigMovementMonitorPidPath -Value $process.Id -NoNewline
    $readyTimer = [System.Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path -LiteralPath $script:UigMovementMonitorReadyPath -PathType Leaf)) {
        $process.Refresh()
        if ($process.HasExited) {
            throw "The action trace graph exited during startup (exit code $($process.ExitCode))."
        }
        if ($readyTimer.Elapsed.TotalSeconds -ge 5d) {
            throw 'The action trace graph did not become ready within five seconds.'
        }
        Start-Sleep -Milliseconds 50
    }
    Write-Host "Action trace graph started (PID $($process.Id))." -ForegroundColor Green
    Write-Host "Sample history: $script:UigMovementLatencyCsvPath"
}

function Stop-UigMovementMonitor {
    $running = @(Get-UigMovementMonitorProcess)
    if ($running.Count -eq 0) {
        Write-Host 'Action trace graph is not running.' -ForegroundColor Yellow
        return
    }

    $process = $running[0]
    try {
        if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(2000)) {
            $process | Stop-Process
            [void]$process.WaitForExit(2000)
        }
    }
    catch {
        $process.Refresh()
        if (-not $process.HasExited) {
            $process | Stop-Process
            [void]$process.WaitForExit(2000)
        }
    }
    Remove-Item -LiteralPath $script:UigMovementMonitorPidPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $script:UigMovementMonitorReadyPath -Force -ErrorAction SilentlyContinue
    Write-Host 'Action trace graph stopped.' -ForegroundColor Green
}

function Show-UigMovementMonitorStatus {
    $running = @(Get-UigMovementMonitorProcess)
    if ($running.Count -eq 0) {
        Write-Host 'Action trace graph: stopped'
        return
    }

    Write-Host "Action trace graph: running (PID $($running[0].Id))" -ForegroundColor Green
    Write-Host "Sample history: $script:UigMovementLatencyCsvPath"
}

function Archive-UigServerCaptureMetrics {
    $logPath = Join-Path $script:UigRuntimeLogDirectory 'server.log'
    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
        return
    }

    $captureLines = @(Get-Content -LiteralPath $logPath -ErrorAction SilentlyContinue | Where-Object {
        ($_.Contains('[Server Persistence] Utc=') -and $_.Contains('Queued world checkpoint transaction')) -or
        $_.Contains('[Server Persistence Capture] Utc=') -or
        $_.Contains('[Server Persistence Write] Utc=')
    })
    if ($captureLines.Count -eq 0) {
        return
    }

    Add-Content -LiteralPath $script:UigServerCaptureHistoryPath -Value $captureLines -Encoding UTF8
}

function Show-UigMovementTrace {
    param([int]$LineCount = 200)

    if ($LineCount -lt 1 -or $LineCount -gt 10000) {
        throw 'The action trace line count must be between 1 and 10000.'
    }
    $ClientId = 'default'
    Assert-UigClientId $ClientId
    $paths = @(
        (Join-Path $script:UigRuntimeLogDirectory 'server.log'),
        (Join-Path $script:UigRuntimeLogDirectory "client-$ClientId.log")
    )
    foreach ($path in $paths) {
        Write-Host "Action trace: $path" -ForegroundColor Cyan
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            Write-Host 'No log exists yet.' -ForegroundColor Yellow
            continue
        }
        $matches = @(Select-String -LiteralPath $path -SimpleMatch -Pattern @(
                '[Action Timing]',
                '[Movement Timing]',
                '[Movement Trace]'))
        if ($matches.Count -eq 0) {
            Write-Host "No trace entries. Run 'uig trace start' while the game is running." -ForegroundColor Yellow
        }
        $matches | Select-Object -Last $LineCount | ForEach-Object { $_.Line }
    }
}

function Test-UigTraceEnabled {
    Test-Path -LiteralPath $script:UigTraceControlPath -PathType Leaf
}

function Assert-UigRuntimeTraceCapable {
    param(
        [Parameter(Mandatory = $true)]$Processes,
        [Parameter(Mandatory = $true)][string]$Role
    )

    foreach ($process in $Processes) {
        $record = Get-CimInstance Win32_Process -Filter "ProcessId = $($process.Id)" -ErrorAction SilentlyContinue
        if ($null -eq $record -or $record.CommandLine -notlike '*--movement-trace-control*') {
            throw "The running $Role process does not support live trace attachment. Restart it once with the current UIG launcher, then run 'uig trace start'."
        }
    }
}

function Start-UigTrace {
    $servers = @(Get-UigProcesses $script:UigServerExecutable)
    $clients = @(Get-UigTrackedClientProcess 'default')
    if ($servers.Count -gt 0) { Assert-UigRuntimeTraceCapable $servers 'server' }
    if ($clients.Count -gt 0) { Assert-UigRuntimeTraceCapable $clients 'client' }

    New-Item -ItemType Directory -Path (Split-Path -Parent $script:UigTraceControlPath) -Force | Out-Null
    Set-Content -LiteralPath $script:UigTraceControlPath -Value ([DateTime]::UtcNow.ToString('O')) -NoNewline
    Start-UigMovementMonitor -Reset
    if ($servers.Count -gt 0 -and $clients.Count -gt 0) {
        Write-Host 'Runtime tracing enabled for the active server and client.' -ForegroundColor Green
    }
    else {
        Write-Host 'Trace viewer opened in waiting mode. It will connect automatically when the server and client start.' -ForegroundColor Yellow
    }
}

function Stop-UigTrace {
    Remove-Item -LiteralPath $script:UigTraceControlPath -Force -ErrorAction SilentlyContinue
    Stop-UigMovementMonitor
    Write-Host 'Runtime tracing disabled. The server and client are still running.' -ForegroundColor Green
}

function Show-UigTraceStatus {
    $enabled = Test-UigTraceEnabled
    $label = if ($enabled) { 'Trace: enabled' } else { 'Trace: disabled' }
    $color = if ($enabled) { 'Green' } else { 'Gray' }
    Write-Host $label -ForegroundColor $color
    Show-UigServerStatus
    Show-UigClients
    Show-UigMovementMonitorStatus
}

function Start-UigGame {
    Start-UigServer
    Start-UigClient
}

function Stop-UigGame {
    Stop-UigClients
    Stop-UigServer
}

function Restart-UigGame {
    Stop-UigGame
    Start-UigGame
}

function Show-UigGameStatus {
    Show-UigServerStatus
    Show-UigClients
}

function Show-UigGameLogs {
    param([int]$LineCount = 40)
    Write-Host 'Server log' -ForegroundColor Cyan
    Show-UigLog (Join-Path $script:UigRuntimeLogDirectory 'server.log') $LineCount
    Write-Host 'Client log' -ForegroundColor Cyan
    Show-UigLog (Join-Path $script:UigRuntimeLogDirectory 'client-default.log') $LineCount
}

function Stop-UigClient {
    $ClientId = 'default'

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
    $ClientId = 'default'

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
    Write-Host "Action response samples: $script:UigMovementLatencyCsvPath"
    Write-Host "Server frame stalls: $script:UigServerFrameStallCsvPath"
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
    Write-Host '  uig server captures [count]'
    Write-Host '  uig server help'
}

function Show-UigServerCaptures {
    param([int]$Count = 5)

    $logPath = Join-Path $script:UigRuntimeLogDirectory 'server.log'
    $logPaths = @($script:UigServerCaptureHistoryPath, $logPath) |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    if ($logPaths.Count -eq 0) {
        Write-Host "No server capture logs exist under '$script:UigRuntimeLogDirectory'." -ForegroundColor Yellow
        return
    }

    $requested = [Math]::Max(1, $Count)
    $summaryLines = @(Select-String -LiteralPath $logPaths -SimpleMatch '[Server Persistence] Utc=' |
        Where-Object { $_.Line -like '*Queued world checkpoint transaction*' } |
        Select-Object -Last $requested)
    if ($summaryLines.Count -eq 0) {
        Write-Host 'No instrumented world checkpoint captures were found.' -ForegroundColor Yellow
        return
    }

    $rows = foreach ($match in $summaryLines) {
        $line = $match.Line
        [pscustomobject]@{
            Utc = [regex]::Match($line, 'Utc=([^ ]+)').Groups[1].Value
            Frames = [int][regex]::Match($line, 'CaptureFrames=([0-9]+)').Groups[1].Value
            MeanMs = [double][regex]::Match($line, 'MeanCaptureTickMs=([0-9.]+)').Groups[1].Value
            MedianMs = [double][regex]::Match($line, 'MedianCaptureTickMs=([0-9.]+)').Groups[1].Value
            ModeMs = [double][regex]::Match($line, 'ModeCaptureTickMs=([0-9.]+)').Groups[1].Value
            P95Ms = [double][regex]::Match($line, 'P95CaptureTickMs=([0-9.]+)').Groups[1].Value
            P99Ms = [double][regex]::Match($line, 'P99CaptureTickMs=([0-9.]+)').Groups[1].Value
            MaxMs = [double][regex]::Match($line, 'MaxCaptureTickMs=([0-9.]+)').Groups[1].Value
            Slowest = [regex]::Match($line, 'MaxCaptureParticipant=([^ ]+)').Groups[1].Value
            AllocatedMB = [Math]::Round(([double][regex]::Match($line, 'CaptureAllocatedBytes=([0-9]+)').Groups[1].Value / 1MB), 2)
            Reused = [int][regex]::Match($line, 'ReusedParticipants=([0-9]+)').Groups[1].Value
        }
    }

    Write-Host 'World checkpoint capture history:' -ForegroundColor Cyan
    $rows | Format-Table -AutoSize

    $latestRaw = Select-String -LiteralPath $logPaths -SimpleMatch '[Server Persistence Capture] Utc=' | Select-Object -Last 1
    if ($null -eq $latestRaw) { return }
    $sampleText = [regex]::Match($latestRaw.Line, 'Samples=(.*)$').Groups[1].Value
    $samples = foreach ($sample in ($sampleText -split ';')) {
        $parts = $sample -split '\|'
        if ($parts.Count -lt 3) { continue }
        [pscustomobject]@{
            Frame = [int]$parts[0]
            Participant = $parts[1]
            Milliseconds = [double]$parts[2]
            AllocatedBytes = if ($parts.Count -gt 3) { [long]$parts[3] } else { 0L }
        }
    }

    Write-Host 'Latest capture - ten slowest frames:' -ForegroundColor Cyan
    $samples | Sort-Object Milliseconds -Descending | Select-Object -First 10 | Format-Table -AutoSize

    $latestWrite = Select-String -LiteralPath $logPaths -SimpleMatch '[Server Persistence Write] Utc=' | Select-Object -Last 1
    if ($null -ne $latestWrite) {
        $line = $latestWrite.Line
        Write-Host 'Latest background checkpoint write:' -ForegroundColor Cyan
        [pscustomobject]@{
            DeferredMs = [double][regex]::Match($line, 'DeferredSerializationMs=([0-9.]+)').Groups[1].Value
            EnvelopeMs = [double][regex]::Match($line, 'EnvelopeSerializationMs=([0-9.]+)').Groups[1].Value
            AtomicWriteMs = [double][regex]::Match($line, 'AtomicWriteMs=([0-9.]+)').Groups[1].Value
            TotalMs = [double][regex]::Match($line, 'TotalWriteMs=([0-9.]+)').Groups[1].Value
            PayloadMB = [Math]::Round(([double][regex]::Match($line, 'SerializedBytes=([0-9]+)').Groups[1].Value / 1MB), 2)
            Reused = [int][regex]::Match($line, 'ReusedParticipants=([0-9]+)').Groups[1].Value
        } | Format-Table -AutoSize
    }
}

function Show-UigClientHelp {
    Write-Host 'Unity Isekai Game - client commands' -ForegroundColor Cyan
    Write-Host ''
    Write-Host '  uig client start'
    Write-Host '  uig client end'
    Write-Host '  uig client restart'
    Write-Host '  uig client status'
    Write-Host '  uig client logs [lines]'
    Write-Host '  uig client help'
    Write-Host ''
    Write-Host 'Examples:' -ForegroundColor Cyan
    Write-Host '  uig client start'
    Write-Host '  uig trace start'
    Write-Host '  uig trace logs [lines]'
    Write-Host '  uig trace graph <start|end|status>'
}

function Show-UigTraceHelp {
    Write-Host 'Unity Isekai Game - trace commands' -ForegroundColor Cyan
    Write-Host ''
    Write-Host '  uig trace start'
    Write-Host '  uig trace end'
    Write-Host '  uig trace restart'
    Write-Host '  uig trace status'
    Write-Host '  uig trace logs [lines]'
    Write-Host '  uig trace graph <start|end|status>'
    Write-Host '  uig trace help'
    Write-Host ''
    Write-Host 'Trace start opens and arms full action logging. It can run before the game and connects automatically when server/client processes appear.'
    Write-Host 'Use the graph toggles to isolate movement, interactions, UI, inventory, combat, authentication, or system traffic.'
    Write-Host 'Trace end disables tracing without stopping the game.'
}

function Show-UigGameHelp {
    Write-Host 'Unity Isekai Game - combined game commands' -ForegroundColor Cyan
    Write-Host ''
    Write-Host '  uig game start'
    Write-Host '  uig game end'
    Write-Host '  uig game restart'
    Write-Host '  uig game status'
    Write-Host '  uig game logs [lines]'
    Write-Host '  uig game help'
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
    Write-Host '  uig server captures [count]'
    Write-Host '  uig server help'
    Write-Host ''
    Write-Host 'Client:' -ForegroundColor Cyan
    Write-Host '  uig client start|end|restart|status|logs [lines]'
    Write-Host '  uig client help'
    Write-Host ''
    Write-Host 'Game (server + client):' -ForegroundColor Cyan
    Write-Host '  uig game <start|end|restart|status>'
    Write-Host '  uig game logs [lines]'
    Write-Host '  uig game help'
    Write-Host ''
    Write-Host 'Runtime tracing:' -ForegroundColor Cyan
    Write-Host '  uig trace <start|end|status>'
    Write-Host '  uig trace logs [lines]'
    Write-Host '  uig trace graph <start|end|status>'
    Write-Host '  uig trace help'
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
        $traceLabel = if (Test-UigTraceEnabled) { 'enabled' } else { 'disabled' }
        Write-Host "Trace: $traceLabel"
        Show-UigMovementMonitorStatus
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

    if ($target -eq 'movement') {
        throw "The 'uig movement' command was renamed. Use 'uig trace help' for the new syntax."
    }

    if ($target -eq 'both') {
        throw "Use 'uig game help' for combined server and client commands."
    }

    if ($target -eq 'game') {
        if ($Command.Count -lt 2) {
            Show-UigGameHelp
            return
        }
        $gameAction = $Command[1].ToLowerInvariant()
        if ($Command.Count -gt 2 -and $gameAction -notin @('log', 'logs')) {
            throw 'Usage: uig game <start|end|restart|status|logs|help> [lines]'
        }
        switch ($gameAction) {
            { $_ -in @('help', '-h', '--help', '/?') } { Show-UigGameHelp; return }
            'start' { Start-UigGame; return }
            { $_ -in @('stop', 'end') } { Stop-UigGame; return }
            'restart' { Restart-UigGame; return }
            'status' { Show-UigGameStatus; return }
            { $_ -in @('log', 'logs') } {
                $lineCount = if ($Command.Count -gt 2) { [int]$Command[2] } else { 40 }
                Show-UigGameLogs $lineCount
                return
            }
            default { throw "Unknown game command '$gameAction'. Run 'uig game help' for usage." }
        }
    }

    if ($target -eq 'trace') {
        if ($Command.Count -lt 2) {
            Show-UigTraceHelp
            return
        }
        $traceAction = $Command[1].ToLowerInvariant()
        if ($traceAction -in @('help', '-h', '--help', '/?')) {
            Show-UigTraceHelp
            return
        }
        if ($traceAction -in @('start', 'restart')) {
            if ($Command.Count -gt 2) {
                throw "Usage: uig trace $traceAction"
            }
            Start-UigTrace
            return
        }
        if ($traceAction -in @('stop', 'end')) {
            Stop-UigTrace
            return
        }
        if ($traceAction -eq 'status') {
            Show-UigTraceStatus
            return
        }
        if ($traceAction -in @('graph', 'monitor')) {
            $graphAction = if ($Command.Count -gt 2) { $Command[2].ToLowerInvariant() } else { 'status' }
            switch ($graphAction) {
                'start' { Start-UigMovementMonitor; return }
                { $_ -in @('stop', 'end') } { Stop-UigMovementMonitor; return }
                'status' { Show-UigMovementMonitorStatus; return }
                default { throw 'Usage: uig trace graph <start|end|status>' }
            }
        }
        if ($traceAction -notin @('log', 'logs')) {
            throw 'Usage: uig trace <start|end|restart|status|logs|graph|help>'
        }
        $lineCount = 200
        if ($Command.Count -gt 2) {
            $lineCount = [int]$Command[2]
        }
        Show-UigMovementTrace $lineCount
        return
    }

    if ($target -eq 'server') {
        if ($Command.Count -lt 2) {
            Show-UigServerHelp
            return
        }

        $action = $Command[1].ToLowerInvariant()
        if ($Command.Count -gt 2 -and $action -notin @('log', 'logs', 'capture', 'captures')) {
            throw "Tracing is controlled separately. Run 'uig server $action', then 'uig trace start'."
        }
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
            { $_ -in @('capture', 'captures') } {
                $captureCount = 5
                if ($Command.Count -gt 2) {
                    $captureCount = [int]$Command[2]
                }
                Show-UigServerCaptures $captureCount
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
            $action = $Command[1].ToLowerInvariant()
            $argumentOffset = 2
        }
        else { throw "Unknown client command '$($Command[1])'. Run 'uig client help' for usage." }
        if ($Command.Count -gt $argumentOffset -and $action -notin @('log', 'logs')) {
            throw "Tracing is controlled separately. Run 'uig client $action', then 'uig trace start'."
        }
        switch ($action) {
            'start' { Start-UigClient; return }
            { $_ -in @('stop', 'end') } { Stop-UigClient; return }
            'restart' { Stop-UigClient; Start-UigClient; return }
            'status' { Show-UigClientStatus; return }
            { $_ -in @('log', 'logs') } {
                $logPath = Join-Path $script:UigRuntimeLogDirectory 'client-default.log'
                if ($Command.Count -gt $argumentOffset) {
                    [int]$lineCount = $Command[$argumentOffset]
                    Show-UigLog $logPath $lineCount
                }
                else {
                    Show-UigLog $logPath
                }
                return
            }
            default { throw "Unknown client command '$($Command[1])'. Run 'uig client help' for usage." }
        }
    }

    throw "Unknown UIG command '$($Command -join ' ')'. Run 'uig help' for usage."
}
