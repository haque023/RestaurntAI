import { useBranch } from "./useBranch";

/** Branch selector for pages that work on one branch; hidden for branch-bound staff. */
export function BranchPicker() {
  const { branches, branchId, select, locked } = useBranch();
  if (locked || branches.length <= 1) return null;

  return (
    <label className="field" style={{ minWidth: 200 }}>
      <span>Branch</span>
      <select value={branchId} onChange={(e) => select(e.target.value)}>
        {branches.map((b) => (
          <option key={b.id} value={b.id}>
            {b.name}
          </option>
        ))}
      </select>
    </label>
  );
}
