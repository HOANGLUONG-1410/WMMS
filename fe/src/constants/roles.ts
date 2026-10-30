export const ROLES = {
  ADMIN: "ADMIN",
  OWNER: "OWNER",
  SALES: "SALES",
  PLANNER: "PLANNER",
  PURCHASING: "PURCHASING",
  WAREHOUSE: "WAREHOUSE",
  LEADER: "LEADER",
  QC: "QC",
} as const;

export type RoleCode = (typeof ROLES)[keyof typeof ROLES];
