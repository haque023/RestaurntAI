import { LoginForm, useAuth } from "@restaurant-ai/auth";
import { Button, Card, Field, PageHeader } from "@restaurant-ai/ui";
import { useState, type FormEvent } from "react";
import { Link, useLocation, useNavigate } from "react-router";

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? "/";

  return (
    <>
      <PageHeader title="Restaurant console" description="Orders, kitchen, tables, menu, staff and your AI waiter." />
      <Card narrow>
        <LoginForm
          scope="Staff"
          onSignedIn={() => navigate(from, { replace: true })}
          footer={
            <p className="small muted">
              New to the platform? <Link to="/signup">Register your restaurant</Link>
            </p>
          }
        />
      </Card>
    </>
  );
}

const TIME_ZONES = ["Asia/Dhaka", "Asia/Kolkata", "Asia/Dubai", "Asia/Singapore", "Europe/London", "Europe/Berlin", "America/New_York", "America/Los_Angeles", "UTC"];
const CURRENCIES = ["BDT", "INR", "AED", "SGD", "GBP", "EUR", "USD"];

/** Self-service onboarding: owner account + restaurant awaiting verification by the platform team. */
export function SignupPage() {
  const { api, signIn } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({ restaurantName: "", displayName: "", email: "", password: "", currency: "BDT", timeZone: "Asia/Dhaka" });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const session = await api.auth.signupRestaurant({ ...form, email: form.email.trim(), restaurantName: form.restaurantName.trim() });
      signIn(session.accessToken);
      navigate("/restaurant", { replace: true });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Sign-up failed.");
    } finally {
      setBusy(false);
    }
  }

  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value });

  return (
    <>
      <PageHeader title="Register your restaurant" description="Start on the free Starter plan. The platform team verifies new restaurants before they appear to guests." />
      <Card narrow>
        <form className="stack" onSubmit={submit}>
          <Field label="Restaurant name">
            <input value={form.restaurantName} onChange={set("restaurantName")} required maxLength={200} />
          </Field>
          <div className="form-grid">
            <Field label="Currency">
              <select value={form.currency} onChange={set("currency")}>
                {CURRENCIES.map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </select>
            </Field>
            <Field label="Time zone">
              <select value={form.timeZone} onChange={set("timeZone")}>
                {TIME_ZONES.map((z) => (
                  <option key={z}>{z}</option>
                ))}
              </select>
            </Field>
          </div>
          <Field label="Your name">
            <input value={form.displayName} onChange={set("displayName")} autoComplete="name" />
          </Field>
          <Field label="Email">
            <input type="email" value={form.email} onChange={set("email")} required autoComplete="email" />
          </Field>
          <Field label="Password">
            <input type="password" value={form.password} onChange={set("password")} required minLength={8} autoComplete="new-password" />
          </Field>
          {error && (
            <p className="alert alert-error" role="alert">
              {error}
            </p>
          )}
          <Button type="submit" variant="primary" busy={busy}>
            Create restaurant
          </Button>
          <p className="small muted">
            Already registered? <Link to="/login">Sign in</Link>
          </p>
        </form>
      </Card>
    </>
  );
}
