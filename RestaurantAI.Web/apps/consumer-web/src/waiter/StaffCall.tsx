import type { ServiceRequestKind } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Button, ErrorAlert } from "@restaurant-ai/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

/** "Call waiter" / "Request the bill" for guests seated at a table (QR session). */
export function StaffCall() {
  const api = useApi();
  const queryClient = useQueryClient();
  const requests = useQuery({ queryKey: ["my-requests"], queryFn: ({ signal }) => api.serviceRequests.mine(signal), refetchInterval: 15_000 });
  const pending = (requests.data ?? []).filter((r) => r.status !== "Resolved");

  const call = useMutation({
    mutationFn: (kind: ServiceRequestKind) => api.serviceRequests.open(kind),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["my-requests"] }),
  });

  const waiting = (kind: ServiceRequestKind) => pending.some((r) => r.kind === kind);

  return (
    <div className="stack">
      <div className="row">
        <Button onClick={() => call.mutate("CallWaiter")} busy={call.isPending && call.variables === "CallWaiter"} disabled={waiting("CallWaiter")}>
          {waiting("CallWaiter") ? "Waiter is on the way" : "🙋 Call waiter"}
        </Button>
        <Button onClick={() => call.mutate("RequestBill")} busy={call.isPending && call.variables === "RequestBill"} disabled={waiting("RequestBill")}>
          {waiting("RequestBill") ? "Bill requested" : "🧾 Request the bill"}
        </Button>
        {pending.some((r) => r.status === "Acknowledged") && <span className="small muted">Staff have seen your request.</span>}
      </div>
      <ErrorAlert error={call.error} title="Could not reach staff" />
    </div>
  );
}
