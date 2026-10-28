import type { DayOfWeek, OpeningPeriod } from "@restaurant-ai/api-client";

export const WEEK: DayOfWeek[] = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

/** One editable row per weekday (the editor supports one period per day). */
export interface DayHours {
  day: DayOfWeek;
  open: boolean;
  opens: string; // "HH:mm"
  closes: string; // "HH:mm"
}

const toInput = (time: string) => time.slice(0, 5);
const toApi = (time: string) => (time.length === 5 ? `${time}:00` : time);

export function toDayRows(periods: OpeningPeriod[]): DayHours[] {
  return WEEK.map((day) => {
    const period = periods.find((p) => p.day === day);
    return period
      ? { day, open: true, opens: toInput(period.opens), closes: toInput(period.closes) }
      : { day, open: false, opens: "11:00", closes: "22:00" };
  });
}

export function toPeriods(rows: DayHours[]): OpeningPeriod[] {
  return rows.filter((r) => r.open).map((r) => ({ day: r.day, opens: toApi(r.opens), closes: toApi(r.closes) }));
}

export function validateRows(rows: DayHours[]): string | null {
  const bad = rows.find((r) => r.open && r.opens === r.closes);
  return bad ? `${bad.day}: opening and closing times must differ.` : null;
}

export const isOvernight = (row: DayHours) => row.open && row.closes < row.opens;
