# Technical Specification (MVP)

## 1. System Name

Smart Energy Expert Web Service.

## 2. Goal

Develop a web service for collection, storage, processing, and expert evaluation of experimental data to support decision-making in cyber-physical energy systems.

## 3. Scope

The system supports a decision support workflow:
1. Capture experiment metadata and parameter measurements.
2. Apply weighted multi-criteria evaluation.
3. Classify risk level.
4. Generate a recommendation.
5. Present evaluation history and basic reports.

## 4. User Roles

1. `Admin`:
   - manages users and roles;
   - manages criteria templates and thresholds;
   - reviews audit logs.
2. `Expert`:
   - creates and runs evaluations;
   - adjusts criteria weights within allowed rules;
   - validates and finalizes recommendations.
3. `Operator`:
   - creates experiments and enters/imports measurements;
   - views own and allowed experiment results;
   - exports basic reports.

## 5. Input and Output Data

### 5.1 Input Data

- experiment metadata (`title`, `type`, `date`, `author`, `description`);
- object context (`asset id`, `location`, `operating mode`);
- measurement parameters (`name`, `value`, `unit`, `timestamp`);
- criteria and weights (`criterion`, `weight`, `limits`);
- optional expert comments.

Input channels:
- manual form entry;
- CSV import;
- optional external API integration (future stage).

### 5.2 Output Data

- integral score (`0..1`);
- risk level (`low`, `moderate`, `high`, `critical`);
- recommendation text and priority;
- evaluation card with details per criterion;
- history of evaluations;
- export file (CSV/PDF on later stage).

## 6. Functional Requirements

### FR-01 Authentication and Authorization
- user registration (admin-controlled in MVP);
- login with JWT token issuance;
- role-based endpoint protection;
- refresh token support is optional for MVP.

### FR-02 User Management
- create/update/deactivate users;
- assign role (`Admin`, `Expert`, `Operator`);
- list users with filtering.

### FR-03 Experiment Management
- create experiment;
- edit metadata before finalization;
- move status: `draft -> submitted -> evaluated`.

### FR-04 Parameter Data Management
- add/edit/delete parameter records in `draft`;
- validate numeric ranges and required units;
- import from CSV.

### FR-05 Criteria Management
- create/edit criteria definitions;
- set `min`, `max`, default `weight`, and normalization method;
- activate/deactivate criteria.

### FR-06 Evaluation Engine
- normalize parameter scores by criterion rules;
- calculate weighted integral score:
  `IntegralScore = Sum(ParameterScore * CriterionWeight) / Sum(CriterionWeight)`;
- map score to risk level.

### FR-07 Recommendation Generation
- apply rule set by risk interval;
- produce recommendation text and priority;
- allow expert confirmation/edit before final save.

### FR-08 Reporting
- list evaluations with filters by date/risk/object;
- show experiment details with criterion breakdown;
- export summary data (CSV in MVP).

### FR-09 Audit Logging
- persist security and domain events:
  login attempts, role changes, evaluation finalization, recommendation changes.

## 7. Non-Functional Requirements

### NFR-01 Security
- password hashing via ASP.NET Identity;
- JWT authentication;
- RBAC checks on all protected endpoints;
- server-side validation for all write requests.

### NFR-02 Performance
- typical API read responses under 500 ms for paginated lists (normal load);
- support at least 30 concurrent users in MVP environment.

### NFR-03 Reliability
- transactional writes for evaluation finalization;
- graceful error handling with consistent API error model;
- logging for diagnostics and audits.

### NFR-04 Scalability and Maintainability
- layered architecture (`API`, `Application`, `Infrastructure`, `Persistence`);
- EF Core migrations for schema evolution;
- modular services to extend evaluation strategies.

## 8. API Groups (Draft)

- `Auth`: login/logout/profile.
- `Users`: CRUD, role assignment.
- `Experiments`: CRUD, lifecycle transitions.
- `Parameters`: parameter records, CSV import.
- `Criteria`: CRUD and activation.
- `Evaluations`: run calculation, save final result.
- `Recommendations`: rule output and expert adjustments.
- `Reports`: list/export endpoints.
- `Audit`: read-only event logs for admin.

## 9. Acceptance Criteria for MVP

1. Authorized user can create an experiment and add parameters.
2. Expert can run evaluation and receive integral score + risk level.
3. System generates recommendation according to configured intervals.
4. Evaluation result is stored and visible in history.
5. Role restrictions prevent unauthorized actions.
6. Key actions are written to audit log.
