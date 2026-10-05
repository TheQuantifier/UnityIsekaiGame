param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,
    [int]$PollMilliseconds = 200,
    [int]$WindowSeconds = 120,
    [int]$MaximumPoints = 1200
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$runtimeLogDirectory = Join-Path $RepositoryRoot 'Logs\Runtime'
$clientLogPath = Join-Path $runtimeLogDirectory 'client-default.log'
$serverLogPath = Join-Path $runtimeLogDirectory 'server.log'
$clientPidPath = Join-Path $runtimeLogDirectory 'client-default.pid'
$csvPath = Join-Path $runtimeLogDirectory 'action-trace.csv'
$stallCsvPath = Join-Path $runtimeLogDirectory 'server-frame-stalls.csv'
$pidPath = Join-Path $runtimeLogDirectory 'movement-latency-monitor.pid'
$readyPath = Join-Path $runtimeLogDirectory 'movement-latency-monitor.ready'
$errorLogPath = Join-Path $runtimeLogDirectory 'movement-latency-monitor-error.log'

New-Item -ItemType Directory -Path $runtimeLogDirectory -Force | Out-Null

function New-TraceTailState {
    param([Parameter(Mandatory = $true)][string]$Path)

    $isPresent = Test-Path -LiteralPath $Path -PathType Leaf
    $initialLength = if ($isPresent) {
        (Get-Item -LiteralPath $Path).Length
    }
    else {
        0L
    }
    @{
        Path = $Path
        Offset = $initialLength
        Carry = ''
        WasPresent = $isPresent
    }
}

function Read-NewTraceLines {
    param([Parameter(Mandatory = $true)][hashtable]$State)

    $result = [pscustomobject]@{ Lines = @(); Reset = $false }
    if (-not (Test-Path -LiteralPath $State.Path -PathType Leaf)) {
        if ($State.WasPresent) {
            $State.Offset = 0L
            $State.Carry = ''
            $State.WasPresent = $false
            $result.Reset = $true
        }
        return $result
    }

    $State.WasPresent = $true
    $stream = [System.IO.File]::Open(
        $State.Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete)
    try {
        if ($stream.Length -lt $State.Offset) {
            $State.Offset = 0L
            $State.Carry = ''
            $result.Reset = $true
        }
        if ($stream.Length -eq $State.Offset) {
            return $result
        }

        $stream.Position = $State.Offset
        $remaining = [int]($stream.Length - $stream.Position)
        $buffer = New-Object byte[] $remaining
        $read = $stream.Read($buffer, 0, $buffer.Length)
        $State.Offset = $stream.Position
        if ($read -le 0) {
            return $result
        }

        $text = $State.Carry + [System.Text.Encoding]::UTF8.GetString($buffer, 0, $read)
        $parts = [regex]::Split($text, "\r?\n")
        if ($text.EndsWith("`n")) {
            $State.Carry = ''
            if ($parts.Count -gt 0 -and $parts[$parts.Count - 1].Length -eq 0) {
                $parts = @($parts[0..($parts.Count - 2)])
            }
        }
        else {
            $State.Carry = $parts[$parts.Count - 1]
            if ($parts.Count -eq 1) {
                $parts = @()
            }
            else {
                $parts = @($parts[0..($parts.Count - 2)])
            }
        }

        $result.Lines = @($parts | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        return $result
    }
    finally {
        $stream.Dispose()
    }
}

function Convert-ToUtcDateTime {
    param([Parameter(Mandatory = $true)][string]$Value)

    [DateTimeOffset]::Parse(
        $Value,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind).UtcDateTime
}

function Get-Percentile {
    param(
        [Parameter(Mandatory = $true)][double[]]$SortedValues,
        [Parameter(Mandatory = $true)][double]$Percentile
    )

    if ($SortedValues.Count -eq 0) { return 0d }
    $index = [Math]::Max(0, [Math]::Min(
        $SortedValues.Count - 1,
        [int][Math]::Ceiling($Percentile * $SortedValues.Count) - 1))
    return $SortedValues[$index]
}

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class UigLatencyMonitorConsole
{
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr window, int command);
}
'@
$consoleWindow = [UigLatencyMonitorConsole]::GetConsoleWindow()
if ($consoleWindow -ne [IntPtr]::Zero) {
    [void][UigLatencyMonitorConsole]::ShowWindow($consoleWindow, 0)
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms.DataVisualization

[System.Windows.Forms.Application]::EnableVisualStyles()

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Unity Isekai - Full Action Trace'
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
$form.Width = 1100
$form.Height = 650
$form.MinimumSize = New-Object System.Drawing.Size(760, 440)
$form.BackColor = [System.Drawing.Color]::FromArgb(30, 22, 15)
$form.ForeColor = [System.Drawing.Color]::FromArgb(241, 211, 145)

$header = New-Object System.Windows.Forms.Panel
$header.Dock = [System.Windows.Forms.DockStyle]::Top
$header.Height = 174
$header.Padding = New-Object System.Windows.Forms.Padding(14, 10, 14, 6)
$header.BackColor = [System.Drawing.Color]::FromArgb(62, 39, 22)
$form.Controls.Add($header)

$titleLabel = New-Object System.Windows.Forms.Label
$titleLabel.Dock = [System.Windows.Forms.DockStyle]::Top
$titleLabel.Height = 30
$titleLabel.Text = 'FULL ACTION TRACE - RESPONSE TIME'
$titleLabel.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 15, [System.Drawing.FontStyle]::Bold)
$titleLabel.ForeColor = [System.Drawing.Color]::FromArgb(239, 184, 75)
$header.Controls.Add($titleLabel)

