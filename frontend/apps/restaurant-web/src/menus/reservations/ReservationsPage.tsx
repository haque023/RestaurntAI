import { newIdempotencyKey, type Reservation } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Badge, Button, Card, EmptyState, ErrorAlert, Field, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { BranchPicker } from "../../shared/BranchPicker";
import { useBranch } from "../../shared/useBranch";

const toDateInput = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;

const TONE: Record<string, "success" | "warning" | "danger" | "neutral"> = {
  Confirmed: "warning",
  Pending: "warning",
  Seated: "success",
  Completed: "neutral",
  Cancelled: "danger",
  NoShow: "danger",
};

/** The day's reservation book for a branch: seat guests, close tables, record no-shows. */
export function ReservationsPage() {
  const api = useApi();
  const queryClient = useQueryClient();
  const { branchId, branch } = useBranch();
  const [day, setDay] = useState(() => toDateInput(new Date()));

  const range = () => {
    const from = new Date(`${day}T00:00:00`);
    return { from: from.toISOString(), to: new Date(from.getTime() + 24 * 3600_000).toISOString() };
  };

  const list = useQuery({
    queryKey: ["reservations", branchId, day],
    queryFn: ({ signal }) => api.reservations.list({ branchId, ...range() }, signal),
    enabled: Boolean(branchId),
    refetchInterval: 30_000,
  });

  const act = useMutation({
    mutationFn: ({ id, action }: { id: string; action: "seat" | "complete" | "noShow" | "cancel" }) =>
      action === "cancel" ? api.reservations.cancel(id, "Cancelled by restaurant", newIdempotencyKey()) : api.reservations[action](id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["reservations", branchId, day] }),
  });

  if (!branchId) return <EmptyState title="No branches yet">Add a branch on the Restaurant page first.</EmptyState>;

  const rows = list.data ?? [];
  const covers = rows.filter((r) => r.status !== "Cancelled" && r.status !== "NoShow").reduce((sum, r) => sum + r.partySize, 0);

  return (
    <>
      <PageHeader
        title="Reservations"
        description={`${branch?.name ?? "Branch"} · ${rows.length} bookings, ${covers} guests expected`}
        actions={
          <>
            <BranchPicker />
            <Field label="Day">
              <input type="date" value={day} onChange={(e) => setDay(e.target.value)} />
            </Field>
          </>
        }
      />
      <ErrorAlert error={list.error ?? act.error} title="Reservations not updated" />
      {list.isPending && <Spinner label="Loading reservations" />}
      {list.data?.length === 0 && <EmptyState title="No reservations this day">Guests book from the app or through the AI waiter.</EmptyState>}

      {rows.length > 0 && (
        <Card>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Time</th>
                  <th className="num">Guests</th>
                  <th>Requests</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <ReservationRow key={r.id} reservation={r} busy={act.isPending && act.variables?.id === r.id} onAction={(action) => act.mutate({ id: r.id, action })} />
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}
    </>
  );
}

function ReservationRow({
  reservation: r,
  busy,
  onAction,
}: {
  reservation: Reservation;
  busy: boolean;
  onAction: (action: "seat" | "complete" | "noShow" | "cancel") => void;
}) {
  const time = (iso: string) => new Date(iso).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
  const upcoming = r.status === "Confirmed" || r.status === "Pending";

  return (
    <tr>
      <td>
        <strong>{time(r.startsAt)}</strong> <span className="muted small">– {time(r.endsAt)}</span>
      </td>
      <td className="num">{r.partySize}</td>
      <td className="small">{r.specialRequests ?? <span className="muted">–</span>}</td>
      <td>
        <Badge tone={TONE[r.status] ?? "neutral"}>{r.status === "NoShow" ? "No-show" : r.status}</Badge>
      </td>
      <td>
        <div className="row">
          {upcoming && (
            <>
              <Button size="sm" variant="primary" busy={busy} onClick={() => onAction("seat")}>
                Seat
              </Button>
              <Button size="sm" disabled={busy} onClick={() => onAction("noShow")}>
                No-show
              </Button>
              {new Date(r.startsAt) > new Date() && (
                <Button size="sm" variant="danger" disabled={busy} onClick={() => window.confirm("Cancel this reservation?") && onAction("cancel")}>
                  Cancel
                </Button>
              )}
            </>
          )}
          {r.status === "Seated" && (
            <Button size="sm" busy={busy} onClick={() => onAction("complete")}>
              Finished
            </Button>
          )}
        </div>
      </td>
    </tr>
  );
}
