# Claude Code Stop hook: 대화 기록(.jsonl)에서 질문과 답변의 글만 뽑아 날짜별 Markdown 파일에 덧붙인다.
#
#   저장 위치: (이 PC 사용자의 문서 폴더)\ClaudeLogs\yyyy-MM-dd.md
#   진행 상태: (같은 폴더)\.state\<세션ID>.json  (어디까지 옮겼는지 기억)
#
# 응답이 끝날 때마다 실행되지만, 지난번 이후에 새로 생긴 줄만 처리한다.
# 도구 실행 결과, 생각 과정, 하위 에이전트 기록, 시스템이 붙인 태그는 넣지 않는다.
# 어떤 오류가 나도 Claude Code를 방해하지 않도록 항상 0으로 끝난다.

$ErrorActionPreference = 'Stop'
$OutDir = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'ClaudeLogs'
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Clean-UserText([string]$text) {
    # Claude Code나 IDE가 질문에 붙이는 태그와 그 내용을 지운다.
    $text = [regex]::Replace($text, '(?s)<(system-reminder|ide_opened_file|ide_selection|ide_diagnostics|local-command-stdout|local-command-stderr|command-message|command-args)>.*?</\1>', '')
    $text = [regex]::Replace($text, '(?s)<command-name>(.*?)</command-name>', '$1')
    return $text.Trim()
}

function Get-Texts($content, [bool]$isUser) {
    $parts = @()
    if ($content -is [string]) {
        $parts += $content
    } elseif ($content) {
        foreach ($block in $content) {
            if ($block.type -eq 'text' -and $block.text) { $parts += $block.text }
            elseif ($isUser -and $block.type -eq 'image') { $parts += '[이미지]' }
        }
    }
    $joined = ($parts -join "`n`n")
    if ($isUser) { $joined = Clean-UserText $joined }
    return $joined.Trim()
}

$mutex = $null
try {
    # Claude Code는 입력을 UTF-8로 보낸다. [Console]::In은 시스템 인코딩(CP949)으로 읽어
    # 마지막 답변 등에 들어 있는 한글이 깨지므로, 바이트로 받아 UTF-8로 직접 해석한다.
    $stdinBuffer = New-Object IO.MemoryStream
    [Console]::OpenStandardInput().CopyTo($stdinBuffer)
    $hookInput = $utf8.GetString($stdinBuffer.ToArray()).TrimStart([char]0xFEFF) | ConvertFrom-Json
    $transcript = $hookInput.transcript_path
    $sessionId = $hookInput.session_id
    if (-not $transcript -or -not (Test-Path -LiteralPath $transcript)) { exit 0 }

    # 응답이 빠르게 이어져 hook이 겹쳐 실행되면 같은 줄을 두 번 쓰지 않도록 한 번에 하나만 실행한다.
    $mutex = New-Object System.Threading.Mutex($false, 'Global\ClaudeTranscriptExport')
    if (-not $mutex.WaitOne(60000)) { exit 0 }

    $stateDir = Join-Path $OutDir '.state'
    [IO.Directory]::CreateDirectory($stateDir) | Out-Null
    $statePath = Join-Path $stateDir "$sessionId.json"
    $state = [pscustomobject]@{ lines = 0; lastRole = ''; lastDay = '' }
    if (Test-Path -LiteralPath $statePath) { $state = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json }

    # 기록 파일은 Claude Code가 쓰는 중일 수 있으므로 공유 모드로 연다.
    $stream = [IO.File]::Open($transcript, 'Open', 'Read', 'ReadWrite')
    $reader = New-Object IO.StreamReader($stream, $utf8)
    $lines = New-Object System.Collections.Generic.List[string]
    while ($null -ne ($l = $reader.ReadLine())) { $lines.Add($l) }
    $reader.Close()

    $project = Split-Path -Leaf ([string]$hookInput.cwd)
    $buffers = @{}   # 날짜 → 덧붙일 내용
    $processed = [int]$state.lines

    # 마지막으로 옮긴 글의 시각. 예전 상태 파일에는 없으므로 마지막 날짜의 다음 날 0시로 대신한다.
    $lastTime = [datetime]::MinValue
    if ($state.PSObject.Properties['lastTime']) { $lastTime = [datetime]$state.lastTime }
    elseif ($state.lastDay) { $lastTime = ([datetime]$state.lastDay).AddDays(1) }

    # 대화가 압축되거나 다시 열리면 기록 파일이 새로 쓰여 줄 수가 줄어든다.
    # 그러면 줄 번호를 믿을 수 없으므로 처음부터 읽고, 이미 옮긴 시각까지는 건너뛴다.
    if ($processed -gt $lines.Count) { $processed = 0 }

    for ($i = $processed; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if (-not $line.Trim()) { $processed = $i + 1; continue }
        try { $o = $line | ConvertFrom-Json }
        catch {
            # 마지막 줄이 아직 쓰이는 중이면 다음 실행 때 다시 읽는다.
            if ($i -eq $lines.Count - 1) { break }
            $processed = $i + 1; continue
        }
        $processed = $i + 1

        if ($o.isSidechain -or $o.isMeta -or -not $o.timestamp) { continue }
        if ($o.type -ne 'user' -and $o.type -ne 'assistant') { continue }

        $isUser = ($o.type -eq 'user')
        $text = Get-Texts $o.message.content $isUser
        if (-not $text) { continue }

        $time = ([datetime]$o.timestamp).ToLocalTime()
        if ($time -le $lastTime) { continue }
        $lastTime = $time
        $day = $time.ToString('yyyy-MM-dd')
        $role = if ($isUser) { '나' } else { 'Claude' }

        if (-not $buffers.ContainsKey($day)) { $buffers[$day] = New-Object System.Text.StringBuilder }
        $sb = $buffers[$day]

        if ($state.lastDay -ne $day) {
            # 이 세션이 이 날짜 파일에 처음 기록될 때 구분선을 넣는다.
            [void]$sb.Append("`n---`n`n## 세션 $($sessionId.Substring(0, 8)) · $project`n")
            $state.lastDay = $day
            $state.lastRole = ''
        }
        if ($state.lastRole -ne $role) {
            [void]$sb.Append("`n### $role · $($time.ToString('HH:mm'))`n`n")
            $state.lastRole = $role
        } else {
            [void]$sb.Append("`n")
        }
        [void]$sb.Append($text).Append("`n")
    }

    [IO.Directory]::CreateDirectory($OutDir) | Out-Null
    foreach ($day in ($buffers.Keys | Sort-Object)) {
        $file = Join-Path $OutDir "$day.md"
        if (-not (Test-Path -LiteralPath $file)) {
            [IO.File]::WriteAllText($file, "# $day Claude Code 대화 기록`n", $utf8)
        }
        [IO.File]::AppendAllText($file, $buffers[$day].ToString(), $utf8)
    }

    $state.lines = $processed
    $state | Add-Member -NotePropertyName lastTime -NotePropertyValue $lastTime.ToString('o') -Force
    [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json -Compress), $utf8)
}
catch {
    # 문제가 생겨도 Claude Code는 멈추지 않게 하고, 원인만 남겨 둔다.
    try {
        [IO.Directory]::CreateDirectory($OutDir) | Out-Null
        [IO.File]::AppendAllText((Join-Path $OutDir '.export-error.log'), "$(Get-Date -Format s) $($_.Exception.Message)`n", $utf8)
    } catch {}
}
finally {
    if ($mutex) { try { $mutex.ReleaseMutex() } catch {}; $mutex.Dispose() }
}
exit 0