$statsLabel = New-Object System.Windows.Forms.Label
$statsLabel.Dock = [System.Windows.Forms.DockStyle]::Top
$statsLabel.Height = 27
$statsLabel.Text = 'Waiting for traced action samples...'
$statsLabel.Font = New-Object System.Drawing.Font('Consolas', 10, [System.Drawing.FontStyle]::Bold)
$statsLabel.ForeColor = [System.Drawing.Color]::FromArgb(245, 226, 184)
$header.Controls.Add($statsLabel)

$connectionLabel = New-Object System.Windows.Forms.Label
$connectionLabel.Dock = [System.Windows.Forms.DockStyle]::Top
$connectionLabel.Height = 22
$connectionLabel.Text = 'WAITING FOR GAME - trace viewer is armed and will connect automatically.'
$connectionLabel.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 9.5, [System.Drawing.FontStyle]::Bold)
$connectionLabel.ForeColor = [System.Drawing.Color]::FromArgb(255, 177, 52)
$header.Controls.Add($connectionLabel)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Dock = [System.Windows.Forms.DockStyle]::Top
$statusLabel.Height = 20
$statusLabel.Text = 'Every point is server receive time minus its matching client send time.'
$statusLabel.Font = New-Object System.Drawing.Font('Segoe UI', 9)
$statusLabel.ForeColor = [System.Drawing.Color]::FromArgb(196, 167, 114)
$header.Controls.Add($statusLabel)

$traceCategories = @('Movement', 'Interaction', 'UI', 'Inventory', 'Combat', 'Authentication', 'System')
$summaryLines = @('Total', 'Background', 'Game Change')
$traceFilters = @($traceCategories) + @($summaryLines)
$categoryEnabled = @{}
$categoryButtons = @{}
$filterPanel = New-Object System.Windows.Forms.FlowLayoutPanel
$filterPanel.Dock = [System.Windows.Forms.DockStyle]::Bottom
$filterPanel.Height = 60
$filterPanel.Padding = New-Object System.Windows.Forms.Padding(10, 3, 0, 2)
$filterPanel.WrapContents = $true
$filterPanel.AutoScroll = $true
$filterPanel.BackColor = [System.Drawing.Color]::FromArgb(50, 31, 18)
$header.Controls.Add($filterPanel)

