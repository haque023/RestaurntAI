import { useApi } from "@restaurant-ai/auth";
import { Badge, Card, ErrorAlert, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router";

/** Model configuration, the agent registry with each agent's tools, and usage per restaurant. */
export function AiGovernancePage() {
  const api = useApi();
  const registry = useQuery({ queryKey: ["admin", "agents"], queryFn: () => api.admin.agents() });
  const usage = useQuery({ queryKey: ["admin", "ai-usage"], queryFn: () => api.admin.aiUsage() });
  const restaurants = useQuery({ queryKey: ["admin", "restaurants", "", ""], queryFn: ({ signal }) => api.admin.restaurants({}, signal) });
  const name = (id: string | null) => (id ? (restaurants.data?.find((r) => r.id === id)?.name ?? id.slice(0, 8)) : "Platform discovery");

  return (
    <>
      <PageHeader title="AI governance" description="Agents act only through authorized MCP tools with the user's own permissions; every tool call is audited." />
      <ErrorAlert error={registry.error ?? usage.error} title="Could not load AI data" />
      {registry.isPending && <Spinner />}

      {registry.data && (
        <Card>
          <h2>Model</h2>
          <p>
            <span className="mono">{registry.data.model}</span> · effort {registry.data.effort} · up to {registry.data.maxOutputTokens.toLocaleString()} output tokens ·
            refusal fallback {registry.data.refusalFallback ? <Badge tone="success">on</Badge> : <Badge>off</Badge>}
          </p>
        </Card>
      )}

      {registry.data && (
        <Card>
          <h2>Agent registry</h2>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Agent</th>
                  <th>Purpose</th>
                  <th>Tools it may call</th>
                </tr>
              </thead>
              <tbody>
                {registry.data.agents.map((a) => (
                  <tr key={a.name}>
                    <td className="mono">{a.name}</td>
                    <td className="small">{a.description}</td>
                    <td>
                      <div className="row">
                        {a.tools.map((t) => (
                          <Badge key={t}>{t}</Badge>
                        ))}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {usage.data && (
        <Card>
          <h2>Usage this month</h2>
          <p className="small muted">
            {usage.data.summary.turns} turns · {usage.data.summary.toolCalls} tool calls · {usage.data.summary.failed} failed · {usage.data.summary.declined} declined ·{" "}
            <Link to="/audit?type=mcp.ToolExecuted">tool audit</Link>
          </p>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Restaurant</th>
                  <th className="num">Turns</th>
                  <th className="num">Tokens</th>
                  <th className="num">Failed</th>
                </tr>
              </thead>
              <tbody>
                {usage.data.byTenant.map((t) => (
                  <tr key={t.tenantId ?? "platform"}>
                    <td>{t.tenantId ? <Link to={`/restaurants/${t.tenantId}`}>{name(t.tenantId)}</Link> : name(null)}</td>
                    <td className="num">{t.turns}</td>
                    <td className="num">{t.tokens.toLocaleString()}</td>
                    <td className="num">{t.failed}</td>
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
