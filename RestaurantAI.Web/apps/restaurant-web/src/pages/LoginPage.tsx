import { DevLoginForm } from "@restaurant-ai/auth";
import { Card, PageHeader } from "@restaurant-ai/ui";
import { useLocation, useNavigate, useSearchParams } from "react-router";
import { managerPersona } from "../personas";

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [search] = useSearchParams();
  const from = (location.state as { from?: string } | null)?.from ?? "/";

  return (
    <>
      <PageHeader title="Restaurant console" description="Manage your restaurant, menu, orders and reservations." />
      <Card narrow>
        <DevLoginForm persona={managerPersona} defaultTenantId={search.get("tenant") ?? ""} onSignedIn={() => navigate(from, { replace: true })} />
      </Card>
    </>
  );
}
