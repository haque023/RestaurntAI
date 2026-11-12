import type { DiscoveryBranch, DishResult } from "@restaurant-ai/api-client";

export const DIET_OPTIONS = ["vegan", "vegetarian", "halal", "gluten-free", "spicy", "nut-free"];
export const RADIUS_OPTIONS_KM = [2, 5, 10, 25, 50];

export interface DishMatch {
  dish: DishResult;
  branch: DiscoveryBranch;
}

/**
 * Joins dish search results with listed branches. Dishes from restaurants that are not listed
 * (or outside the chosen radius) have no branch and are dropped. Keeps the branch order
 * (nearest first when a location is used), then cheapest dish first.
 */
export function joinDishesWithBranches(dishes: DishResult[], branches: DiscoveryBranch[]): DishMatch[] {
  const rank = new Map(branches.map((b, i) => [b.branchId, i]));
  const byId = new Map(branches.map((b) => [b.branchId, b]));

  return dishes
    .filter((d) => byId.has(d.branchId))
    .map((dish) => ({ dish, branch: byId.get(dish.branchId)! }))
    .sort((a, b) => rank.get(a.branch.branchId)! - rank.get(b.branch.branchId)! || a.dish.price - b.dish.price);
}

export function uniqueBranchIds(dishes: DishResult[], max = 100): string[] {
  return [...new Set(dishes.map((d) => d.branchId))].slice(0, max);
}

export function formatDistance(km: number | null): string | null {
  if (km === null) return null;
  return km < 1 ? `${Math.round(km * 1000)} m` : `${km.toFixed(km < 10 ? 1 : 0)} km`;
}

export function toggle<T>(list: T[], value: T): T[] {
  return list.includes(value) ? list.filter((v) => v !== value) : [...list, value];
}
