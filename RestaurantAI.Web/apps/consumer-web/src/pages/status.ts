import type { OrderStatus, ReservationStatus } from "@restaurant-ai/api-client";
import type { BadgeTone } from "@restaurant-ai/ui";

export const ORDER_STEPS: OrderStatus[] = ["Placed", "Accepted", "Preparing", "Ready", "Served", "Completed"];

export const isFinalOrderStatus = (status: OrderStatus) => status === "Completed" || status === "Cancelled";

/** Mirrors the backend rule: only orders not yet in preparation can be cancelled. */
export const canCancelOrder = (status: OrderStatus) => status === "Placed" || status === "Accepted";

export function orderTone(status: OrderStatus): BadgeTone {
  if (status === "Cancelled") return "danger";
  if (status === "Completed" || status === "Ready" || status === "Served") return "success";
  return "warning";
}

export function reservationTone(status: ReservationStatus): BadgeTone {
  if (status === "Cancelled" || status === "NoShow") return "danger";
  if (status === "Confirmed" || status === "Seated" || status === "Completed") return "success";
  return "warning";
}
