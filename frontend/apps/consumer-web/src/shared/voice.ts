import { useCallback, useEffect, useRef, useState } from "react";

/**
 * Voice for the AI assistants, using the browser's own speech services (Web Speech API): no extra
 * keys or vendors. Claude only ever sees text: speech is turned into a message, and the reply text is
 * read aloud. Recognition is unavailable in some browsers (e.g. Firefox); the UI then hides the mic.
 */

export type VoiceLanguage = "en-US" | "ar-KW";

export const VOICE_LANGUAGES: { code: VoiceLanguage; label: string }[] = [
  { code: "en-US", label: "English" },
  { code: "ar-KW", label: "العربية" },
];

// Minimal typings: the Web Speech recognition API is not in TypeScript's DOM library.
interface RecognitionAlternative {
  transcript: string;
}
interface RecognitionResult {
  readonly isFinal: boolean;
  readonly length: number;
  [index: number]: RecognitionAlternative;
}
interface RecognitionEvent {
  readonly resultIndex: number;
  readonly results: { readonly length: number; [index: number]: RecognitionResult };
}
interface Recognition {
  lang: string;
  continuous: boolean;
  interimResults: boolean;
  maxAlternatives: number;
  onresult: ((event: RecognitionEvent) => void) | null;
  onerror: ((event: { error: string }) => void) | null;
  onend: (() => void) | null;
  start(): void;
  stop(): void;
  abort(): void;
}
type RecognitionConstructor = new () => Recognition;

function recognitionConstructor(): RecognitionConstructor | null {
  if (typeof window === "undefined") return null;
  const w = window as unknown as { SpeechRecognition?: RecognitionConstructor; webkitSpeechRecognition?: RecognitionConstructor };
  return w.SpeechRecognition ?? w.webkitSpeechRecognition ?? null;
}

export const canListen = () => recognitionConstructor() !== null;
export const canSpeak = () => typeof window !== "undefined" && "speechSynthesis" in window && typeof SpeechSynthesisUtterance !== "undefined";

const ARABIC = /\p{Script=Arabic}/gu;
const LETTER = /\p{L}/gu;

/** The language to read a reply in: Arabic when Arabic script dominates its letters, otherwise English. */
export function detectLanguage(text: string): VoiceLanguage {
  const letters = text.match(LETTER)?.length ?? 0;
  const arabic = text.match(ARABIC)?.length ?? 0;
  return letters > 0 && arabic / letters >= 0.3 ? "ar-KW" : "en-US";
}

/**
 * Turns a chat reply into text worth hearing: markdown links become their label, bare URLs, list
 * markers, emphasis, headings and code marks are dropped, and lines become sentence breaks.
 */
