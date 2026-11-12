import { Permissions, type DiningTable, type TableStatus } from "@restaurant-ai/api-client";
import { useApi, useAuth } from "@restaurant-ai/auth";
import { Badge, Button, Card, EmptyState, ErrorAlert, Field, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import QRCode from "qrcode";
import { useState, type FormEvent } from "react";
import { BranchPicker } from "../../shared/BranchPicker";
import { useBranch } from "../../shared/useBranch";

const CONSUMER_URL = (import.meta.env.VITE_CONSUMER_URL ?? "http://localhost:3000").replace(/\/$/, "");

const STATUS_TONE: Record<TableStatus, "success" | "warning" | "danger" | "neutral"> = {
  Available: "success",
  Occupied: "warning",
  Reserved: "neutral",
  OutOfService: "danger",
};

/** The address printed on a table's QR sticker. */
const tableQrUrl = (table: DiningTable) => `${CONSUMER_URL}/t/${table.qrToken}`;

/** Tables, their live status and printable QR codes that open the AI waiter at that table. */
export function TablesPage() {
  const api = useApi();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission(Permissions.RestaurantManage);
  const canUpdate = hasPermission(Permissions.OrdersManage);
  const { branchId, branch } = useBranch();
  const [printing, setPrinting] = useState(false);

  const tables = useQuery({ queryKey: ["tables", branchId], queryFn: ({ signal }) => api.tables.list(branchId, signal), enabled: Boolean(branchId) });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ["tables", branchId] });

  const setStatus = useMutation({ mutationFn: ({ id, status }: { id: string; status: TableStatus }) => api.tables.setStatus(id, status), onSuccess: refresh });
  const rotate = useMutation({ mutationFn: (id: string) => api.tables.rotateQr(id), onSuccess: refresh });
  const remove = useMutation({ mutationFn: (id: string) => api.tables.remove(id), onSuccess: refresh });

  if (!branchId) return <EmptyState title="No branches yet">Add a branch on the Restaurant page first.</EmptyState>;

  return (
    <>
      <PageHeader
        title="Tables & QR codes"
        description={`${branch?.name ?? "Branch"} · guests scan a table's code to see the menu, order and talk to the AI waiter. No app or account needed.`}
        actions={
          <>
            <BranchPicker />
            {(tables.data?.length ?? 0) > 0 && (
              <Button
                onClick={() => {
                  setPrinting(true);
                  setTimeout(() => window.print(), 300);
                }}
              >
                🖨 Print QR codes
              </Button>
            )}
          </>
        }
      />

      <ErrorAlert error={tables.error ?? setStatus.error ?? rotate.error ?? remove.error} title="Tables not updated" />
      {tables.isPending && <Spinner label="Loading tables" />}
      {tables.data?.length === 0 && <EmptyState title="No tables yet">{canManage ? "Add your first table below." : "A manager adds tables."}</EmptyState>}

      {printing ? (
        <div className="stack">
          <div className="row no-print">
            <Button onClick={() => setPrinting(false)}>Back to tables</Button>
          </div>
          <div className="grid">
            {tables.data?.map((t) => (
              <div key={t.id} className="card qr-card">
                <QrImage value={tableQrUrl(t)} />
                <strong style={{ fontSize: "1.25rem" }}>Table {t.label}</strong>
                <span className="small">Scan to order or ask our AI waiter</span>
                <span className="small muted mono">{t.qrToken}</span>
              </div>
            ))}
          </div>
        </div>
      ) : (
        <div className="grid">
          {tables.data?.map((t) => (
            <Card key={t.id} as="article">
              <div className="row row-between">
                <h3>Table {t.label}</h3>
                <Badge tone={STATUS_TONE[t.status]}>{t.status === "OutOfService" ? "Out of service" : t.status}</Badge>
              </div>
              <p className="small muted">{t.capacity} seats</p>
              <div className="qr-card">
                <QrImage value={tableQrUrl(t)} />
                <a className="small" href={tableQrUrl(t)} target="_blank" rel="noreferrer">
                  Open as a guest ↗
                </a>
              </div>
              {canUpdate && (
                <label className="field">
                  <span>Status</span>
                  <select value={t.status} onChange={(e) => setStatus.mutate({ id: t.id, status: e.target.value as TableStatus })}>
                    <option value="Available">Available</option>
                    <option value="Occupied">Occupied</option>
                    <option value="Reserved">Reserved</option>
                    <option value="OutOfService">Out of service</option>
                  </select>
                </label>
              )}
              {canManage && (
                <div className="row">
                  <Button
                    size="sm"
                    busy={rotate.isPending && rotate.variables === t.id}
                    onClick={() => window.confirm(`Replace table ${t.label}'s QR code? Printed stickers for it stop working.`) && rotate.mutate(t.id)}
                  >
                    New QR code
                  </Button>
                  <Button size="sm" variant="danger" onClick={() => window.confirm(`Delete table ${t.label}?`) && remove.mutate(t.id)}>
                    Delete
                  </Button>
                </div>
              )}
            </Card>
          ))}
        </div>
      )}

      {canManage && !printing && <AddTableForm branchId={branchId} />}
    </>
  );
}

function QrImage({ value }: { value: string }) {
  const svg = useQuery({
    queryKey: ["qr", value],
    queryFn: () => QRCode.toString(value, { type: "svg", margin: 1, errorCorrectionLevel: "M" }),
    staleTime: Infinity,
  });
  // The SVG is generated locally from our own URL (path data only), never from user-supplied markup.
  return svg.data ? <div aria-label={`QR code for ${value}`} role="img" dangerouslySetInnerHTML={{ __html: svg.data }} /> : <Spinner label="QR" />;
}

function AddTableForm({ branchId }: { branchId: string }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const [label, setLabel] = useState("");
  const [capacity, setCapacity] = useState(4);

  const add = useMutation({
    mutationFn: () => api.tables.create(branchId, label.trim(), capacity),
    onSuccess: () => {
      setLabel("");
      return queryClient.invalidateQueries({ queryKey: ["tables", branchId] });
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    add.mutate();
  }

  return (
    <Card narrow>
      <h2>Add table</h2>
      <form className="stack" onSubmit={submit}>
        <div className="form-grid">
          <Field label="Label">
            <input value={label} onChange={(e) => setLabel(e.target.value)} placeholder="T1, Patio 3…" required maxLength={40} />
          </Field>
          <Field label="Seats">
            <input type="number" min={1} max={50} value={capacity} onChange={(e) => setCapacity(Number(e.target.value))} required />
          </Field>
        </div>
        <Button type="submit" variant="primary" busy={add.isPending}>
          Add table
        </Button>
        <ErrorAlert error={add.error} title="Table not added" />
      </form>
    </Card>
  );
}
