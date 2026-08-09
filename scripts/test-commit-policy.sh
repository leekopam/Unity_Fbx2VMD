#!/usr/bin/env bash
set -euo pipefail

HOOK="$(git rev-parse --show-toplevel)/scripts/hooks/commit-msg"
MESSAGE_FILE="$(mktemp)"
trap 'rm -f "$MESSAGE_FILE"' EXIT

run_case() {
  local name="$1"
  local expected_exit="$2"
  local message="$3"
  local actual_exit=0

  printf '%s' "$message" > "$MESSAGE_FILE"
  if bash "$HOOK" "$MESSAGE_FILE" >/dev/null 2>&1; then
    actual_exit=0
  else
    actual_exit=1
  fi

  if [ "$actual_exit" -ne "$expected_exit" ]; then
    printf 'FAIL: %s (expected=%s actual=%s)\n' "$name" "$expected_exit" "$actual_exit"
    exit 1
  fi
  printf 'PASS: %s\n' "$name"
}

run_case 'valid code commit' 0 $'refactor(fbx-import): Material 생성을 분리\n\n생성 책임을 분리해 importer 변경 영향을 줄였다.\n검증: EditMode Tests PASS\n'
run_case 'code commit needs scope' 1 $'fix: 입력 경계를 수정\n\n입력 검증 누락을 보완했다.\n검증: EditMode Tests PASS\n'
run_case 'code commit needs body' 1 $'perf(recording): 녹화 경로를 최적화\n'
run_case 'code commit needs verification' 1 $'fix(recording): 녹화 실패를 수정\n\n상태 전이 누락을 보완했다.\n'
run_case 'breaking change syntax' 0 $'feat(fbx-import)!: 입력 형식을 변경\n\n새 형식으로 입력 계약을 변경했다.\n검증: EditMode Tests PASS\n\nBREAKING CHANGE: 이전 형식은 지원하지 않는다.\n'
run_case 'docs can omit scope and body' 0 $'docs: 커밋 규칙을 설명\n'
run_case 'unknown scope rejected' 1 $'fix(unknown): 입력 경계를 수정\n\n입력 검증 누락을 보완했다.\n검증: EditMode Tests PASS\n'
