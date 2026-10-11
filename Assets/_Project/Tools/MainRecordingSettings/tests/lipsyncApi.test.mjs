import assert from "node:assert/strict";
import test from "node:test";

import {
  createHealthRequest,
  createJobSubmitRequest,
  createJobStatusRequest,
  createJobLogRequest,
  createJobCancelRequest,
  createResultRequest,
  submitJob,
  getJob,
  cancelJob
} from "../client/lipsyncApi.js";

test("createHealthRequest builds GET /health", () => {
  const request = createHealthRequest({ apiBaseUrl: "http://127.0.0.1:17845/" });
  assert.equal(request.url, "http://127.0.0.1:17845/health");
  assert.equal(request.init.method, "GET");
});

test("createJobSubmitRequest builds POST /jobs with kind/key/params", () => {
  const request = createJobSubmitRequest({
    apiBaseUrl: "http://127.0.0.1:17845",
    kind: "separate",
    key: "C:/in.wav",
    params: { engine: "audio_separator", input: "C:/in.wav", output_dir: "C:/out" }
  });

  assert.equal(request.url, "http://127.0.0.1:17845/jobs");
  assert.equal(request.init.method, "POST");
  const body = JSON.parse(request.init.body);
  assert.equal(body.kind, "separate");
  assert.equal(body.key, "C:/in.wav");
  assert.equal(body.params.engine, "audio_separator");
});

test("createJobSubmitRequest rejects empty kind", () => {
  assert.throws(
    () => createJobSubmitRequest({ apiBaseUrl: "http://x", kind: " " }),
    /작업 종류 필요/
  );
});

test("createJobStatusRequest builds GET /jobs/{id}", () => {
  const request = createJobStatusRequest({ apiBaseUrl: "http://x", jobId: "abc123" });
  assert.equal(request.url, "http://x/jobs/abc123");
});

test("createJobLogRequest builds GET /jobs/{id}/log?tail=", () => {
  const request = createJobLogRequest({ apiBaseUrl: "http://x", jobId: "a", tail: 50 });
  assert.equal(request.url, "http://x/jobs/a/log?tail=50");
});

test("createJobCancelRequest builds POST /jobs/{id}/cancel", () => {
  const request = createJobCancelRequest({ apiBaseUrl: "http://x", jobId: "a" });
  assert.equal(request.url, "http://x/jobs/a/cancel");
  assert.equal(request.init.method, "POST");
});

test("createResultRequest builds GET /result/{kind} with optional key", () => {
  const request = createResultRequest({ apiBaseUrl: "http://x/", kind: "separate", key: "C:/a b.wav" });
  assert.equal(request.url, "http://x/result/separate?key=C%3A%2Fa%20b.wav");
  const bare = createResultRequest({ apiBaseUrl: "http://x", kind: "clean" });
  assert.equal(bare.url, "http://x/result/clean");
});

test("submitJob returns job_id on success", async () => {
  const result = await submitJob({
    apiBaseUrl: "http://x",
    kind: "separate",
    params: { engine: "demucs", input: "i", output_dir: "o" },
    fetchImpl: async () => ({
      ok: true,
      json: async () => ({ job_id: "j1" })
    })
  });
  assert.equal(result.job_id, "j1");
});

test("submitJob reattaches to existing job on 409", async () => {
  const result = await submitJob({
    apiBaseUrl: "http://x",
    kind: "separate",
    key: "dup",
    params: {},
    fetchImpl: async () => ({
      ok: false,
      status: 409,
      text: async () => JSON.stringify({ detail: { job_id: "existing9", message: "동일 작업이 이미 실행 중입니다." } })
    })
  });
  assert.equal(result.job_id, "existing9");
  assert.equal(result.reused, true);
});

test("submitJob propagates non-409 errors with status", async () => {
  await assert.rejects(
    () => submitJob({
      apiBaseUrl: "http://x",
      kind: "separate",
      params: {},
      fetchImpl: async () => ({
        ok: false,
        status: 400,
        text: async () => JSON.stringify({ detail: "필수 파라미터 누락: input" })
      })
    }),
    (error) => error.status === 400 && /HTTP 오류 400/.test(error.message)
  );
});

test("getJob throws on 404 job", async () => {
  await assert.rejects(
    () => getJob({
      apiBaseUrl: "http://x",
      jobId: "gone",
      fetchImpl: async () => ({ ok: false, status: 404, text: async () => "job not found" })
    }),
    /HTTP 오류 404/
  );
});

test("cancelJob returns ok", async () => {
  const calls = [];
  const result = await cancelJob({
    apiBaseUrl: "http://x",
    jobId: "j2",
    fetchImpl: async (url, init) => {
      calls.push({ url, init });
      return { ok: true, json: async () => ({ ok: true }) };
    }
  });
  assert.equal(result.ok, true);
  assert.equal(calls[0].url, "http://x/jobs/j2/cancel");
});
