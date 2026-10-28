import { newIdempotencyKey } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Badge, Button, Card, ErrorAlert, Field, formatDateTime, formatMoney, PageHeader } from "@restaurant-ai/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { useSearchParams } from "react-router";

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function LookupForm({ label, onLookup, initial }: { label: string; onLookup: (id: string) => void; initial: string }) {
  const [id, setId] = useState(initial);
  const valid = GUID.test(id.trim());

  function submit(event: FormEvent) {
    event.preventDefault();
    if (valid) onLookup(id.trim());
  }

  return (
    <form className="row" onSubmit={submit} style={{ alignItems: "flex-end" }}>
      <div style={{ flex: 1, minWidth: 260 }}>
        <Field label={label}>
          <input value={id} onChange={(e) => setId(e.target.value)} placeholder="00000000-0000-0000-0000-000000000000" />
        </Field>
      </div>
      <Button type="submit" disabled={!valid}>
        Look up
      </Button>
    </form>
  );
}

// The order queue (list by branch/status) needs a list endpoint in Order.Api; lookup by id for now.
export function OrdersPage() {
  const api = useApi();
  const queryClient = useQueryClient();
  const [search, setSearch] = useSearchParams();
  const orderId = search.get("id") ?? "";

  const order = useQuery({ queryKey: ["order", orderId], queryFn: ({ signal }) => api.orders.get(orderId, signal), enabled: GUID.test(orderId) });
  const cancel = useMutation({
    mutationFn: () => api.orders.cancel(orderId, "Cancelled by restaurant", newIdempotencyKey()),
    onSuccess: (updated) => queryClient.setQueryData(["order", orderId], updated),
  });

  return (
    <>
      <PageHeader title="Orders" description="Live order queue requires an order list endpoint; look up an order by id." />
      <Card>
        <LookupForm label="Order id" initial={orderId} onLookup={(id) => setSearch({ id })} />
        <ErrorAlert error={order.error} title="Order not found" />
        {order.data && (
          <div className="stack">
            <div className="row row-between">
              <strong>{formatMoney(order.data.total, order.data.currency)}</strong>
              <Badge tone={order.data.status === "Cancelled" ? "danger" : "warning"}>{order.data.status}</Badge>
            </div>
            <p className="small muted">Placed {formatDateTime(order.data.placedAt)}</p>
            <ul>
              {order.data.lines.map((l) => (
                <li key={l.menuItemId}>
                  {l.quantity} × {l.name}
                  {l.notes && <span className="muted"> — {l.notes}</span>}
                </li>
              ))}
            </ul>
            {(order.data.status === "Placed" || order.data.status === "Accepted") && (
              <div>
                <Button variant="danger" busy={cancel.isPending} onClick={() => cancel.mutate()}>
                  Cancel order
                </Button>
              </div>
            )}
            <ErrorAlert error={cancel.error} title="Not cancelled" />
          </div>
        )}
      </Card>
    </>
  );
}

export function ReservationsPage() {
  const api = useApi();
  const queryClient = useQueryClient();
  const [search, setSearch] = useSearchParams();
  const reservationId = search.get("id") ?? "";

  const reservation = useQuery({
    queryKey: ["reservation", reservationId],
    queryFn: ({ signal }) => api.reservations.get(reservationId, signal),
    enabled: GUID.test(reservationId),
  });
  const cancel = useMutation({
    mutationFn: () => api.reservations.cancel(reservationId, "Cancelled by restaurant", newIdempotencyKey()),
    onSuccess: (updated) => queryClient.setQueryData(["reservation", reservationId], updated),
  });

  return (
    <>
      <PageHeader title="Reservations" description="The reservation book requires a list endpoint; look up a reservation by id." />
      <Card>
        <LookupForm label="Reservation id" initial={reservationId} onLookup={(id) => setSearch({ id })} />
        <ErrorAlert error={reservation.error} title="Reservation not found" />
        {reservation.data && (
          <div className="stack">
            <div className="row row-between">
              <strong>
                {formatDateTime(reservation.data.startsAt)} · {reservation.data.partySize} guests
              </strong>
              <Badge>{reservation.data.status}</Badge>
            </div>
            {reservation.data.specialRequests && <p className="muted">{reservation.data.specialRequests}</p>}
            {(reservation.data.status === "Pending" || reservation.data.status === "Confirmed") && (
              <div>
                <Button variant="danger" busy={cancel.isPending} onClick={() => cancel.mutate()}>
                  Cancel reservation
                </Button>
              </div>
            )}
            <ErrorAlert error={cancel.error} title="Not cancelled" />
          </div>
        )}
      </Card>
    </>
  );
}
