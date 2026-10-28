import { LoginForm, RegisterForm } from "@restaurant-ai/auth";
import { Card, PageHeader } from "@restaurant-ai/ui";
import { Link, useLocation, useNavigate } from "react-router";

function useReturnTo() {
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? "/";
  return { from, done: () => navigate(from, { replace: true }) };
}

export function LoginPage() {
  const { from, done } = useReturnTo();

  return (
    <>
      <PageHeader title="Sign in" description="Save restaurants, ask the AI, order, reserve and collect loyalty points." />
      <Card narrow>
        <LoginForm
          scope="Customer"
          onSignedIn={done}
          footer={
            <p className="small muted">
              New here?{" "}
              <Link to="/register" state={{ from }}>
                Create an account
              </Link>
            </p>
          }
        />
      </Card>
    </>
  );
}

export function RegisterPage() {
  const { from, done } = useReturnTo();

  return (
    <>
      <PageHeader title="Create your account" description="One account for every restaurant on the platform." />
      <Card narrow>
        <RegisterForm
          onSignedIn={done}
          footer={
            <p className="small muted">
              Already have an account?{" "}
              <Link to="/login" state={{ from }}>
                Sign in
              </Link>
            </p>
          }
        />
      </Card>
    </>
  );
}
