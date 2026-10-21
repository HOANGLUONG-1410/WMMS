# projectmem - WMMS

_Last updated: 2026-10-21_

## Project purpose
Replace this placeholder with a concise description of what this project does, who it serves, and the main technologies or runtime assumptions.

## Recent issues
- No issues logged yet.

## Decisions
- Implement Order Intake & Order Space Initialization module (ORD - BF-01, UC-30..UC-35) via OrdersController and OrderIntakeService, and extend AuditLogsController with CSV export (SCR-ADM-02) and SystemConfig management (UC-15), alongside ReactJS SPA frontend screens for Section III.1, III.2, III.3, and III.4.

## Notes
- Order Intake staging validation (BR-ORD-01, BR-ORD-02, BR-ORD-03) strips Container No, Shipping Port, PI, and Unit Prices (CBD), highlights invalid dimensions/ETD/quantities as red cells, and blocks ConfirmSalesOrder until ErrorCellCount == 0.

## Key files
- `UC-30..UC`
- `III.1`
- `III.2`
- `III.3`
- `III.4`

## Open questions
- None logged yet.
