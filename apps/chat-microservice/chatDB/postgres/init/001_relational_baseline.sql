create table chats (
    id uuid primary key,
    title varchar(200),
    type varchar(50) not null
        constraint ck_chats_type check (type in ('Direct', 'Group')),
    created_at timestamptz not null,
    -- "<smallest userId>:<largest userId>" - ensures at most one direct chat per user pair
    direct_key varchar(73),

    constraint ck_chats_direct_key
        check ((type = 'Direct') = (direct_key is not null))
);

create unique index uq_chats_direct_key
    on chats(direct_key)
    where direct_key is not null;
 
create table chat_participants (
    id uuid primary key,
    chat_id uuid not null,
    user_id uuid not null,
    role varchar(50) not null
        constraint ck_chat_participants_role check (role in ('Member', 'Admin', 'Owner')),
    joined_at timestamptz not null,
    left_at timestamptz,
 
    constraint fk_chat_participants_chat
        foreign key (chat_id)
        references chats(id)
        on delete cascade,
 
    constraint uq_chat_participants_chat_user
        unique (chat_id, user_id)
);
 
create table messages (
    id uuid primary key,
    chat_id uuid not null,
    sender_user_id uuid not null,
    content text not null,
    sent_at timestamptz not null,
 
    constraint fk_messages_chat
        foreign key (chat_id)
        references chats(id)
        on delete cascade
);
 
create table message_receipts (
    message_id uuid not null,
    user_id uuid not null,
    delivered_at timestamptz,
    seen_at timestamptz,

    constraint pk_message_receipts
        primary key (message_id, user_id),

    constraint fk_message_receipts_message
        foreign key (message_id)
        references messages(id)
        on delete cascade
);
 
create index ix_chat_participants_user_id
    on chat_participants(user_id);
 
create index ix_messages_chat_id_sent_at_id
    on messages(chat_id, sent_at, id);
 
create index ix_message_receipts_user_id
    on message_receipts(user_id);