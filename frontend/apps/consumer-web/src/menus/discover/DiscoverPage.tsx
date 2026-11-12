import type { DiscoverySearch } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Badge, Button, Card, EmptyState, ErrorAlert, Field, formatMoney, PageHeader, Spinner } from "@restaurant-ai/ui";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useDeferredValue, useState, type FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";
import { DIET_OPTIONS, joinDishesWithBranches, RADIUS_OPTIONS_KM, toggle, uniqueBranchIds } from "./discovery";
import { DiscoveryAssistant } from "./DiscoveryAssistant";
import { FavoriteButton, RestaurantCard } from "./RestaurantCard";
import { useNearMe, type Coordinates } from "./useLocation";

type Mode = "restaurants" | "dishes";

export function DiscoverPage() {
  const [params, setParams] = useSearchParams();
  const mode: Mode = params.get("tab") === "dishes" ? "dishes" : "restaurants";
  const [text, setText] = useState("");
  const [diets, setDiets] = useState<string[]>([]);
  const [openNow, setOpenNow] = useState(false);
  const [radiusKm, setRadiusKm] = useState(10);
  const nearMe = useNearMe();
  const query = useDeferredValue(text.trim());

  return (
    <>
      <PageHeader title="Discover" description="Find restaurants and dishes near you, then order or reserve." />

      <DiscoveryAssistant nearMe={nearMe} />

      <div className="row" role="tablist" aria-label="Search for">
        {(["restaurants", "dishes"] as const).map((tab) => (
          <button
            key={tab}
            type="button"
            role="tab"
            aria-selected={mode === tab}
            className={mode === tab ? "btn btn-primary btn-sm" : "btn btn-sm"}
            onClick={() => setParams(tab === "dishes" ? { tab } : {})}
          >
            {tab === "restaurants" ? "Restaurants" : "Dishes"}
          </button>
        ))}
      </div>

      <Card>
        <Field label={mode === "restaurants" ? "Restaurant or cuisine" : "Dish"}>
          <input
            type="search"
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder={mode === "restaurants" ? "Pizza, Thai, Spice Route…" : "Ramen, biryani, vegan burger…"}
          />
        </Field>
        <div className="row" role="group" aria-label="Dietary filters">
          {DIET_OPTIONS.map((diet) => (
            <button key={diet} type="button" className="chip" aria-pressed={diets.includes(diet)} onClick={() => setDiets((d) => toggle(d, diet))}>
              {diet}
            </button>
          ))}
        </div>
        <div className="row">
          <button type="button" className="chip" aria-pressed={openNow} onClick={() => setOpenNow((v) => !v)}>
            Open now
          </button>
          {nearMe.coords ? (
            <>
              <button type="button" className="chip" aria-pressed onClick={nearMe.clear}>
                Near me ✕
              </button>
              <label className="row small">
                Within
                <select value={radiusKm} onChange={(e) => setRadiusKm(Number(e.target.value))} aria-label="Search radius">
                  {RADIUS_OPTIONS_KM.map((km) => (
                    <option key={km} value={km}>
                      {km} km
                    </option>
                  ))}
                </select>
              </label>
            </>
          ) : (
            <button type="button" className="chip" onClick={nearMe.locate} disabled={nearMe.state.status === "locating"}>
              {nearMe.state.status === "locating" ? "Locating…" : "Near me"}
            </button>
          )}
        </div>
        {nearMe.state.status === "error" && <p className="small muted">{nearMe.state.message}</p>}
      </Card>

      {mode === "restaurants" ? (
        <RestaurantResults query={query} diets={diets} openNow={openNow} coords={nearMe.coords} radiusKm={radiusKm} />
      ) : (
        <DishResults query={query} diets={diets} openNow={openNow} coords={nearMe.coords} radiusKm={radiusKm} />
      )}

      <QrEntry />
    </>
  );
}

interface ResultProps {
  query: string;
  diets: string[];
  openNow: boolean;
  coords: Coordinates | null;
  radiusKm: number;
}

function locationParams(coords: Coordinates | null, radiusKm: number): Partial<DiscoverySearch> {
  return coords ? { lat: coords.lat, lng: coords.lng, radiusKm } : {};
}

