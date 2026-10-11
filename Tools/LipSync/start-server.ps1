# 립싱크 로컬 서버 기동 — 프로젝트 venv의 python으로 server.py를 실행한다.
# 사용: powershell -File Tools/LipSync/start-server.ps1 [-Port 17845]
param(
    [int]$Port = 17845
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$venvPy = Join-Path $root "Tools/LipSync/.venv/Scripts/python.exe"
if (-not (Test-Path $venvPy)) {
    Write-Error "프로젝트 venv가 없습니다: $venvPy — Unity 창의 환경 준비를 먼저 실행하세요."
    exit 1
}
$server = Join-Path $PSScriptRoot "server.py"
Write-Host "립싱크 서버 기동: $venvPy $server --port $Port"
& $venvPy $server --port $Port
