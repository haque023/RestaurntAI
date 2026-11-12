import { createContext, useContext, type Dispatch } from "react";
import type { CartAction, CartState } from "./cart";

export const CartContext = createContext<{ cart: CartState; dispatch: Dispatch<CartAction> } | null>(null);

export function useCart() {
  const context = useContext(CartContext);
  if (!context) throw new Error("useCart must be used inside <CartProvider>.");
  return context;
}
