import { describe, expect, it } from "vitest";
import { isOvernight, toDayRows, toPeriods, validateRows } from "./hours";

describe("opening hours editor", () => {
  it("round-trips API periods through weekday rows", () => {
    const periods = [
      { day: "Monday" as const, opens: "11:00:00", closes: "15:00:00" },
      { day: "Friday" as const, opens: "18:00:00", closes: "02:00:00" },
    ];

    const rows = toDayRows(periods);

    expect(rows).toHaveLength(7);
    expect(rows.find((r) => r.day === "Monday")).toEqual({ day: "Monday", open: true, opens: "11:00", closes: "15:00" });
    expect(rows.find((r) => r.day === "Tuesday")?.open).toBe(false);
    expect(toPeriods(rows)).toEqual(periods);
  });

  it("flags overnight periods and identical times", () => {
    const rows = toDayRows([{ day: "Friday", opens: "18:00:00", closes: "02:00:00" }]);
    expect(isOvernight(rows.find((r) => r.day === "Friday")!)).toBe(true);

    rows[0] = { day: "Monday", open: true, opens: "10:00", closes: "10:00" };
    expect(validateRows(rows)).toMatch(/Monday/);
  });
});
