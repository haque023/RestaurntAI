import { PlatformRoles, RoleLabels, StaffRoles, type StaffMember } from "@restaurant-ai/api-client";
import { useApi, useAuth } from "@restaurant-ai/auth";
import { Badge, Button, Card, ErrorAlert, Field, formatDateTime, PageHeader, Spinner } from "@restaurant-ai/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { useRestaurantSummary } from "../../shared/useRestaurant";

const ROLE_HELP: Record<string, string> = {
  [PlatformRoles.RestaurantManager]: "Everything except managing managers",
  [PlatformRoles.Waiter]: "Orders, tables, reservations, guest requests",
  [PlatformRoles.KitchenStaff]: "Kitchen board: accept, cook, mark ready",
  [PlatformRoles.Cashier]: "Orders and payments at the counter",
  [PlatformRoles.RestaurantStaff]: "View and update orders",
  [PlatformRoles.MarketingUser]: "Dashboard and loyalty",
};

/** Staff accounts: role and (optionally) one branch. Permissions follow the role. */
export function StaffPage() {
  const api = useApi();
  const queryClient = useQueryClient();
  const { user, hasRole } = useAuth();
  const isOwner = hasRole(PlatformRoles.RestaurantOwner);
  const branches = useRestaurantSummary().data?.branches ?? [];
  const staff = useQuery({ queryKey: ["staff"], queryFn: () => api.staff.list() });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ["staff"] });

  const update = useMutation({
    mutationFn: ({ member, role, branchId }: { member: StaffMember; role: string; branchId: string | null }) => api.staff.update(member.userId, role, branchId),
    onSuccess: refresh,
  });
  const remove = useMutation({ mutationFn: (userId: string) => api.staff.remove(userId), onSuccess: refresh });

  const branchName = (id: string | null) => (id ? (branches.find((b) => b.id === id)?.name ?? "one branch") : "All branches");
  const assignable = StaffRoles.filter((r) => isOwner || r !== PlatformRoles.RestaurantManager);

  return (
    <>
      <PageHeader title="Staff" description="Give each person their own sign-in. What they can do follows their role; a branch limits them to that branch." />
      <ErrorAlert error={staff.error ?? update.error ?? remove.error} title="Staff not updated" />
      {staff.isPending && <Spinner label="Loading staff" />}

      {staff.data && (
        <Card>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Person</th>
                  <th>Role</th>
                  <th>Branch</th>
                  <th>Last sign-in</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {staff.data.map((m) => {
                  const role = m.roles[0]?.role ?? "";
                  const branchId = m.roles[0]?.branchId ?? null;
                  const protectedMember = m.userId === user?.userId || role === PlatformRoles.RestaurantOwner || (!isOwner && role === PlatformRoles.RestaurantManager);
                  return (
                    <tr key={m.userId}>
                      <td>
                        <strong>{m.displayName}</strong>
                        <div className="small muted">{m.email}</div>
                      </td>
                      <td>
                        {protectedMember ? (
                          <Badge>{RoleLabels[role] ?? role}</Badge>
                        ) : (
                          <select value={role} aria-label={`Role of ${m.displayName}`} onChange={(e) => update.mutate({ member: m, role: e.target.value, branchId })}>
                            {assignable.map((r) => (
                              <option key={r} value={r}>
                                {RoleLabels[r]}
                              </option>
                            ))}
                          </select>
                        )}
                      </td>
                      <td>
                        {protectedMember || branches.length < 2 ? (
                          <span className="small">{branchName(branchId)}</span>
                        ) : (
                          <select
                            value={branchId ?? ""}
                            aria-label={`Branch of ${m.displayName}`}
                            onChange={(e) => update.mutate({ member: m, role, branchId: e.target.value || null })}
                          >
                            <option value="">All branches</option>
                            {branches.map((b) => (
                              <option key={b.id} value={b.id}>
                                {b.name}
                              </option>
                            ))}
                          </select>
                        )}
                      </td>
                      <td className="small muted">{m.lastLoginAt ? formatDateTime(m.lastLoginAt) : "Never"}</td>
                      <td>
                        {!protectedMember && (
                          <Button size="sm" variant="danger" onClick={() => window.confirm(`Remove ${m.displayName}'s access?`) && remove.mutate(m.userId)}>
                            Remove
                          </Button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      <AddStaffForm roles={assignable} branches={branches} />
    </>
  );
}

function AddStaffForm({ roles, branches }: { roles: readonly string[]; branches: { id: string; name: string }[] }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const [form, setForm] = useState({ displayName: "", email: "", password: "", role: PlatformRoles.Waiter as string, branchId: user?.branchId ?? "" });

  const add = useMutation({
    mutationFn: () =>
      api.staff.add({
        email: form.email.trim(),
        displayName: form.displayName.trim() || undefined,
        password: form.password || undefined,
        role: form.role,
        branchId: form.branchId || null,
      }),
    onSuccess: () => {
      setForm({ ...form, displayName: "", email: "", password: "" });
      return queryClient.invalidateQueries({ queryKey: ["staff"] });
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    add.mutate();
  }

  return (
    <Card narrow>
      <h2>Add a team member</h2>
      <form className="stack" onSubmit={submit}>
        <Field label="Name">
          <input value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
        </Field>
        <Field label="Email">
          <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required />
        </Field>
        <Field label="Initial password (new accounts)">
          <input type="text" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} minLength={8} autoComplete="off" />
          <small className="muted">Share it with them privately. Leave empty if they already have an account on the platform.</small>
        </Field>
        <Field label="Role">
          <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}>
            {roles.map((r) => (
              <option key={r} value={r}>
                {RoleLabels[r]} — {ROLE_HELP[r]}
              </option>
            ))}
          </select>
        </Field>
        {!user?.branchId && branches.length > 0 && (
          <Field label="Branch">
            <select value={form.branchId} onChange={(e) => setForm({ ...form, branchId: e.target.value })}>
              <option value="">All branches</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </Field>
        )}
        <Button type="submit" variant="primary" busy={add.isPending}>
          Add to team
        </Button>
        <ErrorAlert error={add.error} title="Not added" />
      </form>
    </Card>
  );
}
