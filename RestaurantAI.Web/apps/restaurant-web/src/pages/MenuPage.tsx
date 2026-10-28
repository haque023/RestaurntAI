import type { CreateMenuItemRequest } from "@restaurant-ai/api-client";
import { useApi, useAuth } from "@restaurant-ai/auth";
import { Badge, Button, Card, EmptyState, ErrorAlert, Field, formatMoney, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { useSearchParams } from "react-router";
import { useRestaurant } from "../useRestaurant";

export function MenuPage() {
  const restaurant = useRestaurant();
  const { user } = useAuth();
  const [search, setSearch] = useSearchParams();
  const branches = restaurant.data?.branches ?? [];
  // Branch-bound staff only see their own branch.
  const branchId = user?.branchId ?? search.get("branch") ?? branches[0]?.id ?? "";

  if (restaurant.isPending) return <Spinner />;
  if (restaurant.error) return <ErrorAlert error={restaurant.error} />;
  if (branches.length === 0) return <EmptyState title="No branches yet">Add a branch on the Restaurant page first.</EmptyState>;

  return (
    <>
      <PageHeader
        title="Menu"
        description="Prices and availability here are what guests and the AI waiter see."
        actions={
          !user?.branchId && (
            <label className="field" style={{ minWidth: 220 }}>
              <span>Branch</span>
              <select value={branchId} onChange={(e) => setSearch({ branch: e.target.value })}>
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
            </label>
          )
        }
      />
      {branchId && <MenuTable branchId={branchId} />}
      {branchId && <NewItemForm branchId={branchId} currency={restaurant.data?.defaultCurrency ?? "USD"} />}
    </>
  );
}

function MenuTable({ branchId }: { branchId: string }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const items = useQuery({ queryKey: ["menu", branchId], queryFn: ({ signal }) => api.menu.list(branchId, signal) });

  const toggle = useMutation({
    mutationFn: ({ id, isAvailable }: { id: string; isAvailable: boolean }) => api.menu.setAvailability(id, isAvailable),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["menu", branchId] }),
  });

  if (items.isPending) return <Spinner label="Loading menu" />;
  if (items.error) return <ErrorAlert error={items.error} title="Could not load the menu" />;
  if (items.data.length === 0) return <EmptyState title="No menu items yet">Create the first item below.</EmptyState>;

  return (
    <Card>
      <ErrorAlert error={toggle.error} title="Availability not changed" />
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Item</th>
              <th>Category</th>
              <th>Tags</th>
              <th className="num">Price</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {items.data.map((item) => (
              <tr key={item.id}>
                <td>
                  <strong>{item.name}</strong>
                  {item.description && <div className="small muted">{item.description}</div>}
                </td>
                <td>{item.category}</td>
                <td>
                  <div className="row">
                    {item.dietaryTags.map((t) => (
                      <Badge key={t}>{t}</Badge>
                    ))}
                  </div>
                </td>
                <td className="num">{formatMoney(item.price, item.currency)}</td>
                <td>{item.isAvailable ? <Badge tone="success">Available</Badge> : <Badge tone="danger">86'd</Badge>}</td>
                <td>
                  <Button
                    size="sm"
                    busy={toggle.isPending && toggle.variables?.id === item.id}
                    onClick={() => toggle.mutate({ id: item.id, isAvailable: !item.isAvailable })}
                  >
                    {item.isAvailable ? "Mark unavailable" : "Mark available"}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

const emptyItem = (currency: string): CreateMenuItemRequest => ({
  category: "",
  name: "",
  description: "",
  price: 0,
  currency,
  dietaryTags: [],
  ingredients: [],
});

function splitList(value: string): string[] {
  return value.split(",").map((v) => v.trim()).filter(Boolean);
}

function NewItemForm({ branchId, currency }: { branchId: string; currency: string }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const [item, setItem] = useState(() => emptyItem(currency));
  const [tags, setTags] = useState("");
  const [ingredients, setIngredients] = useState("");

  const create = useMutation({
    mutationFn: () => api.menu.create(branchId, { ...item, dietaryTags: splitList(tags), ingredients: splitList(ingredients) }),
    onSuccess: () => {
      setItem(emptyItem(currency));
      setTags("");
      setIngredients("");
      return queryClient.invalidateQueries({ queryKey: ["menu", branchId] });
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    create.mutate();
  }

  return (
    <Card>
      <h2>New menu item</h2>
      <form className="stack" onSubmit={submit}>
        <div className="form-grid">
          <Field label="Name">
            <input value={item.name} onChange={(e) => setItem({ ...item, name: e.target.value })} required maxLength={200} />
          </Field>
          <Field label="Category">
            <input value={item.category} onChange={(e) => setItem({ ...item, category: e.target.value })} placeholder="Mains" maxLength={100} />
          </Field>
          <Field label={`Price (${item.currency})`}>
            <input
              type="number"
              min={0}
              step="0.01"
              inputMode="decimal"
              value={item.price}
              onChange={(e) => setItem({ ...item, price: Number(e.target.value) })}
              required
            />
          </Field>
        </div>
        <Field label="Description">
          <textarea value={item.description} onChange={(e) => setItem({ ...item, description: e.target.value })} maxLength={2000} />
        </Field>
        <div className="form-grid">
          <Field label="Dietary tags (comma separated)">
            <input value={tags} onChange={(e) => setTags(e.target.value)} placeholder="vegan, spicy" />
          </Field>
          <Field label="Ingredients (comma separated)">
            <input value={ingredients} onChange={(e) => setIngredients(e.target.value)} />
          </Field>
        </div>
        <div className="row">
          <Button type="submit" variant="primary" busy={create.isPending}>
            Add item
          </Button>
        </div>
        <ErrorAlert error={create.error} title="Item not created" />
      </form>
    </Card>
  );
}
