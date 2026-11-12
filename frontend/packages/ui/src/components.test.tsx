import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Button, ErrorAlert } from "./components";
import { formatMoney } from "./format";

describe("ui components", () => {
  it("disables a busy button", () => {
    render(<Button busy>Save</Button>);
    expect(screen.getByRole("button", { name: "Save" })).toBeDisabled();
  });

  it("shows the correlation id of an API error", () => {
    render(<ErrorAlert error={{ message: "Item unavailable", correlationId: "abc123" }} />);
    expect(screen.getByRole("alert")).toHaveTextContent("Item unavailable");
    expect(screen.getByRole("alert")).toHaveTextContent("abc123");
  });

  it("formats money with currency", () => {
    expect(formatMoney(13.9, "USD", "en-US")).toBe("$13.90");
  });
});

describe("ErrorAlert", () => {
  it("explains an unreachable gateway", () => {
    render(<ErrorAlert error={{ message: "Request failed with status 502", status: 502 }} />);
    expect(screen.getByRole("alert")).toHaveTextContent("Cannot reach the platform");
  });
});
