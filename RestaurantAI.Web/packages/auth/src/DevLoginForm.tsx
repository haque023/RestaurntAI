import { useState, type FormEvent } from "react";
import { useAuth } from "./context";
import type { SessionUser } from "./claims";

export interface DevPersona {
  label: string;
  roles: string[];
  permissions: string[];
  /** Ask for a tenant id (restaurant). */
  needsTenant: boolean;
  /** Offer an optional branch binding. */
  allowsBranch?: boolean;
}

export interface DevLoginFormProps {
  persona: DevPersona;
  defaultTenantId?: string;
  defaultBranchId?: string;
  /** Keep the same development user across sign-ins (so favorites and orders stay theirs). */
  userId?: string;
  onSignedIn: (user: SessionUser) => void;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Local development sign-in using the Identity service's dev-token endpoint (Development only).
 * Replace with the identity provider's hosted login in deployed environments.
 */
export function DevLoginForm({ persona, defaultTenantId = "", defaultBranchId = "", userId, onSignedIn }: DevLoginFormProps) {
  const { api, signIn } = useAuth();
  const [tenantId, setTenantId] = useState(defaultTenantId);
  const [branchId, setBranchId] = useState(defaultBranchId);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);

    if (persona.needsTenant && !GUID.test(tenantId.trim())) {
      setError("Enter the restaurant (tenant) id as a GUID.");
      return;
    }
    if (branchId.trim() && !GUID.test(branchId.trim())) {
      setError("Branch id must be a GUID.");
      return;
    }

    setBusy(true);
    try {
      const { access_token } = await api.auth.devToken({
        userId,
        tenantId: persona.needsTenant ? tenantId.trim() : undefined,
        branchId: branchId.trim() || undefined,
        roles: persona.roles,
        permissions: persona.permissions,
        lifetimeMinutes: 480,
      });
      onSignedIn(signIn(access_token));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Sign-in failed. Is the gateway and Identity service running?");
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="stack" onSubmit={submit} aria-label="Development sign-in">
      <p className="muted">
        Development sign-in as <strong>{persona.label}</strong>. Tokens come from <code>/api/auth/dev-token</code>.
      </p>
      {persona.needsTenant && (
        <label className="field">
          <span>Restaurant (tenant) id</span>
          <input value={tenantId} onChange={(e) => setTenantId(e.target.value)} placeholder="00000000-0000-0000-0000-000000000000" required />
        </label>
      )}
      {persona.allowsBranch && (
        <label className="field">
          <span>Branch id (optional, restricts to one branch)</span>
          <input value={branchId} onChange={(e) => setBranchId(e.target.value)} />
        </label>
      )}
      {error && (
        <p className="alert alert-error" role="alert">
          {error}
        </p>
      )}
      <button className="btn btn-primary" type="submit" disabled={busy}>
        {busy ? "Signing in…" : "Sign in"}
      </button>
    </form>
  );
}
