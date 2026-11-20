-- Week 46: a message can reference a file in FileService. Name and size are copies,
-- so the message can be shown without calling FileService.
alter table messages
    add column attachment_file_id uuid,
    add column attachment_file_name varchar(255),
    add column attachment_size_bytes bigint,
    add constraint ck_messages_attachment_all_or_nothing
        check ((attachment_file_id is null) = (attachment_file_name is null)
           and (attachment_file_id is null) = (attachment_size_bytes is null));

-- Text is optional when there is a file. A deleted message keeps neither text nor file.
alter table messages
    drop constraint ck_messages_deleted_has_no_content,
    add constraint ck_messages_content_or_attachment
        check (case
                   when deleted_at is null then content is not null or attachment_file_id is not null
                   else content is null and attachment_file_id is null
               end);
