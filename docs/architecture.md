# Architecture Overview

## Layers

1. Presentation layer (`client`)
   - Web interface for Admin, Expert, Operator.
2. Business logic layer (`server`)
   - Experiment processing, evaluation, recommendation generation.
3. Data layer (`PostgreSQL`)
   - Persistent storage for users, experiments, criteria, evaluations.

## Core Modules

- Authentication and authorization (JWT, roles).
- Experiment management.
- Criteria and weights management.
- Evaluation engine.
- Decision recommendation module.
- Reporting and export.
- Audit logging.

## Initial API Groups

- `Auth`
- `Users`
- `Experiments`
- `Evaluations`
- `Reports`

## Evaluation Formula

`IntegralScore = Sum(ParameterScore * CriterionWeight) / Sum(CriterionWeight)`

Risk levels:
- `0.00 - 0.25`: low
- `0.26 - 0.50`: moderate
- `0.51 - 0.75`: high
- `0.76 - 1.00`: critical
