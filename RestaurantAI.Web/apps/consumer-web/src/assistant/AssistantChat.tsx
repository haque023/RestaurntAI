import { Button, Card, ChatText, ErrorAlert } from "@restaurant-ai/ui";
import { useMutation } from "@tanstack/react-query";
import { useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";

interface ChatLine {
  role: "user" | "assistant";
  text: string;
}

interface ChatState {
  conversationId: string | null;
  lines: ChatLine[];
}

export interface AssistantReply {
  conversationId: string;
  reply: string;
}

export interface AssistantChatProps {
  title: string;
  intro: string;
  suggestions: string[];
  /** sessionStorage key; one conversation per key. */
  storageKey: string;
  placeholder?: string;
  openLabel: string;
  startOpen?: boolean;
  /** Extra controls under the title (e.g. "Use my location"). */
  toolbar?: ReactNode;
  send: (conversationId: string | null, message: string) => Promise<AssistantReply>;
  onReply?: () => void;
}

function load(key: string): ChatState {
  try {
    const raw = sessionStorage.getItem(key);
    return raw ? (JSON.parse(raw) as ChatState) : { conversationId: null, lines: [] };
  } catch {
    return { conversationId: null, lines: [] };
  }
}

/** A conversation with one of the platform's AI agents. Replies render as safe, in-app-links-only text. */
export function AssistantChat({ title, intro, suggestions, storageKey, placeholder = "Type a message…", openLabel, startOpen = false, toolbar, send, onReply }: AssistantChatProps) {
  const [chat, setChat] = useState<ChatState>(() => load(storageKey));
  const [draft, setDraft] = useState("");
  const [open, setOpen] = useState(startOpen || chat.lines.length > 0);
  const endRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    try {
      sessionStorage.setItem(storageKey, JSON.stringify(chat));
    } catch {
      // storage unavailable: the conversation lives in memory only
    }
    endRef.current?.scrollIntoView({ block: "nearest" });
  }, [chat, storageKey]);

  const turn = useMutation({
    mutationFn: (message: string) => send(chat.conversationId, message),
    onMutate: (message) => setChat((c) => ({ ...c, lines: [...c.lines, { role: "user", text: message }] })),
    onSuccess: (response) => {
      setChat((c) => ({ conversationId: response.conversationId, lines: [...c.lines, { role: "assistant", text: response.reply }] }));
      onReply?.();
    },
  });

  function ask(message: string) {
    const text = message.trim();
    if (!text || turn.isPending) return;
    turn.mutate(text);
  }

  function submit(event: FormEvent) {
    event.preventDefault();
    ask(draft);
    setDraft("");
  }

  if (!open) {
    return (
      <div>
        <Button variant="primary" onClick={() => setOpen(true)}>
          {openLabel}
        </Button>
      </div>
    );
  }

  return (
    <Card>
      <div className="row row-between">
        <h2>{title}</h2>
        <div className="row">
          {chat.lines.length > 0 && (
            <Button size="sm" variant="ghost" onClick={() => setChat({ conversationId: null, lines: [] })}>
              New conversation
            </Button>
          )}
          <Button size="sm" variant="ghost" onClick={() => setOpen(false)} aria-label={`Close ${title}`}>
            ✕
          </Button>
        </div>
      </div>
      {toolbar}

      <div className="chat-log" aria-live="polite">
        {chat.lines.length === 0 && <p className="muted small">{intro}</p>}
        {chat.lines.map((line, index) =>
          line.role === "user" ? (
            <div key={index} className="chat-bubble chat-bubble-user small" style={{ whiteSpace: "pre-wrap" }}>
              {line.text}
            </div>
          ) : (
            <div key={index} className="chat-bubble chat-bubble-assistant small">
              <ChatText text={line.text} />
            </div>
          ),
        )}
        {turn.isPending && <div className="small muted">Thinking…</div>}
        <div ref={endRef} />
      </div>

      {chat.lines.length === 0 && (
        <div className="row">
          {suggestions.map((s) => (
            <button key={s} type="button" className="chip" onClick={() => ask(s)} disabled={turn.isPending}>
              {s}
            </button>
          ))}
        </div>
      )}

      <ErrorAlert error={turn.error} title="No answer" />
      <form className="row" onSubmit={submit}>
        <input
          aria-label={`Message ${title}`}
          value={draft}
          maxLength={2000}
          onChange={(e) => setDraft(e.target.value)}
          placeholder={placeholder}
          style={{ flex: 1, minWidth: 0, font: "inherit", padding: "8px 12px", borderRadius: 10, border: "1px solid var(--color-border)", background: "var(--color-surface)", color: "var(--color-text)", minHeight: 40 }}
        />
        <Button type="submit" variant="primary" busy={turn.isPending} disabled={!draft.trim()}>
          Send
        </Button>
      </form>
    </Card>
  );
}
