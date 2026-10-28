import { useApi } from "@restaurant-ai/auth";
import { Button, Card, ErrorAlert } from "@restaurant-ai/ui";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState, type FormEvent } from "react";

interface ChatLine {
  role: "guest" | "waiter";
  text: string;
}

interface ChatState {
  conversationId: string | null;
  lines: ChatLine[];
}

const storageKey = (branchId: string) => `rai.consumer.waiter.${branchId}`;

function load(branchId: string): ChatState {
  try {
    const raw = sessionStorage.getItem(storageKey(branchId));
    return raw ? (JSON.parse(raw) as ChatState) : { conversationId: null, lines: [] };
  } catch {
    return { conversationId: null, lines: [] };
  }
}

const SUGGESTIONS = ["What do you recommend?", "Something spicy under 500", "What's vegan here?", "Book a table for 4 tomorrow at 8pm"];

/** Conversational AI waiter for one branch. Prices and availability always come from live tools. */
export function WaiterChat({ branchId, tableId }: { branchId: string; tableId: string | null }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const [chat, setChat] = useState<ChatState>(() => load(branchId));
  const [draft, setDraft] = useState("");
  const [open, setOpen] = useState(chat.lines.length > 0);
  const endRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    try {
      sessionStorage.setItem(storageKey(branchId), JSON.stringify(chat));
    } catch {
      // storage unavailable: the conversation lives in memory only
    }
    endRef.current?.scrollIntoView({ block: "nearest" });
  }, [chat, branchId]);

  const send = useMutation({
    mutationFn: (message: string) => api.ai.waiterTurn({ conversationId: chat.conversationId, branchId, tableId, message }),
    onMutate: (message) => setChat((c) => ({ ...c, lines: [...c.lines, { role: "guest", text: message }] })),
    onSuccess: (response) => {
      setChat((c) => ({ conversationId: response.conversationId, lines: [...c.lines, { role: "waiter", text: response.reply }] }));
      // The waiter may have placed an order or made a reservation.
      void queryClient.invalidateQueries({ queryKey: ["menu", branchId] });
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    const message = draft.trim();
    if (!message || send.isPending) return;
    setDraft("");
    send.mutate(message);
  }

  if (!open) {
    return (
      <div>
        <Button variant="primary" onClick={() => setOpen(true)}>
          Ask the AI waiter
        </Button>
      </div>
    );
  }

  return (
    <Card>
      <div className="row row-between">
        <h2>AI waiter</h2>
        <div className="row">
          {chat.lines.length > 0 && (
            <Button size="sm" variant="ghost" onClick={() => setChat({ conversationId: null, lines: [] })}>
              New conversation
            </Button>
          )}
          <Button size="sm" variant="ghost" onClick={() => setOpen(false)} aria-label="Close AI waiter">
            ✕
          </Button>
        </div>
      </div>

      <div className="stack" aria-live="polite" style={{ maxHeight: 360, overflowY: "auto" }}>
        {chat.lines.length === 0 && (
          <p className="muted small">Ask about dishes, ingredients or allergies, get recommendations, order, or book a table.</p>
        )}
        {chat.lines.map((line, index) => (
          <div
            key={index}
            className="small"
            style={{
              alignSelf: line.role === "guest" ? "flex-end" : "flex-start",
              maxWidth: "85%",
              whiteSpace: "pre-wrap",
              padding: "8px 12px",
              borderRadius: 12,
              background: line.role === "guest" ? "var(--color-primary)" : "var(--color-surface-muted)",
              color: line.role === "guest" ? "var(--color-primary-contrast)" : "var(--color-text)",
            }}
          >
            {line.text}
          </div>
        ))}
        {send.isPending && <div className="small muted">The waiter is thinking…</div>}
        <div ref={endRef} />
      </div>

      {chat.lines.length === 0 && (
        <div className="row">
          {SUGGESTIONS.map((s) => (
            <button key={s} type="button" className="chip" onClick={() => send.mutate(s)} disabled={send.isPending}>
              {s}
            </button>
          ))}
        </div>
      )}

      <ErrorAlert error={send.error} title="The waiter could not answer" />
      <form className="row" onSubmit={submit}>
        <input
          aria-label="Message the AI waiter"
          value={draft}
          maxLength={2000}
          onChange={(e) => setDraft(e.target.value)}
          placeholder="Type a message…"
          style={{ flex: 1, minWidth: 0, font: "inherit", padding: "8px 12px", borderRadius: 10, border: "1px solid var(--color-border)", background: "var(--color-surface)", color: "var(--color-text)", minHeight: 40 }}
        />
        <Button type="submit" variant="primary" busy={send.isPending} disabled={!draft.trim()}>
          Send
        </Button>
      </form>
    </Card>
  );
}
