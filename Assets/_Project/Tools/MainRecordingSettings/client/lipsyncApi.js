export const DEFAULT_LIPSYNC_BASE_URL = "http://127.0.0.1:17845";

// 립싱크 로컬 서버(Tools/LipSync/server.py)의 HTTP 클라이언트.
// settingsApi.js와 동일하게 요청 생성과 fetch 호출을 분리해
// UI와 단위 테스트가 같은 로직을 공유한다.

export function createHealthRequest({ apiBaseUrl = DEFAULT_LIPSYNC_BASE_URL } = {}) {
  return { url: `${normalizeBaseUrl(apiBaseUrl)}/health`, init: { method: "GET" } };
}

export function createJobSubmitRequest({
  apiBaseUrl = DEFAULT_LIPSYNC_BASE_URL,
  kind,
  key = "",
  params = {}
}) {
  const baseUrl = normalizeBaseUrl(apiBaseUrl);
  const normalizedKind = normalizeRequiredText(kind, "작업 종류 필요");

  return {
    url: `${baseUrl}/jobs`,
    init: {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ kind: normalizedKind, key, params })
    }
  };
}

export function createJobStatusRequest({
  apiBaseUrl = DEFAULT_LIPSYNC_BASE_URL,
  jobId
}) {
  const baseUrl = normalizeBaseUrl(apiBaseUrl);
  const id = normalizeRequiredText(jobId, "작업 ID 필요");
  return { url: `${baseUrl}/jobs/${encodeURIComponent(id)}`, init: { method: "GET" } };
}

export function createJobLogRequest({
  apiBaseUrl = DEFAULT_LIPSYNC_BASE_URL,
  jobId,
  tail = 200
}) {
  const baseUrl = normalizeBaseUrl(apiBaseUrl);
  const id = normalizeRequiredText(jobId, "작업 ID 필요");
  return {
    url: `${baseUrl}/jobs/${encodeURIComponent(id)}/log?tail=${tail}`,
    init: { method: "GET" }
  };
}

export function createJobCancelRequest({
  apiBaseUrl = DEFAULT_LIPSYNC_BASE_URL,
  jobId
}) {
  const baseUrl = normalizeBaseUrl(apiBaseUrl);
  const id = normalizeRequiredText(jobId, "작업 ID 필요");
  return {
    url: `${baseUrl}/jobs/${encodeURIComponent(id)}/cancel`,
    init: { method: "POST" }
  };
}

export function createResultRequest({
  apiBaseUrl = DEFAULT_LIPSYNC_BASE_URL,
  kind,
  key = ""
}) {
  const baseUrl = normalizeBaseUrl(apiBaseUrl);
  const normalizedKind = normalizeRequiredText(kind, "작업 종류 필요");
  const query = key ? `?key=${encodeURIComponent(key)}` : "";
  return {
    url: `${baseUrl}/result/${encodeURIComponent(normalizedKind)}${query}`,
    init: { method: "GET" }
  };
}

export async function getHealth({ apiBaseUrl, fetchImpl = globalThis.fetch } = {}) {
  const request = createHealthRequest({ apiBaseUrl });
  return fetchJson(request, fetchImpl);
}

export async function submitJob({ apiBaseUrl, kind, key, params, fetchImpl = globalThis.fetch }) {
  const request = createJobSubmitRequest({ apiBaseUrl, kind, key, params });
  try {
    return await fetchJson(request, fetchImpl);
  } catch (error) {
    // 409 동일 kind+key 작업이 이미 실행 중이면 서버가 detail.job_id로
    // 기존 작업을 돌려준다. 재제출 대신 그 작업에 붙는다.
    if (error?.status === 409 && error.body) {
      try {
        const detail = JSON.parse(error.body)?.detail;
        if (detail?.job_id) {
          return { job_id: detail.job_id, reused: true };
        }
      } catch {
        // 본문 파싱 실패 시 원래 오류를 그대로 전파한다.
      }
    }
    throw error;
  }
}

export async function getJob({ apiBaseUrl, jobId, fetchImpl = globalThis.fetch }) {
  const request = createJobStatusRequest({ apiBaseUrl, jobId });
  return fetchJson(request, fetchImpl);
}

export async function getJobLog({ apiBaseUrl, jobId, tail, fetchImpl = globalThis.fetch }) {
  const request = createJobLogRequest({ apiBaseUrl, jobId, tail });
  return fetchJson(request, fetchImpl);
}

export async function cancelJob({ apiBaseUrl, jobId, fetchImpl = globalThis.fetch }) {
  const request = createJobCancelRequest({ apiBaseUrl, jobId });
  return fetchJson(request, fetchImpl);
}

// 서버 재시작 후에도 마지막 완료 작업을 key 기준으로 다시 조회한다.
export async function getResult({ apiBaseUrl, kind, key, fetchImpl = globalThis.fetch }) {
  const request = createResultRequest({ apiBaseUrl, kind, key });
  return fetchJson(request, fetchImpl);
}

async function fetchJson(request, fetchImpl) {
  if (typeof fetchImpl !== "function") {
    throw new Error("fetch 구현 필요");
  }

  const response = await fetchImpl(request.url, request.init);
  if (!response.ok) {
    const body = typeof response.text === "function" ? await response.text() : "";
    const detail = body ? `: ${body}` : "";
    const error = new Error(`HTTP 오류 ${response.status}${detail}`);
    error.status = response.status;
    error.body = body;
    throw error;
  }

  if (typeof response.json !== "function") {
    return {};
  }

  return await response.json();
}

function normalizeBaseUrl(value) {
  return normalizeRequiredText(value, "API 기본 주소 필요").replace(/\/+$/, "");
}

function normalizeRequiredText(value, message) {
  const normalized = typeof value === "string" ? value.trim() : "";
  if (!normalized) {
    throw new Error(message);
  }

  return normalized;
}