export function toSpeech(text: string): string {
  return text
    .replace(/!?\[([^\]]*)\]\([^)]*\)/g, "$1")
    .replace(/<?\bhttps?:\/\/\S+>?/gi, "")
    .replace(/`+/g, "")
    .replace(/(\*\*|__|\*|_)(.+?)\1/g, "$2")
    .split("\n")
    .map((line) => line.trim().replace(/^#{1,6}\s+/, "").replace(/^([-*•]|\d+[.)])(\s+|$)/, "").trim())
    .filter((line) => line.length > 0)
    .map((line) => (/[.!?؟:;،…]$/.test(line) ? line : `${line}.`))
    .join(" ")
    .replace(/\s{2,}/g, " ")
    .trim();
}

/** Picks an installed voice for the language: exact locale first, then any voice of the same language. */
export function pickVoice(voices: SpeechSynthesisVoice[], language: VoiceLanguage): SpeechSynthesisVoice | null {
  const lower = language.toLowerCase();
  const base = lower.split("-")[0];
  return (
    voices.find((v) => v.lang.toLowerCase().replace("_", "-") === lower) ??
    voices.find((v) => v.lang.toLowerCase().startsWith(`${base}-`) || v.lang.toLowerCase() === base) ??
    null
  );
}

/** Reads text aloud; `onEnd` runs when it finishes, is cancelled or fails. */
export function speak(text: string, language: VoiceLanguage, onEnd?: () => void): boolean {
  if (!canSpeak()) return false;
  const spoken = toSpeech(text);
  if (!spoken) return false;
  const synth = window.speechSynthesis;
  synth.cancel();
  const utterance = new SpeechSynthesisUtterance(spoken);
  utterance.lang = language;
  const voice = pickVoice(synth.getVoices(), language);
  if (voice) utterance.voice = voice;
  utterance.onend = () => onEnd?.();
  utterance.onerror = () => onEnd?.();
  synth.speak(utterance);
  return true;
}

export function stopSpeaking(): void {
  if (canSpeak()) window.speechSynthesis.cancel();
}

/** Whether the device has a voice for the language (voices load asynchronously in some browsers). */
export function useHasVoice(language: VoiceLanguage): boolean {
  const [has, setHas] = useState(() => canSpeak() && pickVoice(window.speechSynthesis.getVoices(), language) !== null);
  useEffect(() => {
    if (!canSpeak()) return;
    const update = () => setHas(pickVoice(window.speechSynthesis.getVoices(), language) !== null);
    update();
    window.speechSynthesis.addEventListener("voiceschanged", update);
    return () => window.speechSynthesis.removeEventListener("voiceschanged", update);
  }, [language]);
  return has;
}

const ERROR_MESSAGES: Record<string, string> = {
  "not-allowed": "Microphone access is blocked. Allow it in your browser's site settings, then try again.",
  "service-not-allowed": "Speech recognition is not allowed in this browser.",
  "no-speech": "I didn't hear anything. Tap the mic and try again.",
  "audio-capture": "No microphone was found.",
  network: "Speech recognition needs an internet connection.",
  "language-not-supported": "This browser can't recognize speech in that language.",
};

export type ListenState = "idle" | "listening";

/**
 * One utterance of speech recognition: `start()` listens until the speaker pauses, shows the words
 * as they are recognized (`interim`), then calls `onFinal` with the full transcript.
 */
export function useSpeechRecognition(language: VoiceLanguage, onFinal: (transcript: string) => void) {
  const [state, setState] = useState<ListenState>("idle");
  const [interim, setInterim] = useState("");
  const [error, setError] = useState<string | null>(null);
  const recognition = useRef<Recognition | null>(null);
  const finalCallback = useRef(onFinal);
  useEffect(() => {
    finalCallback.current = onFinal;
  }, [onFinal]);

  useEffect(() => () => recognition.current?.abort(), []);

  const start = useCallback(() => {
    const Constructor = recognitionConstructor();
    if (!Constructor) {
      setError("Voice input isn't supported in this browser. Try Chrome, Edge or Safari.");
      return;
    }
    recognition.current?.abort();
    stopSpeaking(); // never listen to our own voice
    const r = new Constructor();
    r.lang = language;
    r.continuous = false;
    r.interimResults = true;
    r.maxAlternatives = 1;
    let transcript = "";
    r.onresult = (event) => {
      let finalText = "";
      let interimText = "";
      for (let i = 0; i < event.results.length; i++) {
        const result = event.results[i];
        const text = result?.[0]?.transcript ?? "";
        if (result?.isFinal) finalText += text;
        else interimText += text;
      }
      transcript = finalText;
      setInterim(`${finalText}${interimText}`.trim());
    };
    r.onerror = (event) => {
      if (event.error !== "aborted") setError(ERROR_MESSAGES[event.error] ?? "Voice input stopped unexpectedly. Please try again.");
    };
    r.onend = () => {
      setState("idle");
      setInterim("");
      recognition.current = null;
      const text = transcript.trim();
      if (text) finalCallback.current(text);
    };
    setError(null);
    setInterim("");
    recognition.current = r;
    try {
      r.start();
      setState("listening");
    } catch {
      recognition.current = null;
      setError("Voice input couldn't start. Please try again.");
    }
  }, [language]);

  const stop = useCallback(() => recognition.current?.stop(), []);

  return { supported: canListen(), state, interim, error, start, stop, clearError: () => setError(null) };
}
