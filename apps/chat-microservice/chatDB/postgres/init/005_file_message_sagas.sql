-- Week 46: saga "send a message with a file". See docs/documentation/saga-file-attachment.md.

-- Saga state. ChatService is the orchestrator, so the state lives here - one row per saga instance.
create table file_message_sagas (
    saga_id uuid primary key,
    chat_id uuid not null,
    sender_user_id uuid not null,
    file_id uuid not null,
    content text,
    status varchar(50) not null
        constraint ck_file_message_sagas_status
            check (status in ('AwaitingQuota', 'Completed', 'Compensating', 'Failed', 'CompensationFailed')),
    message_id uuid,
    failure_reason text,
    compensation_attempts int not null default 0,
    started_at timestamptz not null,
    updated_at timestamptz not null,

    constraint fk_file_message_sagas_chat
        foreign key (chat_id)
        references chats(id)
        on delete cascade
);

-- The timeout job looks for sagas that have waited too long in a non-final status.
create index ix_file_message_sagas_waiting
    on file_message_sagas(updated_at)
    where status in ('AwaitingQuota', 'Compensating');