foreach ($category in $traceFilters) {
    $categoryEnabled[$category] = $true
    $button = New-Object System.Windows.Forms.CheckBox
    $button.Appearance = [System.Windows.Forms.Appearance]::Normal
    $button.AutoSize = $true
    $button.Height = 24
    $button.Margin = New-Object System.Windows.Forms.Padding(5, 2, 12, 2)
    $button.Padding = New-Object System.Windows.Forms.Padding(0)
    $button.FlatStyle = [System.Windows.Forms.FlatStyle]::Standard
    $button.BackColor = [System.Drawing.Color]::Transparent
    $button.ForeColor = [System.Drawing.Color]::FromArgb(239, 211, 151)
    $button.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 9, [System.Drawing.FontStyle]::Regular)
    $button.CheckAlign = [System.Drawing.ContentAlignment]::MiddleLeft
    $button.TextAlign = [System.Drawing.ContentAlignment]::MiddleLeft
    $button.Text = $category
    $button.Checked = $true
    $button.Tag = $category
    $categoryButtons[$category] = $button
    $filterPanel.Controls.Add($button)
}

$chart = New-Object System.Windows.Forms.DataVisualization.Charting.Chart
$chart.Dock = [System.Windows.Forms.DockStyle]::Fill
$chart.BackColor = [System.Drawing.Color]::FromArgb(30, 22, 15)
$chart.Palette = [System.Windows.Forms.DataVisualization.Charting.ChartColorPalette]::None
$chart.PaletteCustomColors = @([System.Drawing.Color]::FromArgb(76, 190, 230))
$chartArea = New-Object System.Windows.Forms.DataVisualization.Charting.ChartArea('Delivery')
$chartArea.BackColor = [System.Drawing.Color]::FromArgb(40, 29, 20)
$chartArea.AxisX.Title = 'Server receipt time (UTC)'
$chartArea.AxisX.LabelStyle.Format = 'HH:mm:ss'
$chartArea.AxisX.LabelStyle.ForeColor = [System.Drawing.Color]::FromArgb(205, 181, 133)
$chartArea.AxisX.TitleForeColor = [System.Drawing.Color]::FromArgb(205, 181, 133)
$chartArea.AxisX.LineColor = [System.Drawing.Color]::FromArgb(116, 79, 39)
$chartArea.AxisX.MajorGrid.LineColor = [System.Drawing.Color]::FromArgb(66, 48, 33)
$chartArea.AxisY.Title = 'Difference (ms)'
$chartArea.AxisY.Minimum = 0d
$chartArea.AxisY.Maximum = 100d
$chartArea.AxisY.Interval = 10d
$chartArea.AxisY.IsStartedFromZero = $true
$chartArea.AxisY.LabelStyle.ForeColor = [System.Drawing.Color]::FromArgb(205, 181, 133)
$chartArea.AxisY.TitleForeColor = [System.Drawing.Color]::FromArgb(205, 181, 133)
$chartArea.AxisY.LineColor = [System.Drawing.Color]::FromArgb(116, 79, 39)
$chartArea.AxisY.MajorGrid.LineColor = [System.Drawing.Color]::FromArgb(66, 48, 33)
$chartArea.AxisY.MajorGrid.Interval = 10d
$chartArea.AxisY.MajorTickMark.Interval = 10d
$chartArea.AxisY.MinorTickMark.Enabled = $true
$chartArea.AxisY.MinorTickMark.Interval = 1d
$chartArea.AxisY.MinorTickMark.LineColor = [System.Drawing.Color]::FromArgb(116, 79, 39)
$chartArea.AxisY.MinorGrid.Enabled = $true
$chartArea.AxisY.MinorGrid.Interval = 1d
$chartArea.AxisY.MinorGrid.LineColor = [System.Drawing.Color]::FromArgb(50, 38, 28)
$chartArea.AxisY.MinorGrid.LineDashStyle = [System.Windows.Forms.DataVisualization.Charting.ChartDashStyle]::Dot
$chart.ChartAreas.Add($chartArea)

$categorySeries = @{}
$categoryColors = @{
    Movement = [System.Drawing.Color]::FromArgb(76, 190, 230)
    Interaction = [System.Drawing.Color]::FromArgb(238, 184, 75)
    UI = [System.Drawing.Color]::FromArgb(215, 146, 230)
    Inventory = [System.Drawing.Color]::FromArgb(116, 214, 126)
    Combat = [System.Drawing.Color]::FromArgb(238, 98, 82)
    Authentication = [System.Drawing.Color]::FromArgb(105, 215, 197)
    System = [System.Drawing.Color]::FromArgb(180, 180, 190)
}

