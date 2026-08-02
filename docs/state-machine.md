# Asset state machine

## States

- `InStock`: controlled by a department and available for assignment, transfer, repair, or retirement.
- `Assigned`: has exactly one active assignment. It must be returned before another transition.
- `InRepair`: has an open maintenance record and cannot be assigned.
- `Retired`: terminal. No lifecycle command can make the asset active again.

## Transition invariants

| From | Command | To | Atomic side effects |
|---|---|---|---|
| New | Register/import | `InStock` | Asset and registered/imported event |
| `InStock` | Assign | `Assigned` | Active assignment, custodian department, event |
| `Assigned` | Return | `InStock` | Assignment return timestamp, notes, event |
| `InStock` | Start repair | `InRepair` | Open maintenance row, event |
| `InRepair` | Complete repair | `InStock` | Completed maintenance row, event |
| `InStock` | Retire | `Retired` | Retirement timestamp and reason event |
| `InRepair` | Retire | `Retired` | Retirement timestamp and reason event |

Moving departments is an in-state command allowed only in `InStock`. Warranty and software commands do not change lifecycle state; software installation is rejected for `Retired` assets.

## Why assigned assets must be returned first

Allowing `Assigned → InRepair` or `Assigned → Retired` would combine a custody transfer and another operational decision. Requiring return creates an explicit owner-history boundary and prevents an active assignment from surviving a non-assignable state.

## Adding a transition

1. Add the edge to `AssetStateMachine`.
2. Define the command's child-record and audit-event invariants.
3. Require an expected concurrency version.
4. Add unit coverage for the allowed edge and all newly relevant rejected edges.
5. Add a Testcontainers integration test for the database transaction and constraints.
6. Update this document and the Mermaid diagram in the README.

