import type { Restaurant } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Button, Card, ErrorAlert, Field, PageHeader } from "@restaurant-ai/ui";
import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { Link } from "react-router";

const CONSOLE_URL = import.meta.env.VITE_RESTAURANT_CONSOLE_URL ?? "http://localhost:3001";
const CURRENCIES = ["BDT", "INR", "AED", "SAR", "SGD", "GBP", "EUR", "USD"];

/** Assisted onboarding: the platform team creates a verified restaurant and its owner account. */
export function OnboardRestaurantPage() {
  const api = useApi();
  const [form, setForm] = useState({
    name: "",
    currency: "BDT",
    timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
    ownerName: "",
    ownerEmail: "",
    ownerPassword: "",
  });

  const onboard = useMutation({
    mutationFn: async (): Promise<Restaurant> => {
      const restaurant = await api.restaurants.register({ name: form.name.trim(), defaultCurrency: form.currency, timeZone: form.timeZone.trim() });
      await api.admin.createOwner({
        tenantId: restaurant.id,
        tenantName: restaurant.name,
        email: form.ownerEmail.trim(),
        displayName: form.ownerName.trim() || undefined,
        password: form.ownerPassword || undefined,
      });
      return restaurant;
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    onboard.mutate();
  }

  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value });

  if (onboard.data) {
    return (
      <>
        <PageHeader title="Restaurant onboarded" description={onboard.data.name} />
        <Card narrow>
          <p className="alert alert-success">The restaurant is verified and the owner can sign in to the restaurant console with {form.ownerEmail}.</p>
          <div className="row">
            <Link className="btn btn-primary" to={`/restaurants/${onboard.data.id}`}>
              View restaurant
            </Link>
            <a className="btn" href={`${CONSOLE_URL}/login`} target="_blank" rel="noreferrer">
              Restaurant console ↗
            </a>
          </div>
        </Card>
      </>
    );
  }

  return (
    <>
      <PageHeader title="Onboard a restaurant" description="Restaurants can also sign up themselves from the restaurant console; those wait for verification." />
      <Card narrow>
        <form className="stack" onSubmit={submit}>
          <Field label="Restaurant name">
            <input value={form.name} onChange={set("name")} required maxLength={200} />
          </Field>
          <div className="form-grid">
            <Field label="Currency">
              <select value={form.currency} onChange={set("currency")}>
                {CURRENCIES.map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </select>
            </Field>
            <Field label="Time zone (IANA)">
              <input value={form.timeZone} onChange={set("timeZone")} required maxLength={64} />
            </Field>
          </div>
          <h3>Owner</h3>
          <Field label="Name">
            <input value={form.ownerName} onChange={set("ownerName")} />
          </Field>
          <Field label="Email">
            <input type="email" value={form.ownerEmail} onChange={set("ownerEmail")} required />
          </Field>
          <Field label="Initial password (new accounts)">
            <input value={form.ownerPassword} onChange={set("ownerPassword")} minLength={8} autoComplete="off" />
            <small className="muted">Leave empty if the owner already has an account on the platform.</small>
          </Field>
          <Button type="submit" variant="primary" busy={onboard.isPending}>
            Create restaurant and owner
          </Button>
          <ErrorAlert error={onboard.error} title="Not onboarded" />
        </form>
      </Card>
    </>
  );
}
