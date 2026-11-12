import { ApiError } from "@restaurant-ai/api-client";
import { useApi, useAuth } from "@restaurant-ai/auth";
import { useQuery } from "@tanstack/react-query";

/** The signed-in user's restaurant (tenant) with full settings; owners and managers only. */
export function useRestaurant() {
  const api = useApi();
  return useQuery({
    queryKey: ["restaurant", "current"],
    queryFn: () => api.restaurants.current().catch((e: unknown) => (e instanceof ApiError && e.status === 404 ? null : Promise.reject(e))),
  });
}

/** Name, status and the branches this staff member may work in; readable by every role. */
export function useRestaurantSummary() {
  const api = useApi();
  const { user } = useAuth();
  return useQuery({
    queryKey: ["restaurant", "summary", user?.tenantId],
    queryFn: ({ signal }) => api.restaurants.summary(signal),
    enabled: Boolean(user?.tenantId),
    staleTime: 60_000,
  });
}
