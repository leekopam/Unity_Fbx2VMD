import assert from "node:assert/strict";
import fs from "node:fs/promises";
import test from "node:test";

// 디자인 일관성 게이트 — shadcn/lint와 동일한 원칙을 순수 CSS 스택에 적용한다.
// 토큰 존(아래 화이트리스트 셀렉터 + 실제 --x 선언 포함) 밖의 raw 색상은
// DESIGN.md의 규칙 위반으로 본다. 기존 위반은 design-baseline.json 스냅샷으로
// 고정하고, 신규 위반·이동·재사용 추가만 차단한다(감소는 허용).
// baseline 재생성: DESIGN_BASELINE_UPDATE=1 node --test tests/designConsistency.test.mjs

const cssUrl = new URL("../build/styles.css", import.meta.url);
const htmlUrl = new URL("../build/index.html", import.meta.url);
const baselineUrl = new URL("./design-baseline.json", import.meta.url);
const jsDirs = [
  new URL("../client/", import.meta.url),
  new URL("../electron/", import.meta.url),
  new URL("../server/", import.meta.url),
  new URL("../scripts/", import.meta.url),
];

// 색상 값 선언이 허용되는 유일한 셀렉터(토큰 존). DESIGN.md의 표와 동기화한다.
const THEME_ZONE_SELECTORS = new Set([
  ":root",
  ".app-shell",
  '.settings-app[data-theme="dark"]',
  '.settings-app[data-theme="dark"] .app-shell',
]);

// CSS 함수 표기·이름 색까지 포함해 raw 색상을 검출한다.
// transparent/currentColor/inherit 등 CSS-wide 키워드는 색상 값이 아니므로 제외.
const rawColor =
  /#[0-9a-fA-F]{3,8}\b|rgba?\s*\(|hsla?\s*\(|oklch\s*\(|oklab\s*\(|lch\s*\(|lab\s*\(|color\s*\(|light-dark\s*\(|\b(?:red|blue|green|lime|aqua|fuchsia|magenta|yellow|navy|teal|olive|maroon|purple|silver|gray|grey|pink|orange|brown|cyan|black|white|rebeccapurple|crimson|coral|gold|indigo|violet|salmon|khaki|lavender|beige|ivory|tan|plum|orchid|hotpink|deeppink|skyblue|steelblue|tomato|turquoise|wheat|snow|seashell|linen|azure|bisque|chocolate|firebrick|gainsboro|honeydew|indianred|lavenderblush|lightblue|lightgray|lightgreen|lightslategray|lightsteelblue|limegreen|mediumblue|midnightblue|mintcream|mistyrose|moccasin|oldlace|orangered|papayawhip|peachpuff|peru|rosybrown|royalblue|saddlebrown|sandybrown|seagreen|sienna|slateblue|slategray|springgreen|thistle|whitesmoke|yellowgreen|aliceblue|antiquewhite|aquamarine|blueviolet|burlywood|cadetblue|chartreuse|cornflowerblue|cornsilk|darkblue|darkcyan|darkgoldenrod|darkgray|darkgreen|darkgrey|darkkhaki|darkmagenta|darkolivegreen|darkorange|darkorchid|darkred|darksalmon|darkseagreen|darkslateblue|darkslategray|darkslategrey|darkturquoise|darkviolet|deepskyblue|dimgray|dimgrey|dodgerblue|floralwhite|forestgreen|ghostwhite|goldenrod|greenyellow|lightcoral|lightcyan|lightgoldenrodyellow|lightpink|lightsalmon|lightseagreen|lightskyblue|lightyellow|mediumaquamarine|mediumorchid|mediumpurple|mediumseagreen|mediumslateblue|mediumspringgreen|mediumturquoise|mediumvioletred|navajowheat|palegoldenrod|palegreen|paleturquoise|palevioletred|powderblue)\b/i;

function stripComments(css) {
  // 줄바꿈은 유지해 라인 번호를 보존한다.
  return css.replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, " "));
}

// 블록 단위로 순회해 {셀렉터, 본문} 위반 선언을 수집한다.
// 본문은 ';'로 분할하되 마지막 선언의 세미콜론 생략도 인식한다.
function collectViolations(css) {
  const decls = [];
  for (const m of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    const sel = m[1].trim().replace(/\s+/g, " ");
    const body = m[2];
    const isZone = THEME_ZONE_SELECTORS.has(sel) && /--[a-zA-Z0-9-]+\s*:/.test(body);
    if (isZone) continue;
    for (const part of body.split(";")) {
      const cm = part.match(/^\s*([a-zA-Z-][a-zA-Z0-9-]*)\s*:\s*([\s\S]*?)\s*$/);
      if (!cm || !rawColor.test(cm[2])) continue;
      const line = css.slice(0, m.index).split("\n").length;
      decls.push({ line, key: `${sel} › ${cm[1]}: ${cm[2].replace(/\s+/g, " ")}` });
    }
  }
  return decls;
}

