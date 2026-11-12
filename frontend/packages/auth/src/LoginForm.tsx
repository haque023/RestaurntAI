import type { Guid, SessionResponse, SessionScope } from "@restaurant-ai/api-client";
import { useState, type FormEvent, type ReactNode } from "react";
import { useAuth } from "./context";

export interface LoginFormProps {
  /** Which app is signing in: customers, restaurant staff or platform administrators. */
  scope: SessionScope;
  /** Staff with several restaurants can pick one; defaults to their first. */
  tenantId?: Guid;
  onSignedIn: (session: SessionResponse) => void;
  submitLabel?: string;
  footer?: ReactNode;
}

/** Email and password sign-in against the Identity service. */
export function LoginForm({ scope, tenantId, onSignedIn, submitLabel = "Sign in", footer }: LoginFormProps) {
  const { api, signIn } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const session = await api.auth.login({ email: email.trim(), password, scope, tenantId });
      signIn(session.accessToken);
      onSignedIn(session);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Sign-in failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="stack" onSubmit={submit} aria-label="Sign in">
      <label className="field">
        <span>Email</span>
        <input type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
      </label>
      <label className="field">
        <span>Password</span>
        <input type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
      </label>
      {error && (
        <p className="alert alert-error" role="alert">
          {error}
        </p>
      )}
      <button className="btn btn-primary" type="submit" disabled={busy}>
        {busy ? "Signing in…" : submitLabel}
      </button>
      {footer}
    </form>
  );
}

export interface RegisterFormProps {
  onSignedIn: (session: SessionResponse) => void;
  footer?: ReactNode;
}

/** Customer account registration (signs the new customer in). */
export function RegisterForm({ onSignedIn, footer }: RegisterFormProps) {
  const { api, signIn } = useAuth();
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const session = await api.auth.register({ email: email.trim(), password, displayName: displayName.trim() || undefined });
      signIn(session.accessToken);
      onSignedIn(session);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Registration failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="stack" onSubmit={submit} aria-label="Create account">
      <label className="field">
        <span>Your name</span>
        <input autoComplete="name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
      </label>
      <label className="field">
        <span>Email</span>
        <input type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
      </label>
      <label className="field">
        <span>Password</span>
        <input type="password" autoComplete="new-password" minLength={8} value={password} onChange={(e) => setPassword(e.target.value)} required />
        <small className="muted">At least 8 characters, with a number or symbol.</small>
      </label>
      {error && (
        <p className="alert alert-error" role="alert">
          {error}
        </p>
      )}
      <button className="btn btn-primary" type="submit" disabled={busy}>
        {busy ? "Creating account…" : "Create account"}
      </button>
      {footer}
    </form>
  );
}
