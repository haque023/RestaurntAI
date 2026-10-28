import type { ReactNode } from "react";
import { Link } from "react-router";
import { isInternalPath } from "./links";

/**
 * Renders the small markdown subset assistants use: paragraphs, "- " bullet lists, **bold** and
 * [label](/path) links. Only in-app paths become links; anything else stays plain text, so a
 * model reply can never inject HTML or send people to an external site.
 */
export function ChatText({ text }: { text: string }) {
  const blocks: ReactNode[] = [];
  let bullets: string[] = [];
  let paragraph: string[] = [];

  const flushParagraph = () => {
    if (paragraph.length > 0) {
      blocks.push(<p key={blocks.length}>{inline(paragraph.join(" "))}</p>);
      paragraph = [];
    }
  };
  const flushBullets = () => {
    if (bullets.length > 0) {
      blocks.push(
        <ul key={blocks.length}>
          {bullets.map((b, i) => (
            <li key={i}>{inline(b)}</li>
          ))}
        </ul>,
      );
      bullets = [];
    }
  };

  for (const raw of text.split("\n")) {
    const line = raw.trim();
    const bullet = /^([-*•]|\d+\.)\s+(.*)$/.exec(line);
    if (bullet) {
      flushParagraph();
      bullets.push(bullet[2] ?? "");
    } else if (line === "") {
      flushParagraph();
      flushBullets();
    } else {
      flushBullets();
      paragraph.push(line.replace(/^#+\s*/, ""));
    }
  }
  flushParagraph();
  flushBullets();

  return <div className="chat-text">{blocks}</div>;
}

const TOKEN = /\*\*(.+?)\*\*|\[([^\]]+)\]\(([^)\s]+)\)/g;

function inline(text: string): ReactNode[] {
  const parts: ReactNode[] = [];
  let last = 0;
  for (const match of text.matchAll(TOKEN)) {
    if (match.index > last) parts.push(text.slice(last, match.index));
    if (match[1] !== undefined) {
      parts.push(<strong key={parts.length}>{match[1]}</strong>);
    } else if (match[3] !== undefined && isInternalPath(match[3])) {
      parts.push(
        <Link key={parts.length} to={match[3]}>
          {match[2]}
        </Link>,
      );
    } else {
      parts.push(match[2]);
    }
    last = match.index + match[0].length;
  }
  if (last < text.length) parts.push(text.slice(last));
  return parts;
}
