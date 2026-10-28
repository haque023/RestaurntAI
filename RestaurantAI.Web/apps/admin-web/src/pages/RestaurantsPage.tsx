import type { Restaurant, RestaurantStatus } from "@restaurant-ai/api-client";
import { RoleLabels } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Badge, Button, Card, EmptyState, ErrorAlert, Field, formatDateTime, PageHeader, Spinner, type BadgeTone } from "@restaurant-ai/ui";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useDeferredValue, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router";

const STATUSES: { value: RestaurantStatus | ""; label: string }[] = [
  { value: "", label: "All" },
  { value: "PendingVerification", label: "Awaiting verification" },
  { value: "Active", label: "Listed" },
  { value: "Draft", label: "Verified, not listed" },
  { value: "Suspended", label: "Suspended" },
];

const STATUS_TONE: Record<RestaurantStatus, BadgeTone> = {
  PendingVerification: "warning",
  Active: "success",
  Draft: "neutral",
  Suspended: "danger",
};

const STATUS_LABEL: Record<RestaurantStatus, string> = {
  PendingVerification: "Awaiting verification",
  Active: "Listed",
  Draft: "Verified",
  Suspended: "Suspended",
};

/** Every tenant on the platform, filterable by lifecycle status. */
export function RestaurantsPage() {
  const api = useApi();
  const [params, setParams] = useSearchParams();
  const status = (params.get("status") ?? "") as RestaurantStatus | "";
  const [text, setText] = useState("");
  const search = useDeferredValue(text.trim());

  const list = useQuery({
    queryKey: ["admin", "restaurants", status, search],
    queryFn: ({ signal }) => api.admin.restaurants({ status: status || undefined, search: search || undefined }, signal),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      <PageHeader
        title="Restaurants"
        description="Verify new sign-ups, manage plans and AI access, suspend or reinstate."
        actions={
          <Link className="btn btn-primary" to="/restaurants/new">
            Onboard a restaurant
          </Link>
        }
      />
      <Card>
        <div className="row">
          {STATUSES.map((s) => (
            <button key={s.value} type="button" className="chip" aria-pressed={status === s.value} onClick={() => setParams(s.value ? { status: s.value } : {})}>
              {s.label}
            </button>
          ))}
        </div>
        <Field label="Search by name">
          <input type="search" value={text} onChange={(e) => setText(e.target.value)} />
        </Field>
      </Card>

      <ErrorAlert error={list.error} title="Could not load restaurants" />
      {list.isPending && <Spinner />}
      {list.data?.length === 0 && <EmptyState title="No restaurants match" />}
      {list.data && list.data.length > 0 && (
        <Card>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Restaurant</th>
                  <th>Status</th>
                  <th>Plan</th>
                  <th className="num">Branches</th>
                  <th>AI</th>
                  <th>Joined</th>
                </tr>
              </thead>
              <tbody>
                {list.data.map((r) => (
                  <tr key={r.id}>
                    <td>
                      <Link to={`/restaurants/${r.id}`}>
                        <strong>{r.name}</strong>
                      </Link>
                      <div className="small muted">{r.defaultCurrency}</div>
                    </td>
                    <td>
                      <Badge tone={STATUS_TONE[r.status]}>{STATUS_LABEL[r.status]}</Badge>
                    </td>
                    <td>{r.planCode}</td>
                    <td className="num">{r.branchCount}</td>
                    <td>{r.aiEnabled ? <Badge tone="success">On</Badge> : <Badge tone="danger">Off</Badge>}</td>
                    <td className="small">{formatDateTime(r.createdAt)}</td>
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

/** One restaurant: lifecycle actions, plan, AI governance, branches and team. */
export function RestaurantDetailPage() {
  const { restaurantId = "" } = useParams();
  const api = useApi();
  const queryClient = useQueryClient();
  const restaurant = useQuery({ queryKey: ["admin", "restaurant", restaurantId], queryFn: () => api.admin.restaurant(restaurantId) });
  const members = useQuery({ queryKey: ["admin", "members", restaurantId], queryFn: () => api.admin.tenantMembers(restaurantId) });
  const plans = useQuery({ queryKey: ["plans"], queryFn: () => api.restaurants.plans(), staleTime: Infinity });

  const apply = (updated: Restaurant) => {
    queryClient.setQueryData(["admin", "restaurant", restaurantId], updated);
    void queryClient.invalidateQueries({ queryKey: ["admin", "restaurants"] });
  };
  const approve = useMutation({ mutationFn: () => api.admin.approve(restaurantId), onSuccess: apply });
  const suspend = useMutation({ mutationFn: (reason: string) => api.admin.suspend(restaurantId, reason), onSuccess: apply });
  const reinstate = useMutation({ mutationFn: () => api.admin.reinstate(restaurantId), onSuccess: apply });
  const changePlan = useMutation({ mutationFn: (plan: string) => api.admin.changePlan(restaurantId, plan), onSuccess: apply });
  const setAi = useMutation({ mutationFn: (enabled: boolean) => api.admin.setAi(restaurantId, enabled), onSuccess: apply });

  if (restaurant.isPending) return <Spinner />;
  if (restaurant.error) return <ErrorAlert error={restaurant.error} title="Restaurant not found" />;
  const r = restaurant.data;

  return (
    <>
      <PageHeader
        title={r.name}
        description={`${r.defaultCurrency} · ${r.timeZone} · joined ${formatDateTime(r.createdAt)}`}
        actions={<Badge tone={STATUS_TONE[r.status]}>{STATUS_LABEL[r.status]}</Badge>}
      />
      <ErrorAlert error={approve.error ?? suspend.error ?? reinstate.error ?? changePlan.error ?? setAi.error} title="Not changed" />

      <div className="grid">
        <Card>
          <h2>Lifecycle</h2>
          {r.status === "PendingVerification" && (
            <>
              <p className="small">Check the business details and owner below, then approve. The owner can list the restaurant for guests afterwards.</p>
              <Button variant="primary" busy={approve.isPending} onClick={() => approve.mutate()}>
                Approve
              </Button>
            </>
          )}
          {r.status === "Suspended" ? (
            <>
              <p className="small">Reason: {r.suspensionReason}</p>
              <Button busy={reinstate.isPending} onClick={() => reinstate.mutate()}>
                Reinstate
              </Button>
            </>
          ) : (
            <div>
              <Button
                variant="danger"
                busy={suspend.isPending}
                onClick={() => {
                  const reason = window.prompt("Reason for suspending (visible to the owner):");
                  if (reason) suspend.mutate(reason);
                }}
              >
                Suspend
              </Button>
            </div>
          )}
          {r.description && <p className="small muted">{r.description}</p>}
          <p className="small">
            {r.phone && <>📞 {r.phone} </>}
            {r.website && <>🌐 {r.website}</>}
          </p>
        </Card>

        <Card>
          <h2>Plan</h2>
          <Field label="Subscription plan">
            <select value={r.plan.code} onChange={(e) => changePlan.mutate(e.target.value)} disabled={changePlan.isPending}>
              {plans.data?.map((p) => (
                <option key={p.code} value={p.code}>
                  {p.name} — {p.maxBranches} branches, {p.aiTurnsPerMonth.toLocaleString()} AI messages/mo, {p.monthlyPrice ? `$${p.monthlyPrice}` : "free"}
                </option>
              ))}
            </select>
          </Field>
          <h2>AI governance</h2>
          <p className="small muted">Turning AI off disables the AI waiter for every guest of this restaurant immediately (within a minute).</p>
          <Button variant={r.aiEnabled ? "danger" : "primary"} busy={setAi.isPending} onClick={() => setAi.mutate(!r.aiEnabled)}>
            {r.aiEnabled ? "Turn AI off" : "Turn AI on"}
          </Button>
        </Card>
      </div>

      <Card>
        <h2>Branches</h2>
        {r.branches.length === 0 ? (
          <p className="muted small">No branches yet.</p>
        ) : (
          <ul>
            {r.branches.map((b) => (
              <li key={b.id}>
                <strong>{b.name}</strong> — {b.address || "no address"} {b.latitude === null && <Badge tone="warning">no location</Badge>}
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card>
        <h2>Team</h2>
        <ErrorAlert error={members.error} title="Could not load team" />
        {members.data?.length === 0 && <p className="muted small">No accounts linked to this restaurant.</p>}
        <ul>
          {members.data?.map((m) => (
            <li key={m.userId}>
              {m.displayName} &lt;{m.email}&gt; — {m.roles.map((x) => RoleLabels[x.role] ?? x.role).join(", ")}
            </li>
          ))}
        </ul>
        <p className="small muted mono">Tenant id {r.id}</p>
      </Card>
    </>
  );
}
