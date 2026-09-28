const measurements = [
  ["maximum_time_error_ms", "ms"],
  ["maximum_penetration_mm", "mm"],
  ["maximum_sole_step_mm", "mm"],
  ["maximum_foot_rotation_step_degrees", "deg"],
  ["maximum_hips_step_mm", "mm"]
];

export function buildFullClipMetrics(state) {
  return measurements.map(([field, unit]) => {
    const value = state?.[field];
    if (!Number.isFinite(value) || value < 0)
      throw new Error(`전체 클립 최대 계측값이 유효하지 않습니다: ${field}`);
    // 차이 0 기준은 수치 증가 감지용이며 시각 품질 합격 기준이 아님.
    return { name: `full_clip/${field}`, value, unit, definitionVersion: "1",
      aggregation: "max", direction: "lower", threshold: { kind: "absolute", value: 0 } };
  });
}
