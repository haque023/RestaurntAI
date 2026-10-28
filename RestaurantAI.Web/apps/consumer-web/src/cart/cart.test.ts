import type { MenuItem } from "@restaurant-ai/api-client";
import { describe, expect, it } from "vitest";
import { cartEstimate, cartItemCount, cartReducer, emptyCart } from "./cart";

const ramen: MenuItem = {
  id: "ramen",
  branchId: "b1",
  category: "Mains",
  name: "Spicy Ramen",
  description: "",
  price: 13.9,
  currency: "USD",
  isAvailable: true,
  dietaryTags: ["spicy"],
  ingredients: [],
};

describe("cartReducer", () => {
  it("adds items and merges quantities", () => {
    let cart = cartReducer(emptyCart, { type: "add", item: ramen });
    cart = cartReducer(cart, { type: "add", item: ramen, quantity: 2 });

    expect(cart.lines).toHaveLength(1);
    expect(cartItemCount(cart)).toBe(3);
    expect(cartEstimate(cart)).toEqual({ amount: 13.9 * 3, currency: "USD" });
  });

  it("removes a line when quantity reaches zero and caps at 99", () => {
    let cart = cartReducer(emptyCart, { type: "add", item: ramen });
    cart = cartReducer(cart, { type: "setQuantity", menuItemId: "ramen", quantity: 500 });
    expect(cart.lines[0]?.quantity).toBe(99);

    cart = cartReducer(cart, { type: "setQuantity", menuItemId: "ramen", quantity: 0 });
    expect(cart.lines).toHaveLength(0);
  });

  it("keeps the checkout key across retries but drops it when the cart changes", () => {
    let cart = cartReducer(emptyCart, { type: "add", item: ramen });
    cart = cartReducer(cart, { type: "beginCheckout", key: "k1" });
    cart = cartReducer(cart, { type: "beginCheckout", key: "k2" });
    expect(cart.checkoutKey).toBe("k1");

    cart = cartReducer(cart, { type: "setNotes", menuItemId: "ramen", notes: "extra hot" });
    expect(cart.checkoutKey).toBeNull();
  });
});