function RestaurantResults({ query, diets, openNow, coords, radiusKm }: ResultProps) {
  const api = useApi();
  const [cuisines, setCuisines] = useState<string[]>([]);
  const cuisineOptions = useQuery({ queryKey: ["discovery", "cuisines"], queryFn: ({ signal }) => api.discovery.cuisines(signal), staleTime: 300_000 });

  const search: DiscoverySearch = { q: query || undefined, cuisines, diets, openNow, ...locationParams(coords, radiusKm), limit: 60 };
  const results = useQuery({
    queryKey: ["discovery", "restaurants", search],
    queryFn: ({ signal }) => api.discovery.restaurants(search, signal),
    placeholderData: keepPreviousData,
  });

  return (
    <section className="stack" aria-label="Restaurants">
      {cuisineOptions.data && cuisineOptions.data.length > 0 && (
        <div className="row" role="group" aria-label="Cuisines">
          {cuisineOptions.data.map((c) => (
            <button key={c} type="button" className="chip" aria-pressed={cuisines.includes(c)} onClick={() => setCuisines((list) => toggle(list, c))}>
              {c}
            </button>
          ))}
        </div>
      )}
      <ErrorAlert error={results.error} title="Could not search restaurants" />
      {results.isPending && <Spinner label="Finding restaurants" />}
      {results.data?.length === 0 && (
        <EmptyState title="No restaurants found">{coords ? "Try a larger radius or remove a filter." : "Try removing a filter."}</EmptyState>
      )}
      <div className="grid">
        {results.data?.map((branch) => (
          <RestaurantCard key={branch.branchId} branch={branch} />
        ))}
      </div>
    </section>
  );
}

function DishResults({ query, diets, openNow, coords, radiusKm }: ResultProps) {
  const api = useApi();
  const [maxPrice, setMaxPrice] = useState("");
  const hasCriteria = query.length > 0 || diets.length > 0;

  const dishes = useQuery({
    queryKey: ["discovery", "dishes", { query, diets, maxPrice }],
    queryFn: ({ signal }) => api.discovery.dishes({ q: query || undefined, diets, maxPrice: maxPrice ? Number(maxPrice) : undefined, limit: 200 }, signal),
    enabled: hasCriteria,
    placeholderData: keepPreviousData,
  });

  const branchIds = uniqueBranchIds(dishes.data ?? []);
  const branchSearch: DiscoverySearch = { branchIds, openNow, ...locationParams(coords, radiusKm), limit: 100 };
  const branches = useQuery({
    queryKey: ["discovery", "restaurants", branchSearch],
    queryFn: ({ signal }) => api.discovery.restaurants(branchSearch, signal),
    enabled: branchIds.length > 0,
  });

  const matches = joinDishesWithBranches(dishes.data ?? [], branches.data ?? []);
  const loading = hasCriteria && (dishes.isPending || (branchIds.length > 0 && branches.isPending));

  return (
    <section className="stack" aria-label="Dishes">
      <div style={{ maxWidth: 220 }}>
        <Field label="Max price">
          <input type="number" min="0" step="0.5" inputMode="decimal" value={maxPrice} onChange={(e) => setMaxPrice(e.target.value)} />
        </Field>
      </div>
      {!hasCriteria && <EmptyState title="Search for a dish">Type a dish name or pick a dietary filter.</EmptyState>}
      <ErrorAlert error={dishes.error ?? branches.error} title="Could not search dishes" />
      {loading && <Spinner label="Finding dishes" />}
      {hasCriteria && !loading && matches.length === 0 && <EmptyState title="No dishes found">Try a different name or filter.</EmptyState>}
      <div className="grid">
        {matches.map(({ dish, branch }) => (
          <Card as="article" key={dish.menuItemId}>
            <div className="row row-between">
              <h3>{dish.name}</h3>
              <strong>{formatMoney(dish.price, dish.currency)}</strong>
            </div>
            {dish.description && <p className="small muted">{dish.description}</p>}
            <div className="row">
              {dish.dietaryTags.map((t) => (
                <Badge key={t}>{t}</Badge>
              ))}
            </div>
            <div className="row row-between small" style={{ marginTop: "auto" }}>
              <span>
                at <strong>{branch.restaurantName}</strong> · {branch.branchName}
                {branch.distanceKm !== null && ` · ${branch.distanceKm.toFixed(1)} km`}
                {!branch.isOpenNow && " · closed now"}
              </span>
              <FavoriteButton restaurantId={branch.tenantId} restaurantName={branch.restaurantName} />
            </div>
            <Link className="btn btn-primary btn-sm" to={`/r/${branch.tenantId}/${branch.branchId}`}>
              Order here
            </Link>
          </Card>
        ))}
      </div>
    </section>
  );
}

/** For guests who can't scan: the code printed under the table's QR. */
function QrEntry() {
  const navigate = useNavigate();
  const [code, setCode] = useState("");

  function open(event: FormEvent) {
    event.preventDefault();
    navigate(`/t/${encodeURIComponent(code.trim())}`);
  }

  return (
    <details className="card">
      <summary className="small muted" style={{ cursor: "pointer" }}>
        At a table? Enter the code printed under the QR
      </summary>
      <form className="row" onSubmit={open} style={{ marginTop: 12 }}>
        <Field label="Table code">
          <input value={code} onChange={(e) => setCode(e.target.value)} required minLength={8} autoCapitalize="off" spellCheck={false} />
        </Field>
        <div style={{ alignSelf: "flex-end" }}>
          <Button type="submit">Open menu</Button>
        </div>
      </form>
    </details>
  );
}