function New-ResponseSeries {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][System.Drawing.Color]$Color,
        [int]$Width = 2,
        [System.Windows.Forms.DataVisualization.Charting.ChartDashStyle]$DashStyle = [System.Windows.Forms.DataVisualization.Charting.ChartDashStyle]::Solid
    )

    $created = New-Object System.Windows.Forms.DataVisualization.Charting.Series($Name)
    $created.ChartType = [System.Windows.Forms.DataVisualization.Charting.SeriesChartType]::FastLine
    $created.XValueType = [System.Windows.Forms.DataVisualization.Charting.ChartValueType]::DateTime
    $created.YValueType = [System.Windows.Forms.DataVisualization.Charting.ChartValueType]::Double
    $created.BorderWidth = $Width
    $created.BorderDashStyle = $DashStyle
    $created.Color = $Color
    $created.ToolTip = "$Name | #VALY{F2} ms"
    [void]$chart.Series.Add($created)
    return $created
}

foreach ($category in $traceCategories) {
    $categorySeries[$category] = New-ResponseSeries "$category response" $categoryColors[$category] 2
}
$totalSeries = New-ResponseSeries 'Total response change' ([System.Drawing.Color]::FromArgb(250, 245, 225)) 3
$backgroundSeries = New-ResponseSeries 'Background response change' ([System.Drawing.Color]::FromArgb(145, 145, 155)) 2 ([System.Windows.Forms.DataVisualization.Charting.ChartDashStyle]::Dash)
$gameChangeSeries = New-ResponseSeries 'Game response change' ([System.Drawing.Color]::FromArgb(255, 164, 45)) 4

function Update-SeriesVisibility {
    foreach ($category in $traceCategories) {
        $categorySeries[$category].Enabled = $categoryEnabled[$category] -ne $false
    }
    $totalSeries.Enabled = $categoryEnabled.Total -ne $false
    $backgroundSeries.Enabled = $categoryEnabled.Background -ne $false
    $gameChangeSeries.Enabled = $categoryEnabled.'Game Change' -ne $false
}

foreach ($category in $traceFilters) {
    $button = $categoryButtons[$category]
    $button.Add_CheckedChanged({
        $selectedCategory = [string]$this.Tag
        $categoryEnabled[$selectedCategory] = $this.Checked
        Update-SeriesVisibility
        Update-VisibleSamples
    })
}

$legend = New-Object System.Windows.Forms.DataVisualization.Charting.Legend
$legend.Docking = [System.Windows.Forms.DataVisualization.Charting.Docking]::Bottom
$legend.BackColor = [System.Drawing.Color]::FromArgb(40, 29, 20)
$legend.ForeColor = [System.Drawing.Color]::FromArgb(220, 195, 146)
$chart.Legends.Add($legend)
$form.Controls.Add($chart)
$chart.BringToFront()

$clientTail = New-TraceTailState $clientLogPath
$serverTail = New-TraceTailState $serverLogPath
$clientSends = @{}
$serverReceives = @{}
$samples = New-Object System.Collections.ArrayList
$expiredClientSamples = 0
$expiredServerSamples = 0
$nextConnectionCheck = [DateTime]::MinValue

function Test-UigTraceProcess {
    param([Parameter(Mandatory = $true)][string]$ProcessName, [string]$PidFile)

    if (-not [string]::IsNullOrWhiteSpace($PidFile) -and (Test-Path -LiteralPath $PidFile -PathType Leaf)) {
        $stored = Get-Content -LiteralPath $PidFile -Raw -ErrorAction SilentlyContinue
        [int]$processId = 0
        $storedText = if ($null -eq $stored) { '' } else { [string]$stored }
        if ([int]::TryParse($storedText.Trim(), [ref]$processId)) {
            $tracked = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($null -ne $tracked -and $tracked.ProcessName -eq $ProcessName) { return $true }
        }
    }
    return $null -ne (Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1)
}

