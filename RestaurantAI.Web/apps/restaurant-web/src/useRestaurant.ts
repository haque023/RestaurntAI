import { ApiError } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { useQuery } from "@tanstack/react-query";

/** The signed-in user's restaurant (tenant). Resolves to null if it has not been registered. */
export function useRestaurant() {
  const api = useApi();
  return useQuery({
    queryKey: ["restaurant", "current"],
    queryFn: () => api.restaurants.current().catch((e: unknown) => (e instanceof ApiError && e.status === 404 ? null : Promise.reject(e))),
  });
}
