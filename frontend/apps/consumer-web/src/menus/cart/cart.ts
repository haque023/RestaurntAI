import type { MenuItem } from "@restaurant-ai/api-client";

export interface CartLine {
  menuItemId: string;
  name: string;
  unitPrice: number;
  currency: string;
  quantity: number;
  notes: string;
}

export interface CartState {
  lines: CartLine[];
  /**
   * Idempotency key of the confirmation in progress. Created when the guest presses
   * "Place order", reused if that request is retried, and dropped whenever the cart changes
   * (a different cart is a different order).
   */
  checkoutKey: string | null;
}

export type CartAction =
  | { type: "add"; item: MenuItem; quantity?: number }
  | { type: "setQuantity"; menuItemId: string; quantity: number }
  | { type: "setNotes"; menuItemId: string; notes: string }
  | { type: "beginCheckout"; key: string }
  | { type: "clear" };

export const MAX_QUANTITY = 99;
export const emptyCart: CartState = { lines: [], checkoutKey: null };

export function cartReducer(state: CartState, action: CartAction): CartState {
  switch (action.type) {
    case "add": {
      const quantity = action.quantity ?? 1;
      const existing = state.lines.find((l) => l.menuItemId === action.item.id);
      const lines = existing
        ? state.lines.map((l) => (l === existing ? { ...l, quantity: Math.min(MAX_QUANTITY, l.quantity + quantity) } : l))
        : [
            ...state.lines,
            {
              menuItemId: action.item.id,
              name: action.item.name,
              unitPrice: action.item.price,
              currency: action.item.currency,
              quantity: Math.min(MAX_QUANTITY, quantity),
              notes: "",
            },
          ];
      return { lines, checkoutKey: null };
    }
    case "setQuantity": {
      const quantity = Math.min(MAX_QUANTITY, Math.max(0, Math.trunc(action.quantity)));
      const lines =
        quantity === 0
          ? state.lines.filter((l) => l.menuItemId !== action.menuItemId)
          : state.lines.map((l) => (l.menuItemId === action.menuItemId ? { ...l, quantity } : l));
      return { lines, checkoutKey: null };
    }
    case "setNotes":
      return {
        lines: state.lines.map((l) => (l.menuItemId === action.menuItemId ? { ...l, notes: action.notes } : l)),
        checkoutKey: null,
      };
    case "beginCheckout":
      return state.checkoutKey ? state : { ...state, checkoutKey: action.key };
    case "clear":
      return emptyCart;
  }
}

export function cartItemCount(state: CartState): number {
  return state.lines.reduce((sum, l) => sum + l.quantity, 0);
}

/** Display estimate only. The order service recalculates prices from the menu. */
export function cartEstimate(state: CartState): { amount: number; currency: string } | null {
  const first = state.lines[0];
  if (!first) return null;
  return { amount: state.lines.reduce((sum, l) => sum + l.unitPrice * l.quantity, 0), currency: first.currency };
}
