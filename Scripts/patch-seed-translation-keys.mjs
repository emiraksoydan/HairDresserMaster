import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const fp = path.join(__dirname, "SeedHelpGuides_2026.sql");
let s = fs.readFileSync(fp, "utf8");

s = s.replaceAll(
  'INSERT INTO "HelpGuides" ("Id","UserType","Title","Description","Order","IsActive","CreatedAt","UpdatedAt") VALUES',
  'INSERT INTO "HelpGuides" ("Id","UserType","Title","Description","TranslationKey","Order","IsActive","CreatedAt","UpdatedAt") VALUES',
);

const rowRe =
  /^(.*?), (\d+), true, (NOW\(\), NOW\(\))\)([,;])\s*$/;

const lines = s.split("\n");
const out = [];
let prefix = "hg_c";

for (let line of lines) {
  line = line.replace(/\r$/, "");
  if (line.includes("MÜŞTERİ (0)")) prefix = "hg_c";
  else if (line.includes("SERBEST BERBER (1)")) prefix = "hg_fb";
  else if (line.includes("BERBER DÜKKANI (2)")) prefix = "hg_st";

  const m = line.match(rowRe);
  if (m && line.startsWith("(gen_random_uuid()")) {
    const idx = String(m[2]).padStart(2, "0");
    const key = `${prefix}_${idx}`;
    out.push(`${m[1]}, '${key}', ${m[2]}, true, ${m[3]})${m[4]}`);
  } else {
    out.push(line);
  }
}

fs.writeFileSync(fp, out.join("\n"), "utf8");
console.log("Patched", fp);
