import { RoleLabels } from "@restaurant-ai/api-client";
import { useApi, useAuth } from "@restaurant-ai/auth";
import { Badge, Button, Card, ErrorAlert, Field, formatDateTime, PageHeader, Spinner } from "@restaurant-ai/ui";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useDeferredValue, useState } from "react";

/** Support view of platform accounts: memberships, last sign-in, disable/enable. */
export function UsersPage() {
  const api = useApi();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const [text, setText] = useState("");
  const search = useDeferredValue(text.trim());
  const users = useQuery({ queryKey: ["admin", "users", search], queryFn: () => api.admin.users(search || undefined), placeholderData: keepPreviousData });
  const setActive = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => api.admin.setUserActive(id, active),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["admin", "users"] }),
  });

  return (
    <>
      <PageHeader title="Users" description="Customers, restaurant staff and administrators." />
      <Card>
        <Field label="Search by email or name">
          <input type="search" value={text} onChange={(e) => setText(e.target.value)} />
        </Field>
      </Card>
      <ErrorAlert error={users.error ?? setActive.error} title="Users not updated" />
      {users.isPending && <Spinner />}
      {users.data && (
        <Card>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Account</th>
                  <th>Restaurants</th>
                  <th>Last sign-in</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {users.data.map((u) => (
                  <tr key={u.id}>
                    <td>
                      <strong>{u.displayName}</strong> {u.isPlatformAdministrator && <Badge tone="warning">admin</Badge>}
                      <div className="small muted">{u.email}</div>
                    </td>
                    <td className="small">
                      {u.memberships.length === 0 ? <span className="muted">Customer</span> : u.memberships.map((m) => `${m.tenantName} (${RoleLabels[m.role] ?? m.role})`).join(", ")}
                    </td>
                    <td className="small">{u.lastLoginAt ? formatDateTime(u.lastLoginAt) : "Never"}</td>
                    <td>{u.isActive ? <Badge tone="success">Active</Badge> : <Badge tone="danger">Disabled</Badge>}</td>
                    <td>
                      {u.id !== user?.userId && (
                        <Button size="sm" variant={u.isActive ? "danger" : "default"} onClick={() => setActive.mutate({ id: u.id, active: !u.isActive })}>
                          {u.isActive ? "Disable" : "Enable"}
                        </Button>
                      )}
                    </td>
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
