import type { Branch } from "@restaurant-ai/api-client";
import { useApi } from "@restaurant-ai/auth";
import { Button, Card, ErrorAlert, Field } from "@restaurant-ai/ui";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { isOvernight, toDayRows, toPeriods, validateRows, type DayHours } from "./hours";

/** Edits a branch's public details: address, map location and weekly opening hours. */
export function BranchEditor({ branch, timeZone, onDone }: { branch: Branch; timeZone: string; onDone: () => void }) {
  const api = useApi();
  const queryClient = useQueryClient();
  const [name, setName] = useState(branch.name);
  const [address, setAddress] = useState(branch.address);
  const [lat, setLat] = useState(branch.latitude?.toString() ?? "");
  const [lng, setLng] = useState(branch.longitude?.toString() ?? "");
  const [rows, setRows] = useState<DayHours[]>(() => toDayRows(branch.openingHours));
  const [formError, setFormError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: () =>
      api.restaurants.updateBranch(branch.id, {
        name: name.trim(),
        address: address.trim(),
        latitude: lat === "" ? null : Number(lat),
        longitude: lng === "" ? null : Number(lng),
        openingHours: toPeriods(rows),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["restaurant"] });
      onDone();
    },
  });

  function fillCurrentLocation() {
    navigator.geolocation?.getCurrentPosition(
      (p) => {
        setLat(p.coords.latitude.toFixed(6));
        setLng(p.coords.longitude.toFixed(6));
      },
      () => setFormError("Your location could not be read. Enter the coordinates manually."),
    );
  }

  function updateRow(index: number, patch: Partial<DayHours>) {
    setRows((current) => current.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  function submit(event: FormEvent) {
    event.preventDefault();
    setFormError(null);
    if ((lat === "") !== (lng === "")) {
      setFormError("Enter both latitude and longitude, or leave both empty.");
      return;
    }
    const hoursError = validateRows(rows);
    if (hoursError) {
      setFormError(hoursError);
      return;
    }
    save.mutate();
  }

  return (
    <Card>
      <h2>Edit {branch.name}</h2>
      <form className="stack" onSubmit={submit}>
        <div className="form-grid">
          <Field label="Name">
            <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
          </Field>
          <Field label="Address">
            <input value={address} onChange={(e) => setAddress(e.target.value)} maxLength={500} />
          </Field>
        </div>
        <div className="form-grid" style={{ alignItems: "end" }}>
          <Field label="Latitude">
            <input type="number" step="any" min={-90} max={90} value={lat} onChange={(e) => setLat(e.target.value)} placeholder="23.7925" />
          </Field>
          <Field label="Longitude">
            <input type="number" step="any" min={-180} max={180} value={lng} onChange={(e) => setLng(e.target.value)} placeholder="90.4078" />
          </Field>
          <div>
            <Button onClick={fillCurrentLocation}>Use my current location</Button>
          </div>
        </div>
        <p className="small muted">A location lets guests find this branch with “Near me”.</p>

        <fieldset className="stack" style={{ border: 0, padding: 0, margin: 0 }}>
          <legend className="small muted" style={{ marginBottom: 8 }}>
            Opening hours ({timeZone}). A closing time earlier than the opening time runs past midnight.
          </legend>
          <div className="table-wrap">
            <table>
              <tbody>
                {rows.map((row, index) => (
                  <tr key={row.day}>
                    <th scope="row" style={{ textTransform: "none", fontSize: "0.9rem", width: 120 }}>
                      {row.day}
                    </th>
                    <td>
                      <label className="row small">
                        <input type="checkbox" checked={row.open} onChange={(e) => updateRow(index, { open: e.target.checked })} />
                        Open
                      </label>
                    </td>
                    <td>
                      <input type="time" aria-label={`${row.day} opens`} value={row.opens} disabled={!row.open} onChange={(e) => updateRow(index, { opens: e.target.value })} />
                    </td>
                    <td>
                      <input type="time" aria-label={`${row.day} closes`} value={row.closes} disabled={!row.open} onChange={(e) => updateRow(index, { closes: e.target.value })} />
                    </td>
                    <td className="small muted">{isOvernight(row) ? "until next day" : ""}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </fieldset>

        {formError && (
          <p className="alert alert-error" role="alert">
            {formError}
          </p>
        )}
        <ErrorAlert error={save.error} title="Branch not saved" />
        <div className="row">
          <Button type="submit" variant="primary" busy={save.isPending}>
            Save branch
          </Button>
          <Button variant="ghost" onClick={onDone}>
            Cancel
          </Button>
        </div>
      </form>
    </Card>
  );
}
