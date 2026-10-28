import { PlatformRoles } from "@restaurant-ai/api-client";
import type { DevPersona } from "@restaurant-ai/auth";

export const adminPersona: DevPersona = {
  label: "Platform administrator",
  roles: [PlatformRoles.PlatformAdministrator],
  permissions: [],
  needsTenant: false,
};
