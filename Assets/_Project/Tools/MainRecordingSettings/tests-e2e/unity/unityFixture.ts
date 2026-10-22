import type { UnityStatus } from "./unityClient.js";

// 엔진 기여 fixture `unity`의 테스트 측 타입 선언이다.
// 구현은 unityEngine.ts의 fixtures.unity — 필드를 추가하면 양쪽을 함께 갱신한다.
export interface UnityFixture {
  /** 임의의 Unity 자동화 명령을 실행하고 최종 상태를 돌려준다. */
  run(command: string, fields?: Record<string, unknown>, timeoutMs?: number): Promise<UnityStatus>;
  /** capture_e2e_environment 결과 JSON을 파싱해 돌려준다. */
  environment(): Promise<Record<string, unknown>>;
  enterPlay(): Promise<UnityStatus>;
  exitPlay(): Promise<UnityStatus>;
  /** Play Mode에서 Game View MP4 녹화를 시작한다. */
  startVideo(): Promise<void>;
  /** 녹화를 끝내고 attempt 산출물 상대 경로를 돌려준다. */
  stopVideo(): Promise<readonly string[]>;
  /** satisfaction_2 풀클립 계측(수 분 소요). all-frames CSV가 artifact로 첨부된다. */
  fullClipMetrics(fields?: Record<string, unknown>): Promise<UnityStatus>;
}
