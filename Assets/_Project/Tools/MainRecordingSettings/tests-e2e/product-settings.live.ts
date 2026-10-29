import fs from "node:fs/promises";
import path from "node:path";
import assert from "node:assert/strict";
import { test } from "@e2e-dev/web";
import { expect } from "e2e";
import { createUnityClient, UNITY_COMMANDS } from "./unity/unityClient.js";

test("실제 설정창 등록·FBX·재생 제어와 보정 실패 비교 경로를 확인한다", { timeout: 600000 }, async ({ app, screen, browser }) => {
  const fixture = JSON.parse(await fs.readFile(process.env.FBX2VMD_E2E_SESSION_FILE!, "utf8"));
  const unity = createUnityClient({ projectRoot: fixture.projectRoot });
  const read = async (file: string) => JSON.parse(await fs.readFile(file, "utf8"));
  const directory = path.dirname(fixture.settingsPath);
  const evidence: Record<string, unknown> = { runId: fixture.runId };
  const capture = async () => {
    const status = await unity.send(UNITY_COMMANDS.environment);
    return read(status.environment_state_path!);
  };
  const waitCommand = async (action: string, previousId: string) => {
    const deadline = Date.now() + 120000;
    while (Date.now() < deadline) {
      const result = (await read(fixture.settingsPath)).lastCommandResult;
      if (result?.action === action && result.commandId && result.commandId !== previousId && result.state !== "running") {
        assert.equal(result.state, "success", result.message);
        evidence[action] = result;
        return result;
      }
      await new Promise(resolve => setTimeout(resolve, 500));
    }
    throw new Error(`${action} 실제 처리 결과 대기 시간 초과`);
  };

  evidence.before = await capture();
  assert.equal((evidence.before as { play_mode: boolean }).play_mode, true);
  assert.equal((await read(fixture.libraryPath)).entries.length, 0);
  try {
    await app.open();
    await expect(browser.locator("#statusText")).toHaveText("연결됨");
    await screen.getByRole("button", "캐릭터").tap();
    await screen.getByRole("button", "캐릭터 라이브러리").tap();
    const previousId = (await read(fixture.settingsPath)).lastCommandResult?.commandId ?? "";
    await screen.getByRole("button", "VRM 파일 추가…").tap();
    const registered = await waitCommand("RegisterVrm", previousId);
    await expect(browser.locator("#libCount")).toContainText("1개 항목 등록됨");
    const library = await read(fixture.libraryPath);
    assert.equal(library.entries.length, 1);
    assert.equal(path.resolve(library.entries[0].sourcePath), path.resolve(fixture.vrmPath));
    await screen.getByRole("button", "불러오기 (Load)").tap();
    await waitCommand("LoadCharacter", registered.commandId);
    assert.equal((await read(fixture.libraryPath)).activeCharacterId, library.entries[0].id);
    evidence.loaded = await capture();
    assert.equal((evidence.loaded as { avatar_valid: boolean }).avatar_valid, true);
    assert.equal((evidence.loaded as { character_manager_active_id: string }).character_manager_active_id, library.entries[0].id);
    await screen.getByRole("button", "온보딩").tap();
    await screen.getByRole("button", "FBX 파일 가져오기").tap();
    const deadline = Date.now() + 300000;
    let ready = false;
    while (Date.now() < deadline) {
      const state = await capture();
      evidence.motion = state;
      if (state.session_state === "Failed") throw new Error("실제 FBX 준비 실패");
      if (state.has_prepared_motion && !state.is_processing) {
        assert.equal(state.motion_clip_name, fixture.expectedClipName);
        assert.ok(state.motion_clip_length_seconds > 0);
        assert.ok(state.motion_last_frame > 0);
        assert.equal(state.is_recording, false);
        ready = true;
        break;
      }
      await new Promise(resolve => setTimeout(resolve, 2000));
    }
    assert.equal(ready, true, "실제 FBX 준비 대기 시간 초과");
    const waitMotion = async (predicate: (state: any) => boolean) => {
      const end = Date.now() + 300000;
      while (Date.now() < end) {
        const state = await capture();
        evidence.lastPlaybackState = state;
        if (predicate(state)) return state;
        if (state.session_state === "Failed") throw new Error("실제 모션 재생 실패");
        await new Promise(resolve => setTimeout(resolve, 1000));
      }
      throw new Error("실제 모션 상태 대기 시간 초과");
    };
    let lastId = (await read(fixture.settingsPath)).lastCommandResult?.commandId ?? "";
    await screen.getByRole("button", "재생", { exact: true }).tap();
    lastId = (await waitCommand("PlayImportedMotion", lastId)).commandId;
    const initialPlayback = await waitMotion(state => state.session_state === "Failed" || (state.is_playing_motion && state.motion_current_frame > 5));
    evidence.correctedPlaybackPassed = initialPlayback.session_state !== "Failed";
    if (initialPlayback.session_state === "Failed") {
      const failedState = await read(`${fixture.settingsPath}.runtime-state.json`);
      assert.ok(failedState.motionError, "보정 실패 원인이 상태에 표시되어야 함");
      evidence.correctionFailure = failedState.motionError;
      await expect(browser.locator("#motionStatus")).toContainText("모션 준비 실패");
      await screen.getByRole("button", "표면 보정 끄기").tap();
      lastId = (await waitCommand("DisableNativeSkinningCorrection", lastId)).commandId;
      await expect(browser.locator("#motionStatus")).toContainText("품질 미검증");
      await screen.getByRole("button", "재생", { exact: true }).tap();
      lastId = (await waitCommand("PlayImportedMotion", lastId)).commandId;
    }
    evidence.playing = await waitMotion(state => state.is_playing_motion && state.motion_current_frame > 5);
    await screen.getByRole("button", "일시정지", { exact: true }).tap();
    lastId = (await waitCommand("PauseImportedMotion", lastId)).commandId;
    const paused = await waitMotion(state => !state.is_playing_motion);
    await new Promise(resolve => setTimeout(resolve, 1500));
    const held = await capture();
    assert.equal(held.motion_current_frame, paused.motion_current_frame);
    evidence.paused = held;
    await screen.getByRole("button", "재생", { exact: true }).tap();
    lastId = (await waitCommand("PlayImportedMotion", lastId)).commandId;
    evidence.resumed = await waitMotion(state => state.is_playing_motion && state.motion_current_frame > held.motion_current_frame);
    await screen.getByRole("button", "정지", { exact: true }).tap();
    await waitCommand("StopImportedMotion", lastId);
    evidence.stopped = await waitMotion(state => !state.is_playing_motion && state.motion_current_frame === 0);
    assert.equal((evidence.stopped as { is_recording: boolean }).is_recording, false);
    evidence.frame = await unity.send(UNITY_COMMANDS.frame);
    evidence.controlsPassed = true;
  } finally {
    await fs.writeFile(path.join(directory, "live-result.json"), JSON.stringify(evidence, null, 2));
  }
});
