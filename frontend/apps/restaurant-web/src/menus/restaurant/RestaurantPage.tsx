import type { Restaurant } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Badge, Button, Card, EmptyState, ErrorAlert, Field, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link } from "react-router";
import { BranchEditor } from "./BranchEditor";
import { useRestaurant } from "../../shared/useRestaurant";

const CONSUMER_URL = import.meta.env.VITE_CONSUMER_URL ?? "http://localhost:3000";

const splitList = (value: string) => value.split(",").map((v) => v.trim()).filter(Boolean);

export function RestaurantPage() {
  const restaurant = useRestaurant();
  const [editingBranch, setEditingBranch] = useState<string | null>(null);

  if (restaurant.isPending) return <Spinner label="Loading restaurant" />;
  if (restaurant.error) return <ErrorAlert error={restaurant.error} title="Could not load the restaurant" />;
  if (!restaurant.data) {
    return (
      <EmptyState title="Restaurant not registered">
        <p>This account's restaurant could not be found. Contact platform support.</p>
      </EmptyState>
    );
  }

  const r = restaurant.data;
  const editing = r.branches.find((b) => b.id === editingBranch);

  return (
    <>
      <PageHeader
        title={r.name}
        description={`${r.defaultCurrency} · ${r.timeZone}`}
        actions={r.isListed ? <Badge tone="success">Listed in discovery</Badge> : <Badge tone="warning">Not listed</Badge>}
      />

      <div className="grid">
        <ListingCard restaurant={r} />
        <ProfileForm key={r.id} restaurant={r} />
        <PlanCard restaurant={r} />
      </div>

      <Card>
        <div className="row row-between">
          <h2>Branches</h2>
          <span className="small muted mono">tenant {r.id}</span>
        </div>
        {r.branches.length === 0 ? (
          <p className="muted">Add your first branch to start building the menu.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Address</th>
                  <th>Location</th>
                  <th>Hours</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {r.branches.map((b) => (
                  <tr key={b.id}>
                    <td>
                      {b.name}
                      <div className="mono small muted">{b.id}</div>
                    </td>
                    <td>{b.address}</td>
                    <td>{b.latitude !== null ? <Badge tone="success">Set</Badge> : <Badge tone="warning">Missing</Badge>}</td>
                    <td>{b.openingHours.length > 0 ? `${new Set(b.openingHours.map((p) => p.day)).size} days` : <Badge tone="warning">Not set</Badge>}</td>
                    <td>
                      <div className="row">
                        <Button size="sm" onClick={() => setEditingBranch(b.id)}>
                          Edit
                        </Button>
                        <Link className="btn btn-sm" to={`/menu?branch=${b.id}`}>
                          Menu
                        </Link>
                        <a className="btn btn-sm btn-ghost" href={`${CONSUMER_URL}/r/${r.id}/${b.id}`} target="_blank" rel="noreferrer">
                          Guest view ↗
                        </a>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {editing && <BranchEditor key={editing.id} branch={editing} timeZone={r.timeZone} onDone={() => setEditingBranch(null)} />}
      <AddBranchForm />
    </>
  );
}

function ListingCard({ restaurant }: { restaurant: Restaurant }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const setListing = useMutation({
    mutationFn: (listed: boolean) => api.restaurants.setListing(listed),
    onSuccess: (updated) => queryClient.setQueryData(["restaurant", "current"], updated),
  });

  const missing = [
    restaurant.branches.length === 0 && "at least one branch",
    !restaurant.branches.some((b) => b.latitude !== null) && "a branch location (for Near me)",
    !restaurant.branches.some((b) => b.openingHours.length > 0) && "opening hours (for Open now)",
    restaurant.cuisines.length === 0 && "cuisines",
  ].filter(Boolean);

  return (
    <Card>
      <h2>Discovery listing</h2>
      <p className="small muted">
        {restaurant.isListed
          ? "Guests can find this restaurant in the consumer app and save it to their restaurants."
          : "Your restaurant is hidden from discovery. Guests can still open it from a QR code."}
      </p>
      {missing.length > 0 && <p className="small">Recommended before listing: {missing.join(", ")}.</p>}
      {restaurant.status === "PendingVerification" && <p className="small">You can list once the platform team has verified the restaurant.</p>}
      <div>
        <Button
          variant={restaurant.isListed ? "default" : "primary"}
          busy={setListing.isPending}
          disabled={!restaurant.isListed && (restaurant.branches.length === 0 || restaurant.status === "PendingVerification" || restaurant.status === "Suspended")}
          onClick={() => setListing.mutate(!restaurant.isListed)}
        >
          {restaurant.isListed ? "Unlist" : "List in discovery"}
        </Button>
      </div>
      <ErrorAlert error={setListing.error} title="Listing not changed" />
    </Card>
  );
}

function ProfileForm({ restaurant }: { restaurant: Restaurant }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const [description, setDescription] = useState(restaurant.description);
  const [cuisines, setCuisines] = useState(restaurant.cuisines.join(", "));
  const [diets, setDiets] = useState(restaurant.dietaryOptions.join(", "));
  const [phone, setPhone] = useState(restaurant.phone ?? "");
  const [website, setWebsite] = useState(restaurant.website ?? "");

  const save = useMutation({
    mutationFn: () =>
      api.restaurants.updateProfile({ description, cuisines: splitList(cuisines), dietaryOptions: splitList(diets), phone: phone || null, website: website || null }),
    onSuccess: (updated) => queryClient.setQueryData(["restaurant", "current"], updated),
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    save.mutate();
  }

  return (
    <Card>
      <h2>Discovery profile</h2>
      <form className="stack" onSubmit={submit}>
        <Field label="Description">
          <textarea value={description} onChange={(e) => setDescription(e.target.value)} maxLength={2000} placeholder="Family-run Bengali kitchen…" />
        </Field>
        <Field label="Cuisines (comma separated)">
          <input value={cuisines} onChange={(e) => setCuisines(e.target.value)} placeholder="bengali, indian, biryani" />
        </Field>
        <Field label="Dietary options (comma separated)">
          <input value={diets} onChange={(e) => setDiets(e.target.value)} placeholder="halal, vegetarian, vegan" />
        </Field>
        <div className="form-grid">
          <Field label="Phone">
            <input type="tel" value={phone} onChange={(e) => setPhone(e.target.value)} maxLength={40} />
          </Field>
          <Field label="Website">
            <input value={website} onChange={(e) => setWebsite(e.target.value)} maxLength={300} placeholder="https://" />
          </Field>
        </div>
        <div className="row">
          <Button type="submit" variant="primary" busy={save.isPending}>
            Save profile
          </Button>
          {save.isSuccess && <span className="small muted">Saved.</span>}
        </div>
        <ErrorAlert error={save.error} title="Profile not saved" />
      </form>
    </Card>
  );
}

function PlanCard({ restaurant }: { restaurant: Restaurant }) {
  const plan = restaurant.plan;
  return (
    <Card>
      <h2>Plan: {plan.name}</h2>
      <ul className="small" style={{ margin: 0, paddingLeft: "1.1rem" }}>
        <li>
          {restaurant.branches.length} of {plan.maxBranches} branch{plan.maxBranches === 1 ? "" : "es"}
        </li>
        <li>Up to {plan.maxTablesPerBranch} tables per branch</li>
        <li>AI waiter: {plan.aiWaiter ? `${plan.aiTurnsPerMonth.toLocaleString()} guest messages a month` : "not included"}</li>
      </ul>
      <p className="small muted">{plan.monthlyPrice === 0 ? "Free" : `${plan.currency} ${plan.monthlyPrice}/month`}. To change plan, contact the platform team.</p>
    </Card>
  );
}

function AddBranchForm() {
  const api = useApi();
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [address, setAddress] = useState("");

  const add = useMutation({
    mutationFn: () => api.restaurants.addBranch({ name: name.trim(), address: address.trim() }),
    onSuccess: () => {
      setName("");
      setAddress("");
      return queryClient.invalidateQueries({ queryKey: ["restaurant"] });
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    add.mutate();
  }

  return (
    <Card narrow>
      <h2>Add branch</h2>
      <form className="stack" onSubmit={submit}>
        <Field label="Name">
          <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
        </Field>
        <Field label="Address">
          <input value={address} onChange={(e) => setAddress(e.target.value)} maxLength={500} />
        </Field>
        <Button type="submit" variant="primary" busy={add.isPending}>
          Add branch
        </Button>
        <ErrorAlert error={add.error} title="Branch not added" />
      </form>
    </Card>
  );
}
