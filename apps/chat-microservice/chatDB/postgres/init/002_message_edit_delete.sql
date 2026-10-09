-- PUT/DELETE on messages (week 38, task 03). Soft delete: content is removed, the row stays,
-- so paging (sent_at, id) and receipts still work.
alter table messages
    alter column content drop not null,
    add column edited_at timestamptz,
    add column deleted_at timestamptz,
    add constraint ck_messages_deleted_has_no_content
        check ((deleted_at is null) = (content is not null));
