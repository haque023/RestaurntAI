import { ApiError, type CustomerProfile } from "@restaurant-ai/api-client";
import { useApi, useAuth } from "@restaurant-ai/auth";
import { Button, Card, ErrorAlert, Field, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";

export function AccountPage() {
  const api = useApi();
  const { user } = useAuth();

  const profile = useQuery({
    queryKey: ["profile"],
    // A new customer has no profile yet (404): show an empty form.
    queryFn: () => api.consumer.profile().catch((e: unknown) => (e instanceof ApiError && e.status === 404 ? null : Promise.reject(e))),
  });

  const loyalty = useQuery({ queryKey: ["loyalty", user?.tenantId], queryFn: () => api.loyalty.balance(), enabled: Boolean(user?.tenantId) });

  return (
    <>
      <PageHeader title="Your account" />
      <div className="grid">
        <Card>
          <h2>Loyalty</h2>
          {!user?.tenantId && <p className="muted small">Open a restaurant's menu to see your points there.</p>}
          {user?.tenantId && loyalty.isPending && <Spinner />}
          <ErrorAlert error={loyalty.error} title="Could not load points" />
          {loyalty.data && (
            <p>
              <strong style={{ fontSize: "2rem" }}>{loyalty.data.balance}</strong> <span className="muted">points at this restaurant</span>
            </p>
          )}
        </Card>
        <Card>
          <h2>Profile</h2>
          {profile.isPending ? <Spinner /> : profile.error ? <ErrorAlert error={profile.error} /> : <ProfileForm profile={profile.data ?? null} />}
        </Card>
      </div>
    </>
  );
}

function ProfileForm({ profile }: { profile: CustomerProfile | null }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const [displayName, setDisplayName] = useState(profile?.displayName ?? "");
  const [language, setLanguage] = useState(profile?.preferredLanguage ?? "");
  const [dietary, setDietary] = useState((profile?.dietaryPreferences ?? []).join(", "));
  const [consent, setConsent] = useState(profile?.marketingConsent ?? false);

  const save = useMutation({
    mutationFn: () =>
      api.consumer.updateProfile({
        displayName: displayName.trim(),
        preferredLanguage: language.trim() || null,
        dietaryPreferences: dietary.split(",").map((d) => d.trim()).filter(Boolean),
        marketingConsent: consent,
      }),
    onSuccess: (updated) => queryClient.setQueryData(["profile"], updated),
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    save.mutate();
  }

  return (
    <form className="stack" onSubmit={submit}>
      <Field label="Name">
        <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} required maxLength={200} />
      </Field>
      <Field label="Preferred language">
        <input value={language} onChange={(e) => setLanguage(e.target.value)} placeholder="en" maxLength={16} />
      </Field>
      <Field label="Dietary preferences (comma separated)">
        <input value={dietary} onChange={(e) => setDietary(e.target.value)} placeholder="vegetarian, nut-free" />
      </Field>
      <label className="row small">
        <input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} />
        Send me offers and news from restaurants I visit
      </label>
      <Button type="submit" variant="primary" busy={save.isPending}>
        Save
      </Button>
      {save.isSuccess && <p className="alert alert-success">Saved.</p>}
      <ErrorAlert error={save.error} title="Not saved" />
    </form>
  );
}
