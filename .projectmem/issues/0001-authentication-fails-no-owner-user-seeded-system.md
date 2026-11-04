# #0001 Authentication fails: no owner user seeded; system admin expects >=8 users; audit logs missing; order spaces missing.

- 2026-10-21T05:47:10Z `issue`: Authentication fails: no owner user seeded; system admin expects >=8 users; audit logs missing; order spaces missing. [DbInitializer.cs]
- 2026-10-21T06:23:05Z `attempt`: Added SeedTestDataAsync in app test AI Program.cs with 7 staff users, 5 audit logs, and 4 B2B customer orders & order spaces; registered BomRoutingController services and verified 22/22 API tests pass. (worked)
- 2026-10-21T06:23:11Z `fix`: Resolved missing seed data in virtual test harness app test AI without modifying main WMMS DbInitializer.cs; all 22 API tests across Auth, Admin, Order Intake, and Material Breakdown & BOM pass.
