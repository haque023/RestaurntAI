import { useEffect, useReducer, type ReactNode } from "react";
import { cartReducer, emptyCart, type CartState } from "./cart";
import { CartContext } from "./useCart";

function load(key: string): CartState {
  try {
    const raw = sessionStorage.getItem(key);
    return raw ? (JSON.parse(raw) as CartState) : emptyCart;
  } catch {
    return emptyCart;
  }
}

/** One cart per branch, kept for the browser tab. */
export function CartProvider({ branchId, children }: { branchId: string; children: ReactNode }) {
  const storageKey = `rai.consumer.cart.${branchId}`;
  const [cart, dispatch] = useReducer(cartReducer, storageKey, load);

  useEffect(() => {
    try {
      sessionStorage.setItem(storageKey, JSON.stringify(cart));
    } catch {
      // storage unavailable: cart stays in memory
    }
  }, [cart, storageKey]);

  return <CartContext.Provider value={{ cart, dispatch }}>{children}</CartContext.Provider>;
}
