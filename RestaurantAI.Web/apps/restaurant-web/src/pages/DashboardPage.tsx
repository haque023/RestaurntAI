import { Permissions, type OrderStats } from "@restaurant-ai/api-client";
import { useApi, useAuth } from "@restaurant-ai/auth";
import { Card, EmptyState, ErrorAlert, formatMoney, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { BranchPicker } from "../BranchPicker";
import { useBranch } from "../useBranch";

const PERIODS = [
  { days: 1, label: "Today" },
  { days: 7, label: "7 days" },
  { days: 30, label: "30 days" },
];

/** Sales, operations and AI KPIs for the restaurant (or one branch). */
export function DashboardPage() {
  const api = useApi();
  const { hasPermission } = useAuth();
  const { branchId, branches } = useBranch();
  const [days, setDays] = useState(7);
  const [allBranches, setAllBranches] = useState(true);
  const scopedBranch = allBranches && branches.length > 1 ? undefined : branchId || undefined;

  const stats = useQuery({
    queryKey: ["stats", days, scopedBranch],
    queryFn: ({ signal }) => {
      const to = new Date();
      const from = new Date(to.getTime() - days * 24 * 3600_000);
      return api.orders.stats({ from: from.toISOString(), to: to.toISOString(), branchId: scopedBranch }, signal);
    },
    refetchInterval: 30_000,
  });
  const ai = useQuery({ queryKey: ["ai-usage"], queryFn: ({ signal }) => api.ai.usage(signal), enabled: hasPermission(Permissions.AnalyticsRead) });

  return (
    <>
      <PageHeader
        title="Dashboard"
        description="Sales and operations, updated every 30 seconds."
        actions={
          <>
            <div className="row" role="group" aria-label="Period">
              {PERIODS.map((p) => (
                <button key={p.days} type="button" className="chip" aria-pressed={days === p.days} onClick={() => setDays(p.days)}>
                  {p.label}
                </button>
              ))}
            </div>
            {branches.length > 1 && (
              <button type="button" className="chip" aria-pressed={allBranches} onClick={() => setAllBranches((v) => !v)}>
                All branches
              </button>
            )}
            {!allBranches && <BranchPicker />}
          </>
        }
      />

      <ErrorAlert error={stats.error} title="Could not load sales" />
      {stats.isPending && <Spinner label="Loading sales" />}
      {stats.data && <SalesOverview stats={stats.data} />}

      {ai.data && (
        <Card>
          <h2>AI waiter this month</h2>
          <div className="stat-grid">
            <Stat label="Conversation turns" value={`${ai.data.usedTurns}${ai.data.limit ? ` / ${ai.data.limit}` : ""}`} />
            <Stat label="Tool calls (menu, orders…)" value={String(ai.data.summary.toolCalls)} />
            <Stat label="Average reply time" value={ai.data.summary.averageLatencyMs ? `${(ai.data.summary.averageLatencyMs / 1000).toFixed(1)} s` : "–"} />
            <Stat label="Status" value={ai.data.aiEnabled === false ? "Off" : "On"} />
          </div>
          {ai.data.limit !== null && ai.data.usedTurns >= ai.data.limit * 0.8 && (
            <p className="small">You have used {Math.round((ai.data.usedTurns / ai.data.limit) * 100)}% of this month's AI allowance. Ask the platform team about a larger plan.</p>
          )}
        </Card>
      )}
    </>
  );
}

function SalesOverview({ stats }: { stats: OrderStats }) {
  const currency = stats.currency ?? "USD";
  if (stats.orders === 0) {
    return <EmptyState title="No orders in this period">Orders from the QR menu, the app and the AI waiter appear here.</EmptyState>;
  }

  const maxDaily = Math.max(...stats.daily.map((d) => d.revenue), 1);
  const maxItem = Math.max(...stats.topItems.map((i) => i.quantity), 1);

  return (
    <>
      <div className="stat-grid">
        <Stat label="Revenue" value={formatMoney(stats.revenue, currency)} />
        <Stat label="Orders" value={String(stats.orders)} />
        <Stat label="Average order" value={formatMoney(stats.averageOrderValue, currency)} />
        <Stat label="In progress" value={String(stats.open)} />
        <Stat label="Avg. prep time" value={stats.averagePrepMinutes !== null ? `${stats.averagePrepMinutes} min` : "–"} />
        <Stat label="Cancelled / rejected" value={`${stats.cancelled} / ${stats.rejected}`} />
      </div>

      <div className="grid">
        <Card>
          <h2>Revenue by day</h2>
          <div className="stack">
            {stats.daily.map((d) => (
              <div key={d.date} className="bar-row">
                <span className="muted">{new Date(d.date).toLocaleDateString(undefined, { weekday: "short", day: "numeric", month: "short" })}</span>
                <div className="bar" style={{ width: `${(d.revenue / maxDaily) * 100}%` }} />
                <span className="num small">{formatMoney(d.revenue, currency)}</span>
              </div>
            ))}
          </div>
        </Card>
        <Card>
          <h2>Top dishes</h2>
          <div className="stack">
            {stats.topItems.map((item) => (
              <div key={item.menuItemId} className="bar-row">
                <span title={item.name} style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                  {item.name}
                </span>
                <div className="bar" style={{ width: `${(item.quantity / maxItem) * 100}%` }} />
                <span className="num small">{item.quantity} sold</span>
              </div>
            ))}
          </div>
        </Card>
        <Card>
          <h2>Where orders come from</h2>
          <div className="stack small">
            {Object.entries(stats.byChannel).map(([channel, count]) => (
              <div key={channel} className="row row-between">
                <span>{CHANNEL_LABELS[channel] ?? channel}</span>
                <strong>{count}</strong>
              </div>
            ))}
          </div>
        </Card>
      </div>
    </>
  );
}

const CHANNEL_LABELS: Record<string, string> = {
  QrTable: "Table QR",
  AiWaiter: "AI waiter",
  ConsumerWeb: "Online",
  ConsumerApp: "App",
  Staff: "Staff",
};

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="stat">
      <div className="stat-value">{value}</div>
      <div className="stat-label">{label}</div>
    </div>
  );
}