function Update-ConnectionBanner {
    $now = [DateTime]::UtcNow
    if ($now -lt $script:nextConnectionCheck) { return }
    $script:nextConnectionCheck = $now.AddSeconds(1)

    $serverRunning = Test-UigTraceProcess 'UnityIsekaiServer'
    $clientRunning = Test-UigTraceProcess 'UnityIsekaiClient' $clientPidPath
    if ($serverRunning -and $clientRunning) {
        $connectionLabel.Text = 'CONNECTED - server and client are running; matching action traffic automatically.'
        $connectionLabel.ForeColor = [System.Drawing.Color]::FromArgb(116, 214, 126)
        return
    }

    $missing = if (-not $serverRunning -and -not $clientRunning) {
        'server and client'
    }
    elseif (-not $serverRunning) {
        'server'
    }
    else {
        'client'
    }
    $connectionLabel.Text = "WAITING FOR $($missing.ToUpperInvariant()) - viewer is armed and will connect automatically."
    $connectionLabel.ForeColor = [System.Drawing.Color]::FromArgb(255, 177, 52)
}

$csvStream = New-Object System.IO.FileStream(
    $csvPath,
    [System.IO.FileMode]::Append,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::ReadWrite)
$csvWriter = New-Object System.IO.StreamWriter($csvStream, (New-Object System.Text.UTF8Encoding($false)))
$csvWriter.AutoFlush = $true
if ($csvStream.Length -eq 0) {
    $csvWriter.WriteLine('ClientUtc,ServerUtc,Category,Action,Correlation,Client,Delivery,Background,LatencyMs')
}

$stallCsvStream = New-Object System.IO.FileStream(
    $stallCsvPath,
    [System.IO.FileMode]::Append,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::ReadWrite)
$stallCsvWriter = New-Object System.IO.StreamWriter($stallCsvStream, (New-Object System.Text.UTF8Encoding($false)))
$stallCsvWriter.AutoFlush = $true
if ($stallCsvStream.Length -eq 0) {
    $stallCsvWriter.WriteLine('Utc,Frame,FrameGapMs,Source,LongestPhase,PhaseMs,Gc0,Gc1,Gc2,NetworkInputs,ReliableInputs,FirstSequence,LastSequence,SequenceSpan')
}

function Add-MatchedDeliverySample {
    param(
        [Parameter(Mandatory = $true)]$ClientSample,
        [Parameter(Mandatory = $true)]$ServerSample
    )

    $latency = [Math]::Max(0d, ($ServerSample.Utc - $ClientSample.Utc).TotalMilliseconds)
    $isBackground = $ServerSample.Delivery -eq 'background'
    $sample = [pscustomobject]@{
        ClientUtc = $ClientSample.Utc
        ServerUtc = $ServerSample.Utc
        Category = $ClientSample.Category
        Action = $ClientSample.Action
        Correlation = $ClientSample.Correlation
        Delivery = $ServerSample.Delivery
        Client = $ServerSample.Client
        IsBackground = $isBackground
        LatencyMs = $latency
        TotalPointAdded = $false
        BackgroundPointAdded = $false
        GameChangePointAdded = $false
    }
    [void]$samples.Add($sample)
    $categoryLine = if ($categorySeries.ContainsKey($sample.Category)) {
        $categorySeries[$sample.Category]
    }
    else {
        $categorySeries.System
    }
    $pointIndex = $categoryLine.Points.AddXY($sample.ServerUtc, $sample.LatencyMs)
    $categoryLine.Points[$pointIndex].ToolTip = '{0} / {1} | {2} | {3:F2} ms' -f
        $sample.Category, $sample.Action, $sample.Correlation, $sample.LatencyMs

    $recentGame = @($samples | Where-Object { -not $_.IsBackground } | Select-Object -Last 30)
    $recentBackground = @($samples | Where-Object { $_.IsBackground } | Select-Object -Last 20)
    if ($recentGame.Count -gt 0) {
        $total = ($recentGame | Measure-Object -Property LatencyMs -Average).Average
        [void]$totalSeries.Points.AddXY($sample.ServerUtc, $total)
        $sample.TotalPointAdded = $true
    }
    if ($recentBackground.Count -gt 0) {
        $background = ($recentBackground | Measure-Object -Property LatencyMs -Average).Average
        [void]$backgroundSeries.Points.AddXY($sample.ServerUtc, $background)
        $sample.BackgroundPointAdded = $true
    }
    if ($recentGame.Count -gt 0 -and $recentBackground.Count -gt 0) {
        $gameChange = [Math]::Max(0d, $total - $background)
        [void]$gameChangeSeries.Points.AddXY($sample.ServerUtc, $gameChange)
        $sample.GameChangePointAdded = $true
    }

    $csvLine = '"{0:O}","{1:O}","{2}","{3}","{4}","{5}","{6}",{7},{8}' -f @(
            $sample.ClientUtc,
            $sample.ServerUtc,
            $sample.Category.Replace('"', '""'),
            $sample.Action.Replace('"', '""'),
            $sample.Correlation.Replace('"', '""'),
            $sample.Client.Replace('"', '""'),
            $sample.Delivery.Replace('"', '""'),
            $sample.IsBackground.ToString().ToLowerInvariant(),
            $sample.LatencyMs.ToString('F3', [Globalization.CultureInfo]::InvariantCulture))
    $csvWriter.WriteLine($csvLine)
}

