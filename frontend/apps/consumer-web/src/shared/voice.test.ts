import { describe, expect, it } from "vitest";
import { detectLanguage, pickVoice, toSpeech } from "./voice";

const voice = (lang: string, name = lang) => ({ lang, name }) as SpeechSynthesisVoice;

describe("toSpeech", () => {
  it("reads markdown links as their label and drops URLs", () => {
    expect(toSpeech("Try [Spice Route](/r/abc/def) or see https://example.com/menu")).toBe("Try Spice Route or see.");
  });

  it("turns list items and lines into sentences without the markers", () => {
    expect(toSpeech("Options:\n- **Kacchi Biryani** KWD 2.500\n- Vegetable Khichuri\n1. Mishti Doi")).toBe(
      "Options: Kacchi Biryani KWD 2.500. Vegetable Khichuri. Mishti Doi.",
    );
  });

  it("keeps Arabic punctuation and text intact", () => {
    expect(toSpeech("أنصحك بـ [برياني لحم](/r/1/2)؟\n- طبق نباتي")).toBe("أنصحك بـ برياني لحم؟ طبق نباتي.");
  });

  it("returns an empty string when nothing is worth reading", () => {
    expect(toSpeech("https://example.com\n\n- ")).toBe("");
  });
});

describe("detectLanguage", () => {
  it("detects Arabic replies", () => {
    expect(detectLanguage("مرحبا! عندنا برياني اليوم بسعر ٢٫٥٠٠ دينار")).toBe("ar-KW");
  });

  it("treats English, and English with a few Arabic words, as English", () => {
    expect(detectLanguage("We have Kacchi Biryani today for KWD 2.500")).toBe("en-US");
    expect(detectLanguage("Try our machboos (مجبوس), it is the house special today")).toBe("en-US");
  });

  it("defaults to English when there are no letters", () => {
    expect(detectLanguage("2.500 / 3")).toBe("en-US");
  });
});

describe("pickVoice", () => {
  it("prefers the exact locale, then any voice of the language", () => {
    const voices = [voice("en-GB"), voice("ar-SA"), voice("ar-KW"), voice("en-US")];
    expect(pickVoice(voices, "ar-KW")?.lang).toBe("ar-KW");
    expect(pickVoice([voice("en-GB"), voice("ar-SA")], "ar-KW")?.lang).toBe("ar-SA");
    expect(pickVoice([voice("en_US")], "en-US")?.lang).toBe("en_US");
  });

  it("returns null when the device has no voice for the language", () => {
    expect(pickVoice([voice("en-US"), voice("fr-FR")], "ar-KW")).toBeNull();
  });
});
