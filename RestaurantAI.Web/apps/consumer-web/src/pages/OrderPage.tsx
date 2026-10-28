import { newIdempotencyKey } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Badge, Button, Card, ErrorAlert, Field, formatDateTime, formatMoney, PageHeader, shortId, Spinner } from "@restaurant-ai/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useParams } from "react-router";
import { canCancelOrder, isFinalOrderStatus, ORDER_STEPS, orderTone } from "./status";

export function OrderPage() {
  const { orderId = "" } = useParams();
  const api = useApi();
  const queryClient = useQueryClient();
  const [reason, setReason] = useState("");
  const [cancelKey] = useState(newIdempotencyKey);

  const order = useQuery({
    queryKey: ["order", orderId],
    queryFn: ({ signal }) => api.orders.get(orderId, signal),
    // Live tracking until the order reaches a final state.
    refetchInterval: (query) => (query.state.data && isFinalOrderStatus(query.state.data.status) ? false : 5_000),
  });

  const cancel = useMutation({
    mutationFn: () => api.orders.cancel(orderId, reason || "Cancelled by guest", cancelKey),
    onSuccess: (updated) => queryClient.setQueryData(["order", orderId], updated),
  });

  if (order.isPending) return <Spinner label="Loading order" />;
  if (order.error) return <ErrorAlert error={order.error} title="Could not load the order" />;

  const data = order.data;
  const currentStep = ORDER_STEPS.indexOf(data.status);

  return (
    <>
      <PageHeader
        title={`Order ${shortId(data.id)}`}
        description={`Placed ${formatDateTime(data.placedAt)}`}
        actions={<Badge tone={orderTone(data.status)}>{data.status}</Badge>}
      />

      {data.status !== "Cancelled" && (
        <Card>
          <ol className="row" aria-label="Order progress" style={{ listStyle: "none", padding: 0, margin: 0 }}>
            {ORDER_STEPS.map((step, index) => (
              <li key={step}>
                <Badge tone={index <= currentStep ? "success" : "neutral"}>
                  {index <= currentStep ? "✓ " : ""}
                  {step}
                </Badge>
              </li>
            ))}
          </ol>
          {!isFinalOrderStatus(data.status) && <p className="small muted">Updates automatically.</p>}
        </Card>
      )}

      <Card>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th className="num">Qty</th>
                <th className="num">Unit</th>
                <th className="num">Total</th>
              </tr>
            </thead>
            <tbody>
              {data.lines.map((line) => (
                <tr key={line.menuItemId}>
                  <td>
                    {line.name}
                    {line.notes && <div className="small muted">{line.notes}</div>}
                  </td>
                  <td className="num">{line.quantity}</td>
                  <td className="num">{formatMoney(line.unitPrice, data.currency)}</td>
                  <td className="num">{formatMoney(line.lineTotal, data.currency)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <th colSpan={3}>Total</th>
                <td className="num">
                  <strong>{formatMoney(data.total, data.currency)}</strong>
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
      </Card>

      {canCancelOrder(data.status) && (
        <Card narrow>
          <h2>Cancel order</h2>
          <Field label="Reason (optional)">
            <input value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
          </Field>
          <Button variant="danger" busy={cancel.isPending} onClick={() => cancel.mutate()}>
            Cancel order
          </Button>
          <ErrorAlert error={cancel.error} title="The order was not cancelled" />
        </Card>
      )}
    </>
  );
}
