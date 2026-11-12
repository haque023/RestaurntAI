import { useApi } from "@restaurant-ai/auth";
import { EmptyState, ErrorAlert, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router";
import { RestaurantCard } from "../discover/RestaurantCard";
import { useFavorites } from "./useFavorites";

export function FavoritesPage() {
  const api = useApi();
  const { favorites } = useFavorites();
  const restaurantIds = (favorites.data ?? []).map((f) => f.restaurantId).slice(0, 100);

  // Hydrate saved restaurants from discovery (only listed ones appear).
  const branches = useQuery({
    queryKey: ["discovery", "restaurants", { tenantIds: restaurantIds }],
    queryFn: ({ signal }) => api.discovery.restaurants({ tenantIds: restaurantIds, limit: 100 }, signal),
    enabled: restaurantIds.length > 0,
  });

  const unavailable = restaurantIds.filter((id) => branches.data && !branches.data.some((b) => b.tenantId === id)).length;

  return (
    <>
      <PageHeader title="My restaurants" description="Restaurants you saved. Tap ♥ anywhere to add or remove." />
      <ErrorAlert error={favorites.error ?? branches.error} title="Could not load your restaurants" />
      {(favorites.isPending || (restaurantIds.length > 0 && branches.isPending)) && <Spinner />}
      {favorites.data?.length === 0 && (
        <EmptyState title="No saved restaurants yet">
          <Link to="/">Discover restaurants</Link>
        </EmptyState>
      )}
      <div className="grid">
        {branches.data?.map((branch) => (
          <RestaurantCard key={branch.branchId} branch={branch} />
        ))}
      </div>
      {unavailable > 0 && <p className="small muted">{unavailable} saved restaurant(s) are not currently listed.</p>}
    </>
  );
}
