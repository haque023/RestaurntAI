import { useAuth } from "@restaurant-ai/auth";
import { useSearchParams } from "react-router";
import { useRestaurantSummary } from "./useRestaurant";

/**
 * The branch a page works on: the staff member's own branch when they are branch-bound,
 * otherwise ?branch= or the first branch.
 */
export function useBranch() {
  const { user } = useAuth();
  const summary = useRestaurantSummary();
  const [search, setSearch] = useSearchParams();
  const branches = summary.data?.branches ?? [];
  const branchId = user?.branchId ?? search.get("branch") ?? branches[0]?.id ?? "";
  const branch = branches.find((b) => b.id === branchId) ?? null;

  const select = (id: string) =>
    setSearch((current) => {
      const next = new URLSearchParams(current);
      next.set("branch", id);
      return next;
    });

  return { summary, branches, branchId, branch, select, locked: Boolean(user?.branchId) };
}
