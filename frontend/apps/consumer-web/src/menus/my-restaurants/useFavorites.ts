import type { FavoriteRestaurant } from "@restaurant-ai/api-client";
import { useApi, useAuth } from "@restaurant-ai/auth";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

const KEY = ["favorites"];

/** The guest's saved restaurants. Empty and read-only when signed out. */
export function useFavorites() {
  const api = useApi();
  const { isAuthenticated, user } = useAuth();
  const queryClient = useQueryClient();
  const queryKey = [...KEY, user?.userId];

  const favorites = useQuery({ queryKey, queryFn: () => api.consumer.favorites(), enabled: isAuthenticated });
  const ids = new Set((favorites.data ?? []).map((f) => f.restaurantId));

  const toggle = useMutation({
    mutationFn: async (restaurantId: string) => {
      if (ids.has(restaurantId)) await api.consumer.removeFavorite(restaurantId);
      else await api.consumer.addFavorite(restaurantId);
    },
    // Optimistic: the heart responds immediately and rolls back if the call fails.
    onMutate: async (restaurantId) => {
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData<FavoriteRestaurant[]>(queryKey) ?? [];
      const next = ids.has(restaurantId)
        ? previous.filter((f) => f.restaurantId !== restaurantId)
        : [{ restaurantId, savedAt: new Date().toISOString() }, ...previous];
      queryClient.setQueryData(queryKey, next);
      return { previous };
    },
    onError: (_error, _id, context) => queryClient.setQueryData(queryKey, context?.previous),
    onSettled: () => queryClient.invalidateQueries({ queryKey }),
  });

  return { favorites, isFavorite: (restaurantId: string) => ids.has(restaurantId), toggle, canSave: isAuthenticated };
}
