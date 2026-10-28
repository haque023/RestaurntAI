import { useApi } from "@restaurant-ai/auth";
import { Card, ErrorAlert, formatDateTime, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router";

export function OverviewPage() {
  const api = useApi();
  const overview = useQuery({ queryKey: ["admin", "overview"], queryFn: () => api.admin.overview() });

  return (
    <>
      <PageHeader title="Overview" actions={<Link className="btn btn-primary" to="/restaurants/new">Register restaurant</Link>} />
      <div className="grid">
        <Card>
          <h2>Platform</h2>
          {overview.isPending && <Spinner />}
          <ErrorAlert error={overview.error} title="Admin API unavailable" />
          {overview.data && (
            <dl className="stack" style={{ margin: 0 }}>
              <div>
                <dt className="small muted">Environment</dt>
                <dd style={{ margin: 0 }}>{overview.data.environment}</dd>
              </div>
              <div>
                <dt className="small muted">Server time</dt>
                <dd style={{ margin: 0 }}>{formatDateTime(overview.data.serverTime)}</dd>
              </div>
            </dl>
          )}
        </Card>
        <Card>
          <h2>Coming next</h2>
          <p className="muted small">Restaurant verification, plans and entitlements, AI configuration, usage monitoring and audit logs need their Admin API endpoints.</p>
        </Card>
      </div>
    </>
  );
}
