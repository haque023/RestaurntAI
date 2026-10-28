import type { MenuItem } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Badge, Button, Card, EmptyState, ErrorAlert, Field, formatMoney, PageHeader, Spinner } from "@restaurant-ai/ui";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useDeferredValue, useMemo, useState } from "react";
import { Link, useParams } from "react-router";
import { cartItemCount } from "../cart/cart";
import { useCart } from "../cart/useCart";
import { useVenueTable } from "../session/useVenueTable";
import { venuePath } from "../venue";
import { useVenueInfo } from "../session/useVenueInfo";
import { StaffCall } from "../waiter/StaffCall";
import { WaiterChat } from "../waiter/WaiterChat";

const DIETARY_FILTERS = ["vegan", "vegetarian", "gluten-free", "spicy", "halal"];

export function MenuPage() {
  const { tenantId = "", branchId = "" } = useParams();
  const tableId = useVenueTable();
  const api = useApi();
  const { cart, dispatch } = useCart();

  const [text, setText] = useState("");
  const [maxPrice, setMaxPrice] = useState("");
  const [dietary, setDietary] = useState<string[]>([]);
  const query = useDeferredValue(text.trim());
  const filtering = query !== "" || maxPrice !== "" || dietary.length > 0;

  const menu = useQuery({
    queryKey: ["menu", branchId, { query, maxPrice, dietary }],
    queryFn: ({ signal }) =>
      filtering
        ? api.menu.search(branchId, { q: query || undefined, maxPrice: maxPrice ? Number(maxPrice) : undefined, dietary }, signal)
        : api.menu.list(branchId, signal),
    placeholderData: keepPreviousData,
  });

  const venue = useVenueInfo(branchId);
  const byCategory = useMemo(() => groupByCategory(menu.data ?? []), [menu.data]);
  const count = cartItemCount(cart);
  const cartLink = venuePath({ tenantId, branchId, tableId }, "cart");

  function toggle(tag: string) {
    setDietary((current) => (current.includes(tag) ? current.filter((t) => t !== tag) : [...current, tag]));
  }

  return (
    <>
      <PageHeader
        title={venue.data?.restaurantName ?? "Menu"}
        description={
          venue.data
            ? `${venue.data.branchName} · ${venue.data.address}${venue.data.isOpenNow ? "" : " · closed now"}${tableId ? " · you're at a table" : ""}`
            : "Live prices and availability from the kitchen."
        }
        actions={
          <>
            <Link className="btn" to={venuePath({ tenantId, branchId, tableId }, "orders")}>
              My orders
            </Link>
            <Link className="btn btn-primary" to={cartLink}>
              Cart ({count})
            </Link>
          </>
        }
      />

      {tableId && <StaffCall />}
      <WaiterChat branchId={branchId} tableId={tableId} />

      <Card>
        <div className="form-grid">
          <Field label="Search">
            <input type="search" value={text} onChange={(e) => setText(e.target.value)} placeholder="Something spicy, noodles…" />
          </Field>
          <Field label="Max price">
            <input type="number" min="0" step="0.5" inputMode="decimal" value={maxPrice} onChange={(e) => setMaxPrice(e.target.value)} />
          </Field>
        </div>
        <div className="row" role="group" aria-label="Dietary filters">
          {DIETARY_FILTERS.map((tag) => (
            <button key={tag} type="button" className="chip" aria-pressed={dietary.includes(tag)} onClick={() => toggle(tag)}>
              {tag}
            </button>
          ))}
        </div>
      </Card>

      <ErrorAlert error={menu.error} title="Could not load the menu" />
      {menu.isPending && <Spinner label="Loading menu" />}
      {menu.data && menu.data.length === 0 && (
        <EmptyState title={filtering ? "No dishes match your filters" : "The menu is empty"}>
          {filtering && <span>Try removing a filter.</span>}
        </EmptyState>
      )}

      {byCategory.map(([category, items]) => (
        <section key={category} className="stack" aria-labelledby={`cat-${category}`}>
          <h2 id={`cat-${category}`}>{category}</h2>
          <div className="grid">
            {items.map((item) => (
              <MenuItemCard key={item.id} item={item} onAdd={() => dispatch({ type: "add", item })} />
            ))}
          </div>
        </section>
      ))}
    </>
  );
}

function MenuItemCard({ item, onAdd }: { item: MenuItem; onAdd: () => void }) {
  return (
    <Card as="article">
      <div className="row row-between">
        <h3>{item.name}</h3>
        <strong>{formatMoney(item.price, item.currency)}</strong>
      </div>
      {item.description && <p className="muted small">{item.description}</p>}
      {item.dietaryTags.length > 0 && (
        <div className="row">
          {item.dietaryTags.map((tag) => (
            <Badge key={tag}>{tag}</Badge>
          ))}
        </div>
      )}
      <div className="row row-between" style={{ marginTop: "auto" }}>
        {item.isAvailable ? <Badge tone="success">Available</Badge> : <Badge tone="danger">Unavailable</Badge>}
        <Button size="sm" variant="primary" disabled={!item.isAvailable} onClick={onAdd} aria-label={`Add ${item.name} to cart`}>
          Add
        </Button>
      </div>
    </Card>
  );
}

function groupByCategory(items: MenuItem[]): [string, MenuItem[]][] {
  const groups = new Map<string, MenuItem[]>();
  for (const item of items) {
    const key = item.category || "Other";
    groups.set(key, [...(groups.get(key) ?? []), item]);
  }
  return [...groups.entries()];
}
