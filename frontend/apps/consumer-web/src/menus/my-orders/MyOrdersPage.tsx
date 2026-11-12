import { useApi } from "@restaurant-ai/auth";
import { Badge, Card, EmptyState, ErrorAlert, formatDateTime, formatMoney, PageHeader, shortId, Spinner } from "@restaurant-ai/ui";
import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router";
import { useVenueTable } from "../../auth/useVenueTable";
import { venuePath } from "../../shared/venue";
import { isFinalOrderStatus, orderTone } from "./status";

/** The guest's orders at the current restaurant, refreshed while any is still in progress. */
export function MyOrdersPage() {
  const { tenantId = "", branchId = "" } = useParams();
  const tableId = useVenueTable();
  const api = useApi();
  const orders = useQuery({
    queryKey: ["my-orders", tenantId],
    queryFn: ({ signal }) => api.orders.mine(signal),
    refetchInterval: (query) => (query.state.data?.some((o) => !isFinalOrderStatus(o.status)) ? 8_000 : false),
  });

  return (
    <>
      <PageHeader
        title="My orders"
        description="Orders you placed at this restaurant, including ones the AI waiter placed for you."
        actions={
          <Link className="btn" to={venuePath({ tenantId, branchId, tableId })}>
            Back to menu
          </Link>
        }
      />
      <ErrorAlert error={orders.error} title="Could not load your orders" />
      {orders.isPending && <Spinner label="Loading orders" />}
      {orders.data?.length === 0 && <EmptyState title="No orders yet">Add dishes to your cart or ask the AI waiter.</EmptyState>}
      <div className="grid">
        {orders.data?.map((order) => (
          <Card key={order.id} as="article">
            <div className="row row-between">
              <h3>
                <Link to={`/orders/${order.id}`}>Order {shortId(order.id)}</Link>
              </h3>
              <Badge tone={orderTone(order.status)}>{order.status}</Badge>
            </div>
            <p className="small muted">
              {formatDateTime(order.placedAt)} · {order.channel === "AiWaiter" ? "via AI waiter" : order.channel === "QrTable" ? "at your table" : "online"}
            </p>
            <ul className="small" style={{ margin: 0, paddingLeft: "1.1rem" }}>
              {order.lines.map((line) => (
                <li key={line.menuItemId}>
                  {line.quantity} × {line.name}
                </li>
              ))}
            </ul>
            <strong>{formatMoney(order.total, order.currency)}</strong>
          </Card>
        ))}
      </div>
    </>
  );
}