const FAIL_HINT =
  "raw 색상 금지 — 기존 토큰(var(--x))을 쓰거나 새 색이 필요하면 토큰 존(:root/.app-shell/[data-theme=\"dark\"])에 --토큰으로 선언하세요. 구조 리팩터 등 고의적 변경이면 DESIGN_BASELINE_UPDATE=1로 tests/design-baseline.json을 재생성하세요.";

test("var(--x) 참조는 어딘가에 정의된 토큰이어야 한다", async () => {
  const css = stripComments(await fs.readFile(cssUrl, "utf8"));
  const defined = new Set([...css.matchAll(/(--[a-zA-Z0-9-]+)\s*:/g)].map((m) => m[1]));
  const missing = [];
  for (const m of css.matchAll(/var\(\s*(--[a-zA-Z0-9-]+)\s*[,)]/g)) {
    const hasFallback = css[m.index + m[0].length - 1] === ",";
    if (!hasFallback && !defined.has(m[1])) {
      missing.push(`${m[1]} (styles.css:${css.slice(0, m.index).split("\n").length})`);
    }
  }
  assert.deepEqual(
    missing,
    [],
    `정의되지 않은 토큰 참조. 토큰 존에 선언을 추가하세요: ${missing.join(", ")}`
  );
});

test("raw 색상은 토큰 존 밖에 새로 추가·이동되지 않는다(기존 위반은 baseline 고정)", async () => {
  const css = stripComments(await fs.readFile(cssUrl, "utf8"));
  const decls = collectViolations(css);
  if (process.env.DESIGN_BASELINE_UPDATE === "1") {
    const next = {
      _note: "기존 raw 색상 위반 스냅샷(셀렉터 › prop: value, 발생건 단위). 신규·이동 금지, 감소만 허용.",
      generatedAt: new Date().toISOString().slice(0, 10),
      occurrences: decls.map((d) => d.key).sort(),
      maxOccurrences: decls.length,
    };
    await fs.writeFile(baselineUrl, JSON.stringify(next, null, 2) + "\n");
    return;
  }
  const baseline = JSON.parse(await fs.readFile(baselineUrl, "utf8"));
  const pool = new Map();
  for (const k of baseline.occurrences) pool.set(k, (pool.get(k) ?? 0) + 1);
  const fresh = [];
  for (const d of decls) {
    const left = pool.get(d.key) ?? 0;
    if (left > 0) pool.set(d.key, left - 1);
    else fresh.push(`styles.css:${d.line} ${d.key}`);
  }
  assert.deepEqual(fresh, [], `새/이동된 raw 색상 위반. ${FAIL_HINT}`);
  assert.ok(
    decls.length <= baseline.maxOccurrences,
    `raw 색상 선언이 baseline ${baseline.maxOccurrences}건에서 ${decls.length}건으로 증가. ${FAIL_HINT}`
  );
});

test("index.html은 인라인 스타일과 raw 색상을 사용하지 않는다", async () => {
  const html = stripComments(await fs.readFile(htmlUrl, "utf8"));
  // HTML 주석도 동일하게 제거. <!-- --> 는 /* */ 와 다른 패턴.
  const clean = html.replace(/<!--[\s\S]*?-->/g, (m) => m.replace(/[^\n]/g, " "));
  assert.doesNotMatch(clean, /\sstyle\s*=/i, "index.html의 style= 속성은 금지 — styles.css의 클래스/토큰으로 옮기세요.");
  // '#hex'가 속성 값 따옴표 직후가 아닌 위치(CSS 문맥)에 있을 때만 위반으로 본다.
  const bad = [...clean.matchAll(/[^\w"']#[0-9a-fA-F]{3,8}\b/g)];
  assert.deepEqual(
    bad.map((m) => `index.html:${clean.slice(0, m.index).split("\n").length} ${m[0].trim()}`),
    [],
    `index.html의 raw 색상은 금지 — 토큰 존에 --토큰을 선언하고 클래스로 참조하세요.`
  );
});

test("client/electron/server/scripts 스크립트는 raw 색상 리터럴을 사용하지 않는다", async () => {
  for (const dir of jsDirs) {
    let names = [];
    try {
      names = await fs.readdir(dir, { recursive: true });
    } catch {
      continue;
    }
    for (const name of names.filter((n) => /\.(mjs|cjs|js)$/.test(n))) {
      const src = stripComments(await fs.readFile(new URL(name, dir), "utf8"));
      assert.doesNotMatch(
        src,
        rawColor,
        `${name}에 raw 색상 리터럴 금지 — 색은 styles.css 토큰이 소유합니다. JS에서 값을 바꿔야 하면 setProperty("--토큰", ...) 패턴을 사용하세요.`
      );
    }
  }
});
