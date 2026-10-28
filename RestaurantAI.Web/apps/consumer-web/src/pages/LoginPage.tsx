import { DevLoginForm } from "@restaurant-ai/auth";
import { Card, PageHeader } from "@restaurant-ai/ui";
import { useLocation, useNavigate } from "react-router";
import { customerPersona, devUserId } from "../personas";

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? "/";

  return (
    <>
      <PageHeader title="Sign in" description="Save restaurants, order, reserve and collect loyalty points." />
      <Card narrow>
        <DevLoginForm persona={customerPersona} userId={devUserId()} onSignedIn={() => navigate(from, { replace: true })} />
      </Card>
    </>
  );
}
