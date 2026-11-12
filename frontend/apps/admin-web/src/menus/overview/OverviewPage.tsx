import { useApi } from "@restaurant-ai/auth";
import { Card, ErrorAlert, formatMoney, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router";

/** Platform KPIs: restaurants, subscriptions, orders and AI activity. */
export function OverviewPage() {
  const api = useApi();
  const overview = useQuery({ queryKey: ["admin", "overview"], queryFn: () => api.admin.overview(), refetchInterval: 30_000 });
  const stats = useQuery({ queryKey: ["admin", "restaurant-stats"], queryFn: () => api.admin.restaurantStats() });
  const ai = useQuery({ queryKey: ["admin", "ai-usage"], queryFn: () => api.admin.aiUsage() });

  const pending = stats.data?.byStatus.PendingVerification ?? 0;

  return (
    <>
      <PageHeader title="Overview" description={overview.data ? `${overview.data.environment} · ${new Date(overview.data.serverTime).toLocaleString()}` : undefined} />
      <ErrorAlert error={overview.error ?? stats.error ?? ai.error} title="Could not load platform data" />
      {(overview.isPending || stats.isPending) && <Spinner />}

      {pending > 0 && (
        <div className="alert alert-info">
          <strong>{pending} restaurant{pending === 1 ? "" : "s"} waiting for verification.</strong> <Link to="/restaurants?status=PendingVerification">Review now</Link>
        </div>
      )}

      {stats.data && (
        <Card>
          <h2>Restaurants</h2>
          <div className="stat-grid">
            <Stat label="Restaurants" value={stats.data.restaurants} />
            <Stat label="Listed (live)" value={stats.data.byStatus.Active ?? 0} />
            <Stat label="Awaiting verification" value={pending} />
            <Stat label="Suspended" value={stats.data.byStatus.Suspended ?? 0} />
            <Stat label="Branches" value={stats.data.branches} />
            <Stat label="Tables with QR" value={stats.data.tables} />
            <Stat label="Subscription MRR" value={formatMoney(stats.data.monthlyRecurringRevenue, "USD")} />
          </div>
          <p className="small muted">
            Plans: {Object.entries(stats.data.byPlan).map(([plan, count]) => `${plan} ${count}`).join(" · ") || "none"}
          </p>
        </Card>
      )}

      {overview.data && (
        <Card>
          <h2>This month</h2>
          <div className="stat-grid">
            <Stat label="Orders" value={overview.data.month.orders} />
            {Object.entries(overview.data.month.grossOrderValue).map(([currency, total]) => (
              <Stat key={currency} label={`Order value (${currency})`} value={formatMoney(total, currency)} />
            ))}
            <Stat label="Reservations" value={overview.data.month.reservations} />
            <Stat label="New restaurants" value={overview.data.month.newRestaurants} />
            <Stat label="New users" value={overview.data.month.newUsers} />
          </div>
        </Card>
      )}

      {ai.data && (
        <Card>
          <h2>AI this month</h2>
          <div className="stat-grid">
            <Stat label="Agent turns" value={ai.data.summary.turns} />
            <Stat label="Tool calls" value={ai.data.summary.toolCalls} />
            <Stat label="Tokens" value={(ai.data.summary.inputTokens + ai.data.summary.outputTokens).toLocaleString()} />
            <Stat label="Failed" value={ai.data.summary.failed} />
            <Stat label="Avg latency" value={`${(ai.data.summary.averageLatencyMs / 1000).toFixed(1)} s`} />
          </div>
          <p className="small muted">
            {Object.entries(ai.data.summary.byAgent).map(([agent, n]) => `${agent}: ${n}`).join(" · ")} · <Link to="/ai">Details</Link>
          </p>
        </Card>
      )}

      {overview.data && Object.keys(overview.data.last24h).length > 0 && (
        <Card>
          <h2>Last 24 hours</h2>
          <div className="stack small">
            {Object.entries(overview.data.last24h).map(([type, count]) => (
              <div key={type} className="row row-between">
                <Link to={`/audit?type=${encodeURIComponent(type)}`} className="mono">
                  {type}
                </Link>
                <strong>{count}</strong>
              </div>
            ))}
          </div>
        </Card>
      )}
    </>
  );
}

function Stat({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="stat">
      <div className="stat-value">{value}</div>
      <div className="stat-label">{label}</div>
    </div>
  );
}
