import type { AiSettings } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Badge, Button, Card, ErrorAlert, Field, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";

const EXAMPLE = `Payment: cash, bKash and cards (cards from 200 BDT).
Parking: free parking behind the building.
Kids: high chairs available; kids' portions on request.
Allergies: we cook with peanuts and sesame; staff confirm with the kitchen.
Delivery: not offered. Takeaway packing is free.
Events: private room for up to 20 guests, book 2 days ahead.`;

/**
 * What the AI waiter knows about the restaurant beyond the live menu: policies, FAQs and tone.
 * Prices and availability always come from the menu, never from this text.
 */
export function AiPage() {
  const api = useApi();
  const settings = useQuery({ queryKey: ["ai-settings"], queryFn: () => api.restaurants.aiSettings() });
  const usage = useQuery({ queryKey: ["ai-usage"], queryFn: ({ signal }) => api.ai.usage(signal) });

  return (
    <>
      <PageHeader
        title="AI waiter"
        description="Guests reach it from the table QR code and from your menu page. It answers from your live menu and this knowledge, takes orders after the guest confirms, books tables and calls staff when needed."
        actions={settings.data && (settings.data.enabled ? <Badge tone="success">On</Badge> : <Badge tone="danger">Turned off by the platform</Badge>)}
      />
      <ErrorAlert error={settings.error} title="Could not load AI settings" />
      {settings.isPending && <Spinner />}
      {settings.data && <KnowledgeForm key={settings.dataUpdatedAt} settings={settings.data} />}
      {usage.data && (
        <Card>
          <h2>This month</h2>
          <p>
            <strong>{usage.data.usedTurns}</strong>
            {usage.data.limit !== null && <> of {usage.data.limit}</>} guest messages answered · {usage.data.summary.toolCalls} menu, order and booking actions ·{" "}
            {usage.data.summary.failed} failed
          </p>
        </Card>
      )}
    </>
  );
}

function KnowledgeForm({ settings }: { settings: AiSettings }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const [knowledge, setKnowledge] = useState(settings.knowledge);
  const [tone, setTone] = useState(settings.tone);

  const save = useMutation({
    mutationFn: () => api.restaurants.updateAiSettings(knowledge, tone),
    onSuccess: (updated) => queryClient.setQueryData(["ai-settings"], updated),
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    save.mutate();
  }

  return (
    <Card>
      <h2>What the AI waiter should know</h2>
      <form className="stack" onSubmit={submit}>
        <Field label={`Policies, FAQs and services (${knowledge.length}/${settings.maxKnowledgeLength})`}>
          <textarea
            value={knowledge}
            onChange={(e) => setKnowledge(e.target.value)}
            maxLength={settings.maxKnowledgeLength}
            rows={10}
            placeholder={EXAMPLE}
          />
          <small className="muted">Write it like you would brief a new waiter. Don't put prices here; the AI always reads them live from the menu.</small>
        </Field>
        <Field label="Tone">
          <input value={tone} onChange={(e) => setTone(e.target.value)} maxLength={200} placeholder="Warm and brief; greet with Assalamu alaikum" />
        </Field>
        <div className="row">
          <Button type="submit" variant="primary" busy={save.isPending}>
            Save
          </Button>
          {save.isSuccess && <span className="small muted">Saved. The AI waiter uses it within a minute.</span>}
        </div>
        <ErrorAlert error={save.error} title="Not saved" />
      </form>
    </Card>
  );
}