function Add-PendingClientSample {
    param([Parameter(Mandatory = $true)][string]$Key, [Parameter(Mandatory = $true)]$Sample)

    if ($serverReceives.ContainsKey($Key)) {
        Add-MatchedDeliverySample $Sample $serverReceives[$Key]
        $serverReceives.Remove($Key)
    }
    else {
        $clientSends[$Key] = $Sample
    }
}

function Add-PendingServerSample {
    param([Parameter(Mandatory = $true)][string]$Key, [Parameter(Mandatory = $true)]$Sample)

    if ($clientSends.ContainsKey($Key)) {
        Add-MatchedDeliverySample $clientSends[$Key] $Sample
        $clientSends.Remove($Key)
    }
    else {
        $serverReceives[$Key] = $Sample
    }
}

function Add-ClientTraceLine {
    param([Parameter(Mandatory = $true)][string]$Line)

    $actionMatch = [regex]::Match(
        $Line,
        '\[Action Timing\]\[ClientSend\] utc=([^ ]+) category=([^ ]+) action=([^ ]+) correlation=([^ ]+) client=([^ ]+) delivery=([^ ]+)')
    if ($actionMatch.Success) {
        $category = $actionMatch.Groups[2].Value
        $action = $actionMatch.Groups[3].Value
        $correlation = $actionMatch.Groups[4].Value
        $key = "action|$category|$action|$correlation"
        Add-PendingClientSample $key ([pscustomobject]@{
                Utc = Convert-ToUtcDateTime $actionMatch.Groups[1].Value
                Category = $category
                Action = $action
                Correlation = $correlation
                Delivery = $actionMatch.Groups[6].Value
                AddedAt = [DateTime]::UtcNow
            })
        return
    }

    $movementMatch = [regex]::Match(
        $Line,
        '\[Movement Timing\]\[ClientSend\] utc=([^ ]+) actor=([^ ]+) seq=([0-9]+)')
    if (-not $movementMatch.Success) { return }
    $actor = $movementMatch.Groups[2].Value
    $sequence = $movementMatch.Groups[3].Value
    $key = "movement|$actor|$sequence"
    Add-PendingClientSample $key ([pscustomobject]@{
            Utc = Convert-ToUtcDateTime $movementMatch.Groups[1].Value
            Category = 'Movement'
            Action = 'Input'
            Correlation = "$actor`:$sequence"
            Delivery = 'game-unreliable'
            AddedAt = [DateTime]::UtcNow
        })
}

