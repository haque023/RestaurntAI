import type { CreateMenuItemRequest, MenuItem } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Badge, Button, Card, EmptyState, ErrorAlert, Field, formatMoney, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { BranchPicker } from "../../shared/BranchPicker";
import { useBranch } from "../../shared/useBranch";

export function MenuPage() {
  const { summary, branches, branchId } = useBranch();
  const currency = summary.data?.defaultCurrency ?? "USD";

  if (summary.isPending) return <Spinner />;
  if (summary.error) return <ErrorAlert error={summary.error} />;
  if (branches.length === 0) return <EmptyState title="No branches yet">Add a branch on the Restaurant page first.</EmptyState>;

  return (
    <>
      <PageHeader title="Menu" description="Prices and availability here are exactly what guests and the AI waiter see, instantly." actions={<BranchPicker />} />
      {branchId && <MenuTable branchId={branchId} />}
      {branchId && (
        <Card>
          <h2>New menu item</h2>
          <ItemForm key={branchId} branchId={branchId} initial={emptyItem(currency)} />
        </Card>
      )}
    </>
  );
}

function MenuTable({ branchId }: { branchId: string }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<string | null>(null);
  const items = useQuery({ queryKey: ["menu", branchId], queryFn: ({ signal }) => api.menu.list(branchId, signal) });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ["menu", branchId] });

  const toggle = useMutation({
    mutationFn: ({ id, isAvailable }: { id: string; isAvailable: boolean }) => api.menu.setAvailability(id, isAvailable),
    onSuccess: refresh,
  });
  const remove = useMutation({ mutationFn: (id: string) => api.menu.remove(id), onSuccess: refresh });

  if (items.isPending) return <Spinner label="Loading menu" />;
  if (items.error) return <ErrorAlert error={items.error} title="Could not load the menu" />;
  if (items.data.length === 0) return <EmptyState title="No menu items yet">Create the first item below.</EmptyState>;

  return (
    <Card>
      <ErrorAlert error={toggle.error ?? remove.error} title="Menu not changed" />
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
            {items.data.map((item) =>
              editing === item.id ? (
                <tr key={item.id}>
                  <td colSpan={6}>
                    <ItemForm branchId={branchId} itemId={item.id} initial={toRequest(item)} onDone={() => setEditing(null)} />
                  </td>
                </tr>
              ) : (
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
                  <td>{item.isAvailable ? <Badge tone="success">Available</Badge> : <Badge tone="danger">Sold out</Badge>}</td>
                  <td>
                    <div className="row">
                      <Button
                        size="sm"
                        busy={toggle.isPending && toggle.variables?.id === item.id}
                        onClick={() => toggle.mutate({ id: item.id, isAvailable: !item.isAvailable })}
                      >
                        {item.isAvailable ? "Sold out" : "Available"}
                      </Button>
                      <Button size="sm" onClick={() => setEditing(item.id)}>
                        Edit
                      </Button>
                      <Button size="sm" variant="danger" onClick={() => window.confirm(`Delete ${item.name}?`) && remove.mutate(item.id)}>
                        Delete
                      </Button>
                    </div>
                  </td>
                </tr>
              ),
            )}
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

const toRequest = (item: MenuItem): CreateMenuItemRequest => ({
  category: item.category,
  name: item.name,
  description: item.description,
  price: item.price,
  currency: item.currency,
  dietaryTags: item.dietaryTags,
  ingredients: item.ingredients,
});

function splitList(value: string): string[] {
  return value
    .split(",")
    .map((v) => v.trim())
    .filter(Boolean);
}

/** Create (no itemId) or edit an item. */
function ItemForm({ branchId, itemId, initial, onDone }: { branchId: string; itemId?: string; initial: CreateMenuItemRequest; onDone?: () => void }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const [item, setItem] = useState(initial);
  const [tags, setTags] = useState(initial.dietaryTags.join(", "));
  const [ingredients, setIngredients] = useState(initial.ingredients.join(", "));

  const save = useMutation({
    mutationFn: () => {
      const request = { ...item, dietaryTags: splitList(tags), ingredients: splitList(ingredients) };
      return itemId ? api.menu.update(itemId, request) : api.menu.create(branchId, request);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["menu", branchId] });
      if (itemId) {
        onDone?.();
      } else {
        setItem(emptyItem(item.currency));
        setTags("");
        setIngredients("");
      }
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    save.mutate();
  }

  return (
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
          <input value={tags} onChange={(e) => setTags(e.target.value)} placeholder="vegan, spicy, halal" />
        </Field>
        <Field label="Ingredients (comma separated)">
          <input value={ingredients} onChange={(e) => setIngredients(e.target.value)} placeholder="rice, beef, green chilli" />
        </Field>
      </div>
      <div className="row">
        <Button type="submit" variant="primary" busy={save.isPending}>
          {itemId ? "Save changes" : "Add item"}
        </Button>
        {onDone && <Button onClick={onDone}>Cancel</Button>}
      </div>
      <ErrorAlert error={save.error} title={itemId ? "Item not saved" : "Item not created"} />
    </form>
  );
}
