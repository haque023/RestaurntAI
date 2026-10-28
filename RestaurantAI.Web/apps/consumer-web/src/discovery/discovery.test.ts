import type { DiscoveryBranch, DishResult } from "@restaurant-ai/api-client";
import { describe, expect, it } from "vitest";
import { formatDistance, joinDishesWithBranches, toggle, uniqueBranchIds } from "./discovery";

const branch = (branchId: string, name: string): DiscoveryBranch => ({
  tenantId: `t-${branchId}`,
  branchId,
  restaurantName: name,
  description: "",
  cuisines: [],
  dietaryOptions: [],
  branchName: "Main",
  address: "",
  latitude: null,
  longitude: null,
  distanceKm: null,
  isOpenNow: true,
  openingHours: [],
});

const dish = (branchId: string, name: string, price: number): DishResult => ({
  menuItemId: `${branchId}-${name}`,
  tenantId: `t-${branchId}`,
  branchId,
  name,
  category: "",
  description: "",
  dietaryTags: [],
  price,
  currency: "USD",
});

describe("joinDishesWithBranches", () => {
  it("drops dishes from unlisted branches and orders by branch rank then price", () => {
    const matches = joinDishesWithBranches(
      [dish("b2", "Ramen", 12), dish("b1", "Ramen XL", 15), dish("b1", "Ramen", 11), dish("hidden", "Ramen", 5)],
      [branch("b1", "Near"), branch("b2", "Far")],
    );

    expect(matches.map((m) => `${m.branch.restaurantName}:${m.dish.price}`)).toEqual(["Near:11", "Near:15", "Far:12"]);
  });
});

describe("helpers", () => {
  it("collects unique branch ids", () => {
    expect(uniqueBranchIds([dish("b1", "a", 1), dish("b1", "b", 2), dish("b2", "c", 3)])).toEqual(["b1", "b2"]);
  });

  it("formats distances", () => {
    expect(formatDistance(null)).toBeNull();
    expect(formatDistance(0.45)).toBe("450 m");
    expect(formatDistance(3.26)).toBe("3.3 km");
    expect(formatDistance(18.4)).toBe("18 km");
  });

  it("toggles values", () => {
    expect(toggle(["vegan"], "halal")).toEqual(["vegan", "halal"]);
    expect(toggle(["vegan", "halal"], "vegan")).toEqual(["halal"]);
  });
});
