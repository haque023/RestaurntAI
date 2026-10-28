import { newIdempotencyKey } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Badge, Button, Card, ErrorAlert, formatDateTime, PageHeader, shortId, Spinner } from "@restaurant-ai/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useParams } from "react-router";
import { reservationTone } from "./status";

export function ReservationPage() {
  const { reservationId = "" } = useParams();
  const api = useApi();
  const queryClient = useQueryClient();
  const [cancelKey] = useState(newIdempotencyKey);

  const reservation = useQuery({
    queryKey: ["reservation", reservationId],
    queryFn: ({ signal }) => api.reservations.get(reservationId, signal),
  });

  const cancel = useMutation({
    mutationFn: () => api.reservations.cancel(reservationId, "Cancelled by guest", cancelKey),
    onSuccess: (updated) => queryClient.setQueryData(["reservation", reservationId], updated),
  });

  if (reservation.isPending) return <Spinner label="Loading reservation" />;
  if (reservation.error) return <ErrorAlert error={reservation.error} title="Could not load the reservation" />;

  const r = reservation.data;
  const cancellable = (r.status === "Pending" || r.status === "Confirmed") && new Date(r.startsAt) > new Date();

  return (
    <>
      <PageHeader title={`Reservation ${shortId(r.id)}`} actions={<Badge tone={reservationTone(r.status)}>{r.status}</Badge>} />
      <Card narrow>
        <dl className="stack" style={{ margin: 0 }}>
          <div>
            <dt className="small muted">When</dt>
            <dd style={{ margin: 0 }}>
              {formatDateTime(r.startsAt)} – {new Date(r.endsAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
            </dd>
          </div>
          <div>
            <dt className="small muted">Guests</dt>
            <dd style={{ margin: 0 }}>{r.partySize}</dd>
          </div>
          {r.specialRequests && (
            <div>
              <dt className="small muted">Special requests</dt>
              <dd style={{ margin: 0 }}>{r.specialRequests}</dd>
            </div>
          )}
        </dl>
        {cancellable && (
          <Button variant="danger" busy={cancel.isPending} onClick={() => cancel.mutate()}>
            Cancel reservation
          </Button>
        )}
        <ErrorAlert error={cancel.error} title="The reservation was not cancelled" />
      </Card>
    </>
  );
}
