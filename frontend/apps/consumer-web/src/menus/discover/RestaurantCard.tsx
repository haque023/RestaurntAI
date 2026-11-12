import type { DiscoveryBranch } from "@restaurant-ai/api-client";
import { Badge, Card } from "@restaurant-ai/ui";
import type { ReactNode } from "react";
import { Link, useLocation, useNavigate } from "react-router";
import { formatDistance } from "./discovery";
import { useFavorites } from "../my-restaurants/useFavorites";

export function FavoriteButton({ restaurantId, restaurantName }: { restaurantId: string; restaurantName: string }) {
  const { isFavorite, toggle, canSave } = useFavorites();
  const navigate = useNavigate();
  const location = useLocation();
  const saved = isFavorite(restaurantId);

  return (
    <button
      type="button"
      className="btn btn-ghost btn-sm"
      aria-pressed={saved}
      aria-label={saved ? `Remove ${restaurantName} from my restaurants` : `Save ${restaurantName} to my restaurants`}
      title={canSave ? undefined : "Sign in to save restaurants"}
      onClick={() => (canSave ? toggle.mutate(restaurantId) : navigate("/login", { state: { from: location.pathname + location.search } }))}
      style={{ fontSize: "1.25rem", color: saved ? "var(--color-primary)" : "var(--color-text-muted)" }}
    >
      {saved ? "♥" : "♡"}
    </button>
  );
}

export function RestaurantCard({ branch, children }: { branch: DiscoveryBranch; children?: ReactNode }) {
  const distance = formatDistance(branch.distanceKm);

  return (
    <Card as="article">
      <div className="row row-between" style={{ alignItems: "flex-start" }}>
        <div>
          <h3>{branch.restaurantName}</h3>
          <div className="small muted">
            {branch.branchName}
            {branch.address && ` · ${branch.address}`}
          </div>
        </div>
        <FavoriteButton restaurantId={branch.tenantId} restaurantName={branch.restaurantName} />
      </div>
      {branch.description && <p className="small muted">{branch.description}</p>}
      <div className="row">
        {branch.isOpenNow ? <Badge tone="success">Open now</Badge> : <Badge>Closed</Badge>}
        {distance && <Badge>{distance}</Badge>}
        {branch.cuisines.map((c) => (
          <Badge key={c}>{c}</Badge>
        ))}
        {branch.dietaryOptions.map((d) => (
          <Badge key={d} tone="warning">
            {d}
          </Badge>
        ))}
      </div>
      {children}
      <div className="row" style={{ marginTop: "auto" }}>
        <Link className="btn btn-primary btn-sm" to={`/r/${branch.tenantId}/${branch.branchId}`}>
          View menu
        </Link>
        <Link className="btn btn-sm" to={`/r/${branch.tenantId}/${branch.branchId}/reserve`}>
          Reserve
        </Link>
      </div>
    </Card>
  );
}
