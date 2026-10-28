/** The restaurant/branch/table the guest is at (from the QR link). */
export interface Venue {
  tenantId: string;
  branchId: string;
  tableId: string | null;
}

const KEY = "rai.consumer.venue";

export function rememberVenue(venue: Venue): void {
  try {
    sessionStorage.setItem(KEY, JSON.stringify(venue));
  } catch {
    // storage unavailable: venue only lives in the URL
  }
}

export function lastVenue(): Venue | null {
  try {
    const raw = sessionStorage.getItem(KEY);
    return raw ? (JSON.parse(raw) as Venue) : null;
  } catch {
    return null;
  }
}

export function venuePath(venue: Venue, page = ""): string {
  const base = `/r/${venue.tenantId}/${venue.branchId}${page ? `/${page}` : ""}`;
  return venue.tableId ? `${base}?table=${venue.tableId}` : base;
}
