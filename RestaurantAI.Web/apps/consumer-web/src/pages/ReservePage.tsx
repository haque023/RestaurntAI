import { newIdempotencyKey, type CreateReservationRequest } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Button, Card, ErrorAlert, Field, PageHeader } from "@restaurant-ai/ui";
import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { useNavigate, useParams } from "react-router";

function defaultStart(): string {
  const date = new Date(Date.now() + 24 * 60 * 60 * 1000);
  date.setHours(19, 0, 0, 0);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function ReservePage() {
  const { branchId = "" } = useParams();
  const api = useApi();
  const navigate = useNavigate();

  const [partySize, setPartySize] = useState(2);
  const [startsAt, setStartsAt] = useState(defaultStart);
  const [duration, setDuration] = useState(90);
  const [specialRequests, setSpecialRequests] = useState("");
  // One key per distinct request; editing the form starts a new request.
  const [attempt, setAttempt] = useState<{ key: string; body: string } | null>(null);

  const create = useMutation({
    mutationFn: ({ request, key }: { request: CreateReservationRequest; key: string }) => api.reservations.create(request, key),
    onSuccess: (reservation) => navigate(`/reservations/${reservation.id}`),
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    const request: CreateReservationRequest = {
      branchId,
      partySize,
      startsAt: new Date(startsAt).toISOString(),
      durationMinutes: duration,
      specialRequests: specialRequests.trim() || null,
    };
    const body = JSON.stringify(request);
    const key = attempt?.body === body ? attempt.key : newIdempotencyKey();
    setAttempt({ key, body });
    create.mutate({ request, key });
  }

  return (
    <>
      <PageHeader title="Reserve a table" />
      <Card narrow>
        <form className="stack" onSubmit={submit}>
          <div className="form-grid">
            <Field label="Guests">
              <input type="number" min={1} max={50} value={partySize} onChange={(e) => setPartySize(Number(e.target.value))} required />
            </Field>
            <Field label="Duration">
              <select value={duration} onChange={(e) => setDuration(Number(e.target.value))}>
                {[60, 90, 120, 150, 180].map((minutes) => (
                  <option key={minutes} value={minutes}>
                    {minutes} minutes
                  </option>
                ))}
              </select>
            </Field>
          </div>
          <Field label="Date and time">
            <input type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} required />
          </Field>
          <Field label="Special requests">
            <textarea value={specialRequests} maxLength={1000} onChange={(e) => setSpecialRequests(e.target.value)} placeholder="High chair, window seat…" />
          </Field>
          <Button type="submit" variant="primary" busy={create.isPending}>
            Request reservation
          </Button>
          <ErrorAlert error={create.error} title="The reservation was not made" />
        </form>
      </Card>
    </>
  );
}
