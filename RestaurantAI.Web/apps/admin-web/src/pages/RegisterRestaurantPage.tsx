import type { Restaurant } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Button, Card, ErrorAlert, Field, PageHeader } from "@restaurant-ai/ui";
import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";

const CONSOLE_URL = import.meta.env.VITE_RESTAURANT_CONSOLE_URL ?? "http://localhost:3001";
const CURRENCIES = ["USD", "EUR", "GBP", "AED", "SAR", "INR", "BDT"];

export function RegisterRestaurantPage() {
  const api = useApi();
  const [name, setName] = useState("");
  const [currency, setCurrency] = useState("USD");
  const [timeZone, setTimeZone] = useState(() => Intl.DateTimeFormat().resolvedOptions().timeZone);

  const register = useMutation({ mutationFn: () => api.restaurants.register({ name: name.trim(), defaultCurrency: currency, timeZone }) });

  function submit(event: FormEvent) {
    event.preventDefault();
    register.mutate();
  }

  if (register.data) return <Registered restaurant={register.data} onAnother={() => register.reset()} />;

  return (
    <>
      <PageHeader title="Register restaurant" description="Creates a new tenant. Its id scopes all of the restaurant's data." />
      <Card narrow>
        <form className="stack" onSubmit={submit}>
          <Field label="Restaurant name">
            <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
          </Field>
          <div className="form-grid">
            <Field label="Default currency">
              <select value={currency} onChange={(e) => setCurrency(e.target.value)}>
                {CURRENCIES.map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </select>
            </Field>
            <Field label="Time zone">
              <input value={timeZone} onChange={(e) => setTimeZone(e.target.value)} required maxLength={64} />
            </Field>
          </div>
          <Button type="submit" variant="primary" busy={register.isPending}>
            Register
          </Button>
          <ErrorAlert error={register.error} title="Restaurant not registered" />
        </form>
      </Card>
    </>
  );
}

function Registered({ restaurant, onAnother }: { restaurant: Restaurant; onAnother: () => void }) {
  return (
    <>
      <PageHeader title="Restaurant registered" description={restaurant.name} />
      <Card narrow>
        <p className="alert alert-success">The tenant is ready. Share its id with the restaurant owner.</p>
        <Field label="Tenant id">
          <input readOnly value={restaurant.id} onFocus={(e) => e.target.select()} className="mono" />
        </Field>
        <div className="row">
          <a className="btn btn-primary" href={`${CONSOLE_URL}/login?tenant=${restaurant.id}`} target="_blank" rel="noreferrer">
            Open restaurant console ↗
          </a>
          <Button onClick={onAnother}>Register another</Button>
        </div>
      </Card>
    </>
  );
}
