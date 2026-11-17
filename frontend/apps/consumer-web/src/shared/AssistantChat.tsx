import { Button, Card, ChatText, ErrorAlert } from "@restaurant-ai/ui";
import { useMutation } from "@tanstack/react-query";
import { useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { canSpeak, detectLanguage, speak, stopSpeaking, useHasVoice, useSpeechRecognition, VOICE_LANGUAGES, type VoiceLanguage } from "./voice";

const LANGUAGE_KEY = "rai.consumer.voice.language";
const READ_ALOUD_KEY = "rai.consumer.voice.readAloud";

function initialLanguage(): VoiceLanguage {
  try {
    const saved = localStorage.getItem(LANGUAGE_KEY);
    if (saved === "en-US" || saved === "ar-KW") return saved;
  } catch {
    // storage unavailable
  }
  return typeof navigator !== "undefined" && navigator.language.toLowerCase().startsWith("ar") ? "ar-KW" : "en-US";
}

function remember(key: string, value: string) {
  try {
    localStorage.setItem(key, value);
  } catch {
    // storage unavailable: the choice lasts for this page only
  }
}

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

  // Voice: speech in (browser recognition) and replies read aloud (browser speech synthesis).
  const [language, setLanguage] = useState<VoiceLanguage>(initialLanguage);
  const [readAloud, setReadAloud] = useState(() => {
    try {
      return localStorage.getItem(READ_ALOUD_KEY) === "true";
    } catch {
      return false;
    }
  });
  const [speaking, setSpeaking] = useState<number | null>(null);
  const spokenTurn = useRef(false);
  const voice = useSpeechRecognition(language, (transcript) => ask(transcript, true));
  const hasVoice = useHasVoice(language);
  const speechOutput = canSpeak();

  const lineCount = useRef(chat.lines.length);
  useEffect(() => {
    lineCount.current = chat.lines.length;
  }, [chat.lines.length]);

  useEffect(() => () => stopSpeaking(), []);

  function readLine(index: number, text: string) {
    if (speaking === index) {
      stopSpeaking();
      setSpeaking(null);
      return;
    }
    if (speak(text, detectLanguage(text), () => setSpeaking((current) => (current === index ? null : current)))) setSpeaking(index);
  }

  function chooseLanguage(code: VoiceLanguage) {
    setLanguage(code);
    remember(LANGUAGE_KEY, code);
  }

  function toggleReadAloud() {
    const next = !readAloud;
    setReadAloud(next);
    remember(READ_ALOUD_KEY, String(next));
    if (!next) {
      stopSpeaking();
      setSpeaking(null);
    }
  }

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
      // The reply is appended after every line already shown (including the question).
      const index = lineCount.current;
      setChat((c) => ({ conversationId: response.conversationId, lines: [...c.lines, { role: "assistant", text: response.reply }] }));
      // A question asked by voice is answered by voice; typed questions only when "Read replies aloud" is on.
      if (spokenTurn.current || readAloud) {
        if (speak(response.reply, detectLanguage(response.reply), () => setSpeaking((current) => (current === index ? null : current)))) setSpeaking(index);
      }
      spokenTurn.current = false;
      onReply?.();
    },
    onError: () => {
      spokenTurn.current = false;
    },
  });

  function ask(message: string, spoken = false) {
    const text = message.trim();
    if (!text || turn.isPending) return;
    spokenTurn.current = spoken;
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
            <Button
              size="sm"
              variant="ghost"
              onClick={() => {
                stopSpeaking();
                setSpeaking(null);
                setChat({ conversationId: null, lines: [] });
              }}
            >
              New conversation
            </Button>
          )}
          <Button
            size="sm"
            variant="ghost"
            onClick={() => {
              stopSpeaking();
              voice.stop();
              setOpen(false);
            }}
            aria-label={`Close ${title}`}
          >
            ✕
          </Button>
        </div>
      </div>
      {toolbar}

      {(voice.supported || speechOutput) && (
        <div className="row small" role="group" aria-label="Voice settings">
          <span className="muted">Voice:</span>
          {VOICE_LANGUAGES.map((l) => (
            <button
              key={l.code}
              type="button"
              className="chip"
              lang={l.code}
              aria-pressed={language === l.code}
              onClick={() => chooseLanguage(l.code)}
            >
              {l.label}
            </button>
          ))}
          {speechOutput && (
            <button type="button" className="chip" aria-pressed={readAloud} onClick={toggleReadAloud}>
              {readAloud ? "🔊 Reading replies aloud" : "🔈 Read replies aloud"}
            </button>
          )}
          {speechOutput && !hasVoice && <span className="muted">This device has no {language === "ar-KW" ? "Arabic" : "English"} voice installed; replies may be read with a default voice.</span>}
        </div>
      )}

      <div className="chat-log" aria-live="polite">
        {chat.lines.length === 0 && <p className="muted small">{intro}</p>}
        {chat.lines.map((line, index) =>
          line.role === "user" ? (
            <div key={index} dir="auto" className="chat-bubble chat-bubble-user small" style={{ whiteSpace: "pre-wrap" }}>
              {line.text}
            </div>
          ) : (
            <div key={index} dir="auto" className="chat-bubble chat-bubble-assistant small">
              <ChatText text={line.text} />
              {speechOutput && (
                <button
                  type="button"
                  className="chip"
                  style={{ marginTop: 6 }}
                  aria-label={speaking === index ? "Stop reading this reply" : "Read this reply aloud"}
                  onClick={() => readLine(index, line.text)}
                >
                  {speaking === index ? "■ Stop" : "🔊 Listen"}
                </button>
              )}
            </div>
          ),
        )}
        {turn.isPending && <div className="small muted">Thinking…</div>}
        {voice.state === "listening" && (
          <div className="small muted" dir="auto" role="status">
            🎙️ Listening{voice.interim ? `: ${voice.interim}` : "…"}
          </div>
        )}
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
      {voice.error && (
        <div className="small muted" role="alert">
          {voice.error}
        </div>
      )}
      <form className="row" onSubmit={submit}>
        {voice.supported && (
          <Button
            type="button"
            variant={voice.state === "listening" ? "primary" : "default"}
            disabled={turn.isPending}
            aria-label={voice.state === "listening" ? "Stop listening" : `Speak to ${title} in ${language === "ar-KW" ? "Arabic" : "English"}`}
            aria-pressed={voice.state === "listening"}
            onClick={() => (voice.state === "listening" ? voice.stop() : voice.start())}
          >
            {voice.state === "listening" ? "■" : "🎤"}
          </Button>
        )}
        <input
          aria-label={`Message ${title}`}
          dir="auto"
          lang={language}
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
