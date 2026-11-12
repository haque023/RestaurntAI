import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { describe, expect, it } from "vitest";
import { ChatText } from "./ChatText";
import { isInternalPath } from "./links";

describe("ChatText", () => {
  it("renders bullets, bold and in-app links", () => {
    render(
      <MemoryRouter>
        <ChatText text={"Try these:\n- **Fuchka** at [Dhaka Bites](/r/t1/b1)\n- Lassi"} />
      </MemoryRouter>,
    );

    expect(screen.getAllByRole("listitem")).toHaveLength(2);
    expect(screen.getByText("Fuchka").tagName).toBe("STRONG");
    expect(screen.getByRole("link", { name: "Dhaka Bites" })).toHaveAttribute("href", "/r/t1/b1");
  });

  it("never links outside the app", () => {
    render(
      <MemoryRouter>
        <ChatText text={"[phish](https://evil.example) and [sneaky](//evil.example) and <b>raw</b>"} />
      </MemoryRouter>,
    );

    expect(screen.queryByRole("link")).toBeNull();
    expect(screen.getByText(/phish and sneaky and <b>raw<\/b>/)).toBeInTheDocument();
  });

  it("classifies paths", () => {
    expect(isInternalPath("/r/a/b")).toBe(true);
    expect(isInternalPath("//x.com")).toBe(false);
    expect(isInternalPath("javascript:alert(1)")).toBe(false);
  });
});
