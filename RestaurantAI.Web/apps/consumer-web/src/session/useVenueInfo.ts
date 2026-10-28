import { useApi } from "@restaurant-ai/auth";
import { useQuery } from "@tanstack/react-query";

/** Public name, address and hours of the branch the guest is at. */
export function useVenueInfo(branchId: string) {
  const api = useApi();
  return useQuery({
    queryKey: ["venue-info", branchId],
    queryFn: async ({ signal }) => (await api.discovery.restaurants({ branchIds: [branchId], limit: 1 }, signal))[0] ?? null,
    staleTime: 300_000,
  });
}
