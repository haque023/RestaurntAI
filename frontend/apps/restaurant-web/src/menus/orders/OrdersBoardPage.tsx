import type { DiningTable, Order, OrderStatus, ServiceRequest } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Badge, Button, EmptyState, ErrorAlert, formatMoney, PageHeader, shortId, Spinner } from "@restaurant-ai/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BranchPicker } from "../../shared/BranchPicker";
import { useBranch } from "../../shared/useBranch";

const COLUMNS: { status: OrderStatus; title: string; action: string }[] = [
  { status: "Placed", title: "New", action: "Accept" },
  { status: "Accepted", title: "Accepted", action: "Start cooking" },
  { status: "Preparing", title: "Preparing", action: "Ready" },
  { status: "Ready", title: "Ready to serve", action: "Served" },
  { status: "Served", title: "Served", action: "Complete" },
];

const ACTIVE: OrderStatus[] = ["Placed", "Accepted", "Preparing", "Ready", "Served"];

const KIND_LABELS: Record<string, string> = {
  CallWaiter: "Calling a waiter",
  RequestBill: "Wants the bill",
  Assistance: "Needs help",
  AiEscalation: "AI waiter handed over",
};

/** Live order queue for the floor and kitchen: one column per status, refreshed every 5 seconds. */
export function OrdersBoardPage() {
  const api = useApi();
  const queryClient = useQueryClient();
  const { branchId, branch } = useBranch();

  const orders = useQuery({
    queryKey: ["board", branchId],
    queryFn: ({ signal }) => api.orders.list({ branchId, status: ACTIVE, take: 200 }, signal),
    enabled: Boolean(branchId),
    refetchInterval: 5_000,
  });
  const requests = useQuery({
    queryKey: ["service-requests", branchId],
    queryFn: ({ signal }) => api.serviceRequests.list(branchId, signal),
    enabled: Boolean(branchId),
    refetchInterval: 5_000,
  });
  const tables = useQuery({ queryKey: ["tables", branchId], queryFn: ({ signal }) => api.tables.list(branchId, signal), enabled: Boolean(branchId), staleTime: 60_000 });
  const tableLabel = (id: string | null) => (id ? (tables.data?.find((t: DiningTable) => t.id === id)?.label ?? "table") : null);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["board", branchId] });
  const advance = useMutation({ mutationFn: ({ id, status }: { id: string; status: OrderStatus }) => api.orders.setStatus(id, status), onSuccess: refresh });
  const reject = useMutation({ mutationFn: ({ id, reason }: { id: string; reason: string }) => api.orders.setStatus(id, "Rejected", reason), onSuccess: refresh });
  const handle = useMutation({
    mutationFn: ({ id, resolve }: { id: string; resolve: boolean }) => (resolve ? api.serviceRequests.resolve(id) : api.serviceRequests.acknowledge(id)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["service-requests", branchId] }),
  });

  if (!branchId) return <EmptyState title="No branches yet">Add a branch on the Restaurant page first.</EmptyState>;

  const byStatus = (status: OrderStatus) => (orders.data ?? []).filter((o) => o.status === status).sort((a, b) => a.placedAt.localeCompare(b.placedAt));

  return (
    <>
      <PageHeader title="Orders" description={`${branch?.name ?? "Branch"} · live, updates every 5 seconds`} actions={<BranchPicker />} />

      {(requests.data?.length ?? 0) > 0 && (
        <section className="stack" aria-label="Guests asking for staff">
          <h2>Guests asking for staff</h2>
          <div className="grid">
            {requests.data!.map((r: ServiceRequest) => (
              <div key={r.id} className="ticket" style={{ borderColor: r.status === "Open" ? "var(--color-primary)" : undefined }}>
                <div className="row row-between">
                  <strong>{tableLabel(r.tableId) ? `Table ${tableLabel(r.tableId)}` : "Remote guest"}</strong>
                  <Badge tone={r.status === "Open" ? "warning" : "neutral"}>{r.status === "Open" ? "New" : "Seen"}</Badge>
                </div>
                <div>{KIND_LABELS[r.kind] ?? r.kind}</div>
                {r.note && <p className="small muted">{r.note}</p>}
                <div className="small muted">{minutesAgo(r.createdAt)}</div>
                <div className="row">
                  {r.status === "Open" && (
                    <Button size="sm" onClick={() => handle.mutate({ id: r.id, resolve: false })}>
                      On my way
                    </Button>
                  )}
                  <Button size="sm" variant="primary" onClick={() => handle.mutate({ id: r.id, resolve: true })}>
                    Done
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      <ErrorAlert error={orders.error ?? advance.error ?? reject.error ?? handle.error} title="Something did not update" />
      {orders.isPending && <Spinner label="Loading orders" />}

      <div className="board">
        {COLUMNS.map((column) => {
          const list = byStatus(column.status);
          return (
            <section key={column.status} className="board-column" aria-label={column.title}>
              <h3>
                {column.title} <Badge>{list.length}</Badge>
              </h3>
              {list.map((order) => (
                <OrderTicket
                  key={order.id}
                  order={order}
                  table={tableLabel(order.tableId)}
                  action={column.action}
                  busy={(advance.isPending && advance.variables?.id === order.id) || (reject.isPending && reject.variables?.id === order.id)}
                  onAdvance={() => order.nextStatus && advance.mutate({ id: order.id, status: order.nextStatus })}
                  onReject={() => {
                    const reason = window.prompt("Why can't you accept this order? The guest will see this.", "Kitchen is too busy right now");
                    if (reason !== null) reject.mutate({ id: order.id, reason });
                  }}
                />
              ))}
              {list.length === 0 && <p className="small muted">Nothing here.</p>}
            </section>
          );
        })}
      </div>
    </>
  );
}

function OrderTicket({
  order,
  table,
  action,
  busy,
  onAdvance,
  onReject,
}: {
  order: Order;
  table: string | null;
  action: string;
  busy: boolean;
  onAdvance: () => void;
  onReject: () => void;
}) {
  return (
    <article className="ticket">
      <div className="row row-between">
        <strong>{table ? `Table ${table}` : `#${shortId(order.id)}`}</strong>
        <span className="small muted">{minutesAgo(order.placedAt)}</span>
      </div>
      <div className="row">
        {order.channel === "AiWaiter" && <Badge tone="success">AI waiter</Badge>}
        {!table && <Badge>{order.channel === "QrTable" ? "Table" : "Online"}</Badge>}
      </div>
      <ul className="small">
        {order.lines.map((line) => (
          <li key={line.menuItemId}>
            <strong>{line.quantity}×</strong> {line.name}
            {line.notes && <span className="muted"> — {line.notes}</span>}
          </li>
        ))}
      </ul>
      {order.notes && <p className="small">📝 {order.notes}</p>}
      <div className="row row-between">
        <strong>{formatMoney(order.total, order.currency)}</strong>
        <div className="row">
          {order.status === "Placed" && (
            <Button size="sm" variant="danger" onClick={onReject} disabled={busy}>
              Reject
            </Button>
          )}
          <Button size="sm" variant="primary" onClick={onAdvance} busy={busy}>
            {action}
          </Button>
        </div>
      </div>
    </article>
  );
}

function minutesAgo(iso: string): string {
  const minutes = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60_000));
  return minutes < 1 ? "just now" : minutes < 60 ? `${minutes} min ago` : `${Math.floor(minutes / 60)} h ${minutes % 60} min ago`;
}
