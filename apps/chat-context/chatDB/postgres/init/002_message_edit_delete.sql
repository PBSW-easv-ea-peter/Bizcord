-- PUT/DELETE på beskeder (uge 38, task 03). Blød sletning: content fjernes, rækken bliver,
-- så paging (sent_at, id) og receipts stadig virker.
alter table messages
    alter column content drop not null,
    add column edited_at timestamptz,
    add column deleted_at timestamptz,
    add constraint ck_messages_deleted_has_no_content
        check ((deleted_at is null) = (content is not null));
