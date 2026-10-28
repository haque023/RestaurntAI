import { newIdempotencyKey } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Button, Card, EmptyState, ErrorAlert, Field, formatMoney, PageHeader } from "@restaurant-ai/ui";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { cartEstimate } from "../cart/cart";
import { useCart } from "../cart/useCart";
import { useVenueTable } from "../session/useVenueTable";
import { venuePath } from "../venue";

export function CartPage() {
  const { tenantId = "", branchId = "" } = useParams();
  const tableId = useVenueTable();
  const menuLink = venuePath({ tenantId, branchId, tableId });
  const api = useApi();
  const navigate = useNavigate();
  const { cart, dispatch } = useCart();
  const [notes, setNotes] = useState("");
  const estimate = cartEstimate(cart);

  const placeOrder = useMutation({
    mutationFn: (idempotencyKey: string) =>
      api.orders.place(
        {
          branchId,
          tableId,
          channel: tableId ? "QrTable" : "ConsumerWeb",
          items: cart.lines.map((l) => ({ menuItemId: l.menuItemId, quantity: l.quantity, notes: l.notes || null })),
          notes: notes || null,
        },
        idempotencyKey,
      ),
    onSuccess: (order) => {
      dispatch({ type: "clear" });
      navigate(`/orders/${order.id}`);
    },
  });

  function confirm() {
    // Reuse the key for retries of the same confirmation so a lost response never creates a second order.
    const key = cart.checkoutKey ?? newIdempotencyKey();
    if (!cart.checkoutKey) dispatch({ type: "beginCheckout", key });
    placeOrder.mutate(key);
  }

  if (cart.lines.length === 0) {
    return (
      <>
        <PageHeader title="Your cart" />
        <EmptyState title="Your cart is empty">
          <Link to={menuLink}>Browse the menu</Link>
        </EmptyState>
      </>
    );
  }

  return (
    <>
      <PageHeader title="Your cart" description={tableId ? "Your order goes straight to the kitchen for your table." : undefined} />
      <Card>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th>Quantity</th>
                <th>Notes</th>
                <th className="num">Price</th>
              </tr>
            </thead>
            <tbody>
              {cart.lines.map((line) => (
                <tr key={line.menuItemId}>
                  <td>{line.name}</td>
                  <td>
                    <input
                      aria-label={`Quantity of ${line.name}`}
                      type="number"
                      min={0}
                      max={99}
                      value={line.quantity}
                      onChange={(e) => dispatch({ type: "setQuantity", menuItemId: line.menuItemId, quantity: Number(e.target.value) })}
                      style={{ width: 72 }}
                    />
                  </td>
                  <td>
                    <input
                      aria-label={`Notes for ${line.name}`}
                      value={line.notes}
                      maxLength={200}
                      placeholder="No onions…"
                      onChange={(e) => dispatch({ type: "setNotes", menuItemId: line.menuItemId, notes: e.target.value })}
                    />
                  </td>
                  <td className="num">{formatMoney(line.unitPrice * line.quantity, line.currency)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Field label="Notes for the kitchen">
          <textarea value={notes} maxLength={500} onChange={(e) => setNotes(e.target.value)} />
        </Field>
        <div className="row row-between">
          <div>
            <div className="small muted">Estimated total</div>
            {estimate && <strong>{formatMoney(estimate.amount, estimate.currency)}</strong>}
            <div className="small muted">The restaurant confirms the final price.</div>
          </div>
          <div className="row">
            <Button variant="ghost" onClick={() => dispatch({ type: "clear" })}>
              Clear
            </Button>
            <Button variant="primary" busy={placeOrder.isPending} onClick={confirm}>
              Place order
            </Button>
          </div>
        </div>
        <ErrorAlert error={placeOrder.error} title="Your order was not placed" />
      </Card>
    </>
  );
}
