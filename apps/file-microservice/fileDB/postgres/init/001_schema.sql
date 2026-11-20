-- FileService's own database (database per service). Week 46: the file-message saga.

-- The bytes are stored in the row (bytea), so "delete the file" is part of the same transaction as the quota change.
-- Fine for a demo - real large files would live in blob storage with only the metadata here.
create table files (
    id uuid primary key,
    owner_user_id uuid not null,
    file_name varchar(255) not null,
    content_type varchar(255) not null,
    size_bytes bigint not null
        constraint ck_files_size check (size_bytes > 0),
    content bytea not null,
    status varchar(50) not null
        constraint ck_files_status check (status in ('Temporary', 'Attached')),
    -- The saga that reserved quota for the file. Makes ReserveQuota/ReleaseQuota idempotent per saga.
    saga_id uuid,
    uploaded_at timestamptz not null,

    constraint ck_files_attached_has_saga
        check ((status = 'Attached') = (saga_id is not null))
);

create index ix_files_owner_user_id
    on files(owner_user_id);

-- A copy of the user's tier from UserService (not implemented - seeded here).
-- Users without a row are on the standard tier with Quota:StandardLimitBytes.
create table user_quotas (
    user_id uuid primary key,
    tier varchar(50) not null
        constraint ck_user_quotas_tier check (tier in ('Standard', 'Premium')),
    limit_bytes bigint not null,
    used_bytes bigint not null default 0,

    -- The quota can never be exceeded, even if two reservations race.
    constraint ck_user_quotas_within_limit check (used_bytes >= 0 and used_bytes <= limit_bytes)
);
