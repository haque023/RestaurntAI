import { DevLoginForm } from "@restaurant-ai/auth";
import { Card, PageHeader } from "@restaurant-ai/ui";
import { useLocation, useNavigate } from "react-router";
import { adminPersona } from "../personas";

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? "/";

  return (
    <>
      <PageHeader title="Platform administration" description="Privileged access. All actions are audited." />
      <Card narrow>
        <DevLoginForm persona={adminPersona} onSignedIn={() => navigate(from, { replace: true })} />
      </Card>
    </>
  );
}
