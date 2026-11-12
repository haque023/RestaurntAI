import { useApi } from "@restaurant-ai/auth";
import { Card, EmptyState, ErrorAlert, Field, formatDateTime, PageHeader, Spinner } from "@restaurant-ai/ui";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useSearchParams } from "react-router";

const MODULES = ["", "identity", "restaurant", "menu", "order", "reservation", "consumer", "loyalty", "mcp"];

/** Every platform event (domain events from all services and AI tool executions), newest first. */
export function AuditPage() {
  const api = useApi();
  const [params, setParams] = useSearchParams();
  const type = params.get("type") ?? "";
  const module = params.get("module") ?? "";
  const tenantId = params.get("tenant") ?? "";
  const [open, setOpen] = useState<string | null>(null);

  const events = useQuery({
    queryKey: ["admin", "audit", type, module, tenantId],
    queryFn: ({ signal }) => api.admin.audit({ type: type || undefined, module: module || undefined, tenantId: tenantId || undefined, take: 200 }, signal),
    placeholderData: keepPreviousData,
    refetchInterval: 15_000,
  });

  const set = (key: string, value: string) =>
    setParams((current) => {
      const next = new URLSearchParams(current);
      if (value) next.set(key, value);
      else next.delete(key);
      return next;
    });

  return (
    <>
      <PageHeader title="Audit log" description="Who did what, when, in which restaurant. Built from the event stream; nothing here can be edited." />
      <Card>
        <div className="form-grid">
          <Field label="Module">
            <select value={module} onChange={(e) => set("module", e.target.value)}>
              {MODULES.map((m) => (
                <option key={m} value={m}>
                  {m || "All"}
                </option>
              ))}
            </select>
          </Field>
          <Field label="Event type">
            <input value={type} onChange={(e) => set("type", e.target.value)} placeholder="order.OrderCreated" />
          </Field>
          <Field label="Restaurant (tenant id)">
            <input value={tenantId} onChange={(e) => set("tenant", e.target.value.trim())} />
          </Field>
        </div>
      </Card>
      <ErrorAlert error={events.error} title="Could not load the audit log" />
      {events.isPending && <Spinner />}
      {events.data?.length === 0 && <EmptyState title="No events match" />}
      {events.data && events.data.length > 0 && (
        <Card>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>When</th>
                  <th>Event</th>
                  <th>Restaurant</th>
                  <th>Actor</th>
                </tr>
              </thead>
              <tbody>
                {events.data.map((e) => (
                  <tr key={e.id} onClick={() => setOpen(open === e.id ? null : e.id)} style={{ cursor: "pointer" }}>
                    <td className="small">{formatDateTime(e.occurredAt)}</td>
                    <td>
                      <span className="mono">{e.type}</span>
                      {e.type === "mcp.ToolExecuted" && (
                        <span className="small">
                          {" "}
                          {String(e.payload.tool)} · {String(e.payload.outcome)}
                        </span>
                      )}
                      {open === e.id && <pre className="small mono" style={{ whiteSpace: "pre-wrap", margin: "8px 0 0" }}>{JSON.stringify(e.payload, null, 2)}</pre>}
                    </td>
                    <td className="mono small">{e.tenantId?.slice(0, 8) ?? "platform"}</td>
                    <td className="mono small">{e.actorId?.slice(0, 8) ?? "system"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}
    </>
  );
}