function Write-ServerStallSample {
    param([Parameter(Mandatory = $true)][System.Text.RegularExpressions.Match]$Match)

    $stall = [pscustomobject]@{
        Utc = Convert-ToUtcDateTime $Match.Groups[1].Value
        Frame = [int]$Match.Groups[2].Value
        FrameGapMs = [double]::Parse($Match.Groups[3].Value, [Globalization.CultureInfo]::InvariantCulture)
        Source = $Match.Groups[4].Value
        LongestPhase = $Match.Groups[5].Value
        PhaseMs = [double]::Parse($Match.Groups[6].Value, [Globalization.CultureInfo]::InvariantCulture)
        Gc0 = [int]$Match.Groups[7].Value
        Gc1 = [int]$Match.Groups[8].Value
        Gc2 = [int]$Match.Groups[9].Value
        NetworkInputs = [int]$Match.Groups[10].Value
        ReliableInputs = [int]$Match.Groups[11].Value
        FirstSequence = [uint32]$Match.Groups[12].Value
        LastSequence = [uint32]$Match.Groups[13].Value
        SequenceSpan = [uint32]$Match.Groups[14].Value
    }
    $stallCsvLine = '"{0:O}",{1},{2},"{3}","{4}",{5},{6},{7},{8},{9},{10},{11},{12},{13}' -f @(
        $stall.Utc,
        $stall.Frame,
        $stall.FrameGapMs.ToString('F3', [Globalization.CultureInfo]::InvariantCulture),
        $stall.Source.Replace('"', '""'),
        $stall.LongestPhase.Replace('"', '""'),
        $stall.PhaseMs.ToString('F3', [Globalization.CultureInfo]::InvariantCulture),
        $stall.Gc0,
        $stall.Gc1,
        $stall.Gc2,
        $stall.NetworkInputs,
        $stall.ReliableInputs,
        $stall.FirstSequence,
        $stall.LastSequence,
        $stall.SequenceSpan)
    $stallCsvWriter.WriteLine($stallCsvLine)
}

function Add-ServerTraceLine {
    param([Parameter(Mandatory = $true)][string]$Line)

    $stallMatch = [regex]::Match(
        $Line,
        '\[Server Frame Stall\] utc=([^ ]+) frame=([0-9]+) frameGapMs=([0-9.]+).*source=([^ ]+).*longestPhase=([^ ]+) phaseMs=([0-9.]+) gc0=([0-9-]+) gc1=([0-9-]+) gc2=([0-9-]+).*networkInputs=([0-9]+) reliableInputs=([0-9]+) firstSeq=([0-9]+) lastSeq=([0-9]+) sequenceSpan=([0-9]+)')
    if ($stallMatch.Success) {
        Write-ServerStallSample $stallMatch
        return
    }

    $actionMatch = [regex]::Match(
        $Line,
        '\[Action Timing\]\[ServerReceive\] utc=([^ ]+) category=([^ ]+) action=([^ ]+) correlation=([^ ]+) client=([^ ]+) delivery=([^ ]+)')
    if ($actionMatch.Success) {
        $category = $actionMatch.Groups[2].Value
        $action = $actionMatch.Groups[3].Value
        $correlation = $actionMatch.Groups[4].Value
        $key = "action|$category|$action|$correlation"
        Add-PendingServerSample $key ([pscustomobject]@{
                Utc = Convert-ToUtcDateTime $actionMatch.Groups[1].Value
                Category = $category
                Action = $action
                Correlation = $correlation
                Client = $actionMatch.Groups[5].Value
                Delivery = $actionMatch.Groups[6].Value
                AddedAt = [DateTime]::UtcNow
            })
        return
    }

    $movementMatch = [regex]::Match(
        $Line,
        '\[Movement Timing\]\[ServerReceive\] utc=([^ ]+) actor=([^ ]+) client=([^ ]+) seq=([0-9]+) delivery=([^ ]+)')
    if (-not $movementMatch.Success) { return }
    $actor = $movementMatch.Groups[2].Value
    $sequence = $movementMatch.Groups[4].Value
    $key = "movement|$actor|$sequence"
    Add-PendingServerSample $key ([pscustomobject]@{
            Utc = Convert-ToUtcDateTime $movementMatch.Groups[1].Value
            Category = 'Movement'
            Action = 'Input'
            Correlation = "$actor`:$sequence"
            Client = $movementMatch.Groups[3].Value
            Delivery = $movementMatch.Groups[5].Value
            AddedAt = [DateTime]::UtcNow
        })
}

function Remove-ExpiredPendingSamples {
    $cutoff = [DateTime]::UtcNow.AddSeconds(-10)
    foreach ($key in @($clientSends.Keys)) {
        if ($clientSends[$key].AddedAt -lt $cutoff) {
            $clientSends.Remove($key)
            $script:expiredClientSamples++
        }
    }
    foreach ($key in @($serverReceives.Keys)) {
        if ($serverReceives[$key].AddedAt -lt $cutoff) {
            $serverReceives.Remove($key)
            $script:expiredServerSamples++
        }
    }
}

