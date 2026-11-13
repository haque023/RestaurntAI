import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import type { PlaywrightTestConfig } from "@playwright/test";
import { inflateRawSync } from "node:zlib";

/**
 * What a test run may keep on disk. Traces are off: a trace records every action's parameters,
 * including values typed into password fields and the arguments of page evaluations, and Playwright
 * offers no redaction for them. Screenshots are kept on failure (password fields render masked), and
 * credentials are typed with `fillSecret`, whose value does not appear in step titles or reports.
 * `support-tests/redaction.spec.ts` proves this policy with a canary value.
 */
export const artifactPolicy: PlaywrightTestConfig["use"] = {
  trace: "off",
  video: "off",
  screenshot: "only-on-failure",
};

/** Every file under `dir`, recursively. */
export function filesUnder(dir: string): string[] {
  if (!existsSync(dir)) return [];
  return readdirSync(dir).flatMap((name) => {
    const full = path.join(dir, name);
    return statSync(full).isDirectory() ? filesUnder(full) : [full];
  });
}

/** Entries of a zip archive (central directory; stored or deflated). */
export function unzip(buffer: Buffer): { name: string; data: Buffer }[] {
  const eocd = buffer.lastIndexOf(Buffer.from([0x50, 0x4b, 0x05, 0x06]));
  if (eocd < 0) return [];
  const count = buffer.readUInt16LE(eocd + 10);
  let p = buffer.readUInt32LE(eocd + 16);
  const entries: { name: string; data: Buffer }[] = [];
  for (let i = 0; i < count && buffer.readUInt32LE(p) === 0x02014b50; i++) {
    const method = buffer.readUInt16LE(p + 10);
    const size = buffer.readUInt32LE(p + 20);
    const nameLength = buffer.readUInt16LE(p + 28);
    const extra = buffer.readUInt16LE(p + 30);
    const comment = buffer.readUInt16LE(p + 32);
    const local = buffer.readUInt32LE(p + 42);
    const name = buffer.toString("utf8", p + 46, p + 46 + nameLength);
    const start = local + 30 + buffer.readUInt16LE(local + 26) + buffer.readUInt16LE(local + 28);
    const raw = Buffer.from(buffer.subarray(start, start + size));
    entries.push({ name, data: method === 8 ? Buffer.from(inflateRawSync(raw)) : raw });
    p += 46 + nameLength + extra + comment;
  }
  return entries;
}

/**
 * The searchable contents of an artifact: its bytes, plus the decoded entries of zip archives
 * (traces) and of the base64 zip that Playwright's HTML report embeds in index.html.
 */
export function artifactContents(file: string): Buffer[] {
  const bytes = Buffer.from(readFileSync(file));
  const parts: Buffer[] = [bytes];
  if (file.endsWith(".zip")) parts.push(...unzip(bytes).map((e) => e.data));
  if (file.endsWith(".html")) {
    for (const m of bytes.toString("utf8").matchAll(/data:application\/zip;base64,([A-Za-z0-9+/=]+)/g)) {
      parts.push(...unzip(Buffer.from(m[1], "base64")).map((e) => e.data));
    }
  }
  return parts;
}

/** Files whose contents (including decoded archives) contain `needle`. */
export function filesContaining(files: string[], needle: string): string[] {
  const bytes = Buffer.from(needle, "utf8");
  return files.filter((f) => artifactContents(f).some((part) => part.includes(bytes)));
}
