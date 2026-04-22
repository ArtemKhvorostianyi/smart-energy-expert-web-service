# Database Schema (PostgreSQL, MVP)

## 1. ER Model (Textual)

- `roles` 1 --- N `users`
- `users` 1 --- N `experiments` (`created_by`)
- `experiments` 1 --- N `experiment_parameters`
- `criteria` 1 --- N `criterion_weights`
- `experiments` 1 --- N `evaluations`
- `users` 1 --- N `evaluations` (`expert_id`)
- `evaluations` 1 --- 1 `recommendations`
- `users` 1 --- N `audit_logs`

## 2. Tables and Fields

### 2.1 `roles`
- `id` UUID PK
- `name` VARCHAR(32) UNIQUE NOT NULL (`Admin`, `Expert`, `Operator`)
- `created_at` TIMESTAMPTZ NOT NULL

### 2.2 `users`
- `id` UUID PK
- `full_name` VARCHAR(120) NOT NULL
- `email` VARCHAR(120) UNIQUE NOT NULL
- `password_hash` TEXT NOT NULL
- `role_id` UUID NOT NULL FK -> `roles.id`
- `is_active` BOOLEAN NOT NULL DEFAULT TRUE
- `created_at` TIMESTAMPTZ NOT NULL
- `updated_at` TIMESTAMPTZ NOT NULL

### 2.3 `experiments`
- `id` UUID PK
- `title` VARCHAR(200) NOT NULL
- `description` TEXT NULL
- `experiment_type` VARCHAR(80) NOT NULL
- `asset_id` VARCHAR(80) NULL
- `location` VARCHAR(120) NULL
- `status` VARCHAR(24) NOT NULL (`draft`, `submitted`, `evaluated`)
- `created_by` UUID NOT NULL FK -> `users.id`
- `created_at` TIMESTAMPTZ NOT NULL
- `updated_at` TIMESTAMPTZ NOT NULL

### 2.4 `experiment_parameters`
- `id` UUID PK
- `experiment_id` UUID NOT NULL FK -> `experiments.id` ON DELETE CASCADE
- `parameter_name` VARCHAR(120) NOT NULL
- `value` NUMERIC(18,6) NOT NULL
- `unit` VARCHAR(40) NOT NULL
- `measured_at` TIMESTAMPTZ NULL
- `created_at` TIMESTAMPTZ NOT NULL

### 2.5 `criteria`
- `id` UUID PK
- `name` VARCHAR(120) UNIQUE NOT NULL
- `description` TEXT NULL
- `min_value` NUMERIC(18,6) NOT NULL
- `max_value` NUMERIC(18,6) NOT NULL
- `default_weight` NUMERIC(8,4) NOT NULL
- `is_active` BOOLEAN NOT NULL DEFAULT TRUE
- `created_at` TIMESTAMPTZ NOT NULL
- `updated_at` TIMESTAMPTZ NOT NULL

### 2.6 `criterion_weights`
- `id` UUID PK
- `criterion_id` UUID NOT NULL FK -> `criteria.id` ON DELETE CASCADE
- `experiment_type` VARCHAR(80) NOT NULL
- `weight` NUMERIC(8,4) NOT NULL
- `is_active` BOOLEAN NOT NULL DEFAULT TRUE
- `created_at` TIMESTAMPTZ NOT NULL

### 2.7 `evaluations`
- `id` UUID PK
- `experiment_id` UUID NOT NULL FK -> `experiments.id` ON DELETE CASCADE
- `expert_id` UUID NOT NULL FK -> `users.id`
- `integral_score` NUMERIC(8,6) NOT NULL
- `risk_level` VARCHAR(24) NOT NULL (`low`, `moderate`, `high`, `critical`)
- `conclusion` TEXT NULL
- `created_at` TIMESTAMPTZ NOT NULL