function Update-VisibleSamples {
    $cutoff = [DateTime]::UtcNow.AddSeconds(-$WindowSeconds)
    while ($samples.Count -gt 0 -and
           ($samples[0].ServerUtc -lt $cutoff -or $samples.Count -gt $MaximumPoints)) {
        $expiredSample = $samples[0]
        $samples.RemoveAt(0)
        $expiredCategory = if ($categorySeries.ContainsKey($expiredSample.Category)) {
            $categorySeries[$expiredSample.Category]
        }
        else {
            $categorySeries.System
        }
        if ($expiredCategory.Points.Count -gt 0) { $expiredCategory.Points.RemoveAt(0) }
        if ($expiredSample.TotalPointAdded -and $totalSeries.Points.Count -gt 0) { $totalSeries.Points.RemoveAt(0) }
        if ($expiredSample.BackgroundPointAdded -and $backgroundSeries.Points.Count -gt 0) { $backgroundSeries.Points.RemoveAt(0) }
        if ($expiredSample.GameChangePointAdded -and $gameChangeSeries.Points.Count -gt 0) { $gameChangeSeries.Points.RemoveAt(0) }
    }

    if ($samples.Count -eq 0) {
        $statsLabel.Text = 'Waiting for matching client-send/server-receive samples...'
        $statusLabel.Text = 'Select action families and summary lines above. Server-only timings are recorded separately and never plotted here.'
        return
    }

    $visibleSamples = @($samples | Where-Object { $categoryEnabled[$_.Category] -ne $false })
    if ($visibleSamples.Count -eq 0) {
        $statsLabel.Text = 'No samples are visible with the current action-family filters.'
        return
    }
    [double[]]$values = @($visibleSamples | ForEach-Object { [double]$_.LatencyMs } | Sort-Object)
    $measure = $values | Measure-Object -Average -Maximum
    $median = if ($values.Count % 2 -eq 1) {
        $values[[int][Math]::Floor($values.Count / 2)]
    }
    else {
        ($values[$values.Count / 2 - 1] + $values[$values.Count / 2]) / 2d
    }
    $p95 = Get-Percentile $values 0.95d
    $current = $visibleSamples[$visibleSamples.Count - 1].LatencyMs
    $statsLabel.Text =
        'CURRENT {0,7:F2} ms   MEAN {1,7:F2} ms   MEDIAN {2,7:F2} ms   P95 {3,7:F2} ms   MAX {4,7:F2} ms   N {5}' -f
            $current, $measure.Average, $median, $p95, $measure.Maximum, $visibleSamples.Count
    $statusLabel.Text = 'Family lines = raw client-to-server difference. Game Response Change = rolling total action response - rolling background response.'
}

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = [Math]::Max(50, $PollMilliseconds)
$timer.Add_Tick({
    try {
        Update-ConnectionBanner
        $clientResult = Read-NewTraceLines $clientTail
        $serverResult = Read-NewTraceLines $serverTail
        if ($clientResult.Reset -or $serverResult.Reset) {
            $clientSends.Clear()
            $serverReceives.Clear()
        }
        foreach ($line in $clientResult.Lines) { Add-ClientTraceLine $line }
        foreach ($line in $serverResult.Lines) { Add-ServerTraceLine $line }
        Remove-ExpiredPendingSamples
        Update-VisibleSamples
    }
    catch {
        $statusLabel.Text = "Monitor error: $($_.Exception.Message)"
        $statusLabel.ForeColor = [System.Drawing.Color]::FromArgb(238, 98, 82)
        Add-Content -LiteralPath $errorLogPath -Value (
            '[{0:O}] {1}{2}{3}' -f [DateTime]::UtcNow, $_.Exception, [Environment]::NewLine, $_.ScriptStackTrace)
    }
})

$form.Add_FormClosed({
    $timer.Stop()
    $timer.Dispose()
    $csvWriter.Dispose()
    $csvStream.Dispose()
    $stallCsvWriter.Dispose()
    $stallCsvStream.Dispose()
    Remove-Item -LiteralPath $pidPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $readyPath -Force -ErrorAction SilentlyContinue
})

Set-Content -LiteralPath $pidPath -Value $PID -NoNewline
Set-Content -LiteralPath $readyPath -Value $PID -NoNewline
$timer.Start()
[System.Windows.Forms.Application]::Run($form)
