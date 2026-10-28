import { LoginForm } from "@restaurant-ai/auth";
import { Card, PageHeader } from "@restaurant-ai/ui";
import { useLocation, useNavigate } from "react-router";

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? "/";

  return (
    <>
      <PageHeader title="Platform administration" description="Restaurants, plans, AI governance, audit and support." />
      <Card narrow>
        <LoginForm scope="Admin" onSignedIn={() => navigate(from, { replace: true })} />
      </Card>
    </>
  );
}
