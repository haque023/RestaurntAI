import { useAuth } from "@restaurant-ai/auth";
import { ErrorAlert, Spinner } from "@restaurant-ai/ui";
import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { venuePath } from "../venue";

/**
 * Target of the printed table QR code (/t/<code>). Opens the restaurant at that table, with a
 * temporary guest identity if the visitor is not signed in, so the AI waiter is one tap away.
 */
export function QrEntryPage() {
  const { code = "" } = useParams();
  const { api, signIn } = useAuth();
  const navigate = useNavigate();
  const [error, setError] = useState<unknown>(null);

  useEffect(() => {
    let cancelled = false;
    api.auth
      .venueSession({ qrToken: code })
      .then((session) => {
        if (cancelled || !session.venue?.branchId) return;
        signIn(session.accessToken);
        navigate(venuePath({ tenantId: session.venue.tenantId, branchId: session.venue.branchId, tableId: session.venue.tableId }), { replace: true });
      })
      .catch((e: unknown) => {
        if (!cancelled) setError(e);
      });
    return () => {
      cancelled = true;
    };
  }, [api, code, navigate, signIn]);

  if (error) {
    return (
      <div className="stack">
        <ErrorAlert error={error} title="This table code did not work" />
        <p>
          Ask a member of staff for help, or <Link to="/">find the restaurant</Link> and order from its menu.
        </p>
      </div>
    );
  }

  return <Spinner label="Finding your table" />;
}
