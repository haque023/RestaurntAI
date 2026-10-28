/** "/r/..." style app paths only; rejects protocol-relative ("//evil") and absolute URLs. */
export function isInternalPath(href: string): boolean {
  return href.startsWith("/") && !href.startsWith("//") && !href.includes(":");
}