### 2.8 `recommendations`
- `id` UUID PK
- `evaluation_id` UUID NOT NULL UNIQUE FK -> `evaluations.id` ON DELETE CASCADE
- `decision_text` TEXT NOT NULL
- `priority` SMALLINT NOT NULL
- `is_expert_adjusted` BOOLEAN NOT NULL DEFAULT FALSE
- `created_at` TIMESTAMPTZ NOT NULL

### 2.9 `audit_logs`
- `id` UUID PK
- `user_id` UUID NULL FK -> `users.id`
- `action` VARCHAR(120) NOT NULL
- `entity_type` VARCHAR(80) NULL
- `entity_id` UUID NULL
- `details` JSONB NULL
- `created_at` TIMESTAMPTZ NOT NULL

## 3. Recommended Indexes

- `users(email)`
- `experiments(created_by, created_at DESC)`
- `experiments(status, created_at DESC)`
- `experiment_parameters(experiment_id)`
- `evaluations(experiment_id, created_at DESC)`
- `evaluations(risk_level, created_at DESC)`
- `audit_logs(user_id, created_at DESC)`
- `audit_logs(action, created_at DESC)`

## 4. Constraints and Rules

1. `criteria.min_value < criteria.max_value`
2. `criteria.default_weight > 0`
3. `criterion_weights.weight > 0`
4. `evaluations.integral_score >= 0 AND evaluations.integral_score <= 1`
5. `recommendations.priority BETWEEN 1 AND 5`
6. One recommendation per evaluation (`UNIQUE(evaluation_id)`).

## 5. SQL Draft (Initial Migration Skeleton)

```sql
create table roles (
  id uuid primary key,
  name varchar(32) not null unique,
  created_at timestamptz not null
);

create table users (
  id uuid primary key,
  full_name varchar(120) not null,
  email varchar(120) not null unique,
  password_hash text not null,
  role_id uuid not null references roles(id),
  is_active boolean not null default true,
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table experiments (
  id uuid primary key,
  title varchar(200) not null,
  description text null,
  experiment_type varchar(80) not null,
  asset_id varchar(80) null,
  location varchar(120) null,
  status varchar(24) not null,
  created_by uuid not null references users(id),
  created_at timestamptz not null,
  updated_at timestamptz not null
);

create table experiment_parameters (
  id uuid primary key,
  experiment_id uuid not null references experiments(id) on delete cascade,
  parameter_name varchar(120) not null,
  value numeric(18,6) not null,
  unit varchar(40) not null,
  measured_at timestamptz null,
  created_at timestamptz not null
);

create table criteria (
  id uuid primary key,
  name varchar(120) not null unique,
  description text null,
  min_value numeric(18,6) not null,
  max_value numeric(18,6) not null,
  default_weight numeric(8,4) not null,
  is_active boolean not null default true,
  created_at timestamptz not null,
  updated_at timestamptz not null,
  constraint ck_criteria_range check (min_value < max_value),
  constraint ck_criteria_weight check (default_weight > 0)
);

create table criterion_weights (
  id uuid primary key,
  criterion_id uuid not null references criteria(id) on delete cascade,
  experiment_type varchar(80) not null,
  weight numeric(8,4) not null check (weight > 0),
  is_active boolean not null default true,
  created_at timestamptz not null
);

create table evaluations (
  id uuid primary key,
  experiment_id uuid not null references experiments(id) on delete cascade,
  expert_id uuid not null references users(id),
  integral_score numeric(8,6) not null check (integral_score >= 0 and integral_score <= 1),
  risk_level varchar(24) not null,
  conclusion text null,
  created_at timestamptz not null
);

create table recommendations (
  id uuid primary key,
  evaluation_id uuid not null unique references evaluations(id) on delete cascade,
  decision_text text not null,
  priority smallint not null check (priority between 1 and 5),
  is_expert_adjusted boolean not null default false,
  created_at timestamptz not null
);

create table audit_logs (
  id uuid primary key,
  user_id uuid null references users(id),
  action varchar(120) not null,
  entity_type varchar(80) null,
  entity_id uuid null,
  details jsonb null,
  created_at timestamptz not null
);
```
