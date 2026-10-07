# 보컬 립싱크 창의 오디오 미리듣기 호스트.
# Unity 에디터 프리뷰(AudioUtil)가 출력을 못 내는 환경에서도 OS 기본 디바이스로
# 실제 소리를 내기 위해 WPF MediaPlayer를 파일 IPC로 구동한다.
#   cmd.txt    : "seq|verb|arg" 한 줄 (play|ms|경로, seek|ms, pause, stop, quit)
#   status.txt : seq=..;pos=..ms;dur=..ms;playing=0|1;err=..
#   hb.txt     : Unity가 쓰는 하트비트 — 8초 이상 정체 시 자동 종료(고아 방지)
param([Parameter(Mandatory=$true)][string]$Dir)

$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName PresentationCore

$cmdFile = Join-Path $Dir 'cmd.txt'
$stFile  = Join-Path $Dir 'status.txt'
$hbFile  = Join-Path $Dir 'hb.txt'

$player  = New-Object System.Windows.Media.MediaPlayer
$playing = $false
$lastSeq = 0
$errMsg  = ''
$pendingOpenAt = $null   # play 직후 NaturalDuration 대기 시각

try {
while ($true) {
    # 하트비트 감시 — Unity가 죽거나 리로드되면 자동 종료한다.
    $hbOk = (Test-Path $hbFile) -and
        ((Get-Date) - (Get-Item $hbFile).LastWriteTime).TotalSeconds -lt 8
    if (-not $hbOk) { break }

    if (Test-Path $cmdFile) {
        $line = ''
        # C#이 UTF-8로 쓰므로 명시해야 한글 경로가 깨지지 않는다.
        try { $line = (Get-Content $cmdFile -Raw -Encoding UTF8 -ErrorAction SilentlyContinue).Trim() } catch {}
        if ($line -match '^(\d+)\|([a-z]+)\|?(.*)$') {
            $seq  = [int]$Matches[1]
            $verb = $Matches[2]
            $arg  = $Matches[3]
            if ($seq -gt $lastSeq) {
                $lastSeq = $seq
                try {
                    switch ($verb) {
                        'play' {
                            # arg = "ms|경로" — 경로 속 '|'는 NTFS 금지문자라 안전
                            $parts = $arg -split '\|', 2
                            if ($parts.Length -lt 2) { throw 'play 인자 부족' }
                            if (-not (Test-Path $parts[1])) { throw "파일이 없습니다: $($parts[1])" }
                            # '#'·'%'·'?' 등이 포함된 파일명도 열리게 이스케이프
                            $player.Open([Uri]::EscapeUriString(
                                (New-Object System.Uri($parts[1])).AbsoluteUri) -as [Uri])
                            $player.Position = [TimeSpan]::FromMilliseconds([double]$parts[0])
                            $player.Play()
                            $playing = $true
                            $pendingOpenAt = Get-Date
                        }
                        'seek' {
                            $player.Position = [TimeSpan]::FromMilliseconds([double]$arg)
                            if (-not $playing) { $player.Play(); $playing = $true }
                        }
                        'pause' { $player.Pause(); $playing = $false }
                        'stop'  { $player.Stop();  $playing = $false }
                        'quit'  { $player.Close(); exit }
                    }
                } catch {
                    $playing = $false
                    # 상태 파일의 구분자가 오염되지 않게 살균한다.
                    $errMsg = $_.Exception.Message -replace '[;\r\n=]', ' '
                }
            }
        }
    }

    $posMs = 0; $durMs = 0
    try {
        $posMs = [int]$player.Position.TotalMilliseconds
        if ($player.NaturalDuration.HasTimeSpan) {
            $durMs = [int]$player.NaturalDuration.TimeSpan.TotalMilliseconds
            $pendingOpenAt = $null
        }
        # 오픈이 2.5초째 안 끝나면 실패로 보고 — IsPlaying=true+무음 고착 방지
        if ($pendingOpenAt -ne $null -and
            ((Get-Date) - $pendingOpenAt).TotalSeconds -gt 2.5) {
            $playing = $false
            $pendingOpenAt = $null
            if ($errMsg -eq '') { $errMsg = '미디어 열기 실패(미지원 포맷이거나 손상 파일)' }
        }
        # 끝까지 재생되면 stopped로 보고 — C# 측 자연 종료 감지용
        if ($playing -and $durMs -gt 0 -and $posMs -ge $durMs) {
            $playing = $false
        }
    } catch {}
    try {
        Set-Content $stFile -Encoding UTF8 -ErrorAction Stop -Value (
            "seq=$lastSeq;pos=$posMs;dur=$durMs;playing=$([int]$playing);err=$errMsg")
    } catch {}

    Start-Sleep -Milliseconds 50
}
} finally {
    # 종료 시 마지막 status가 남아 "재생 중"으로 오인되지 않게 지운다.
    try { Remove-Item $stFile -Force -ErrorAction SilentlyContinue } catch {}
    try { $player.Close() } catch {}
}
