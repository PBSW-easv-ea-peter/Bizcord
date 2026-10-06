-- ============================================================
-- Dummy chat data
-- ============================================================

-- ------------------------------------------------------------
-- Chats
-- ------------------------------------------------------------

INSERT INTO chats (
    id,
    title,
    type,
    created_at,
    direct_key
)
VALUES
    -- Direct chat: Alice <-> Bob
    (
        '11111111-1111-1111-1111-111111111111',
        NULL,
        'Direct',
        '2026-10-01 09:00:00+02',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
    ),

    -- Direct chat: Alice <-> Charlie
    (
        '22222222-2222-2222-2222-222222222222',
        NULL,
        'Direct',
        '2026-10-02 10:30:00+02',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:cccccccc-cccc-cccc-cccc-cccccccccccc'
    ),

    -- Group chat
    (
        '33333333-3333-3333-3333-333333333333',
        'Project Team',
        'Group',
        '2026-10-01 08:00:00+02',
        NULL
    ),

    -- Another group chat
    (
        '44444444-4444-4444-4444-444444444444',
        'Weekend Plans',
        'Group',
        '2026-10-03 14:00:00+02',
        NULL
    );


-- ------------------------------------------------------------
-- Chat participants
-- ------------------------------------------------------------

INSERT INTO chat_participants (
    id,
    chat_id,
    user_id,
    role,
    joined_at,
    left_at
)
VALUES
    -- Alice <-> Bob
    (
        '10000000-0000-0000-0000-000000000001',
        '11111111-1111-1111-1111-111111111111',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Member',
        '2026-10-01 09:00:00+02',
        NULL
    ),
    (
        '10000000-0000-0000-0000-000000000002',
        '11111111-1111-1111-1111-111111111111',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        'Member',
        '2026-10-01 09:00:00+02',
        NULL
    ),

    -- Alice <-> Charlie
    (
        '10000000-0000-0000-0000-000000000003',
        '22222222-2222-2222-2222-222222222222',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Member',
        '2026-10-02 10:30:00+02',
        NULL
    ),
    (
        '10000000-0000-0000-0000-000000000004',
        '22222222-2222-2222-2222-222222222222',
        'cccccccc-cccc-cccc-cccc-cccccccccccc',
        'Member',
        '2026-10-02 10:30:00+02',
        NULL
    ),

    -- Project Team
    (
        '10000000-0000-0000-0000-000000000005',
        '33333333-3333-3333-3333-333333333333',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Owner',
        '2026-10-01 08:00:00+02',
        NULL
    ),
    (
        '10000000-0000-0000-0000-000000000006',
        '33333333-3333-3333-3333-333333333333',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        'Admin',
        '2026-10-01 08:05:00+02',
        NULL
    ),
    (
        '10000000-0000-0000-0000-000000000007',
        '33333333-3333-3333-3333-333333333333',
        'cccccccc-cccc-cccc-cccc-cccccccccccc',
        'Member',
        '2026-10-01 08:10:00+02',
        NULL
    ),
    (
        '10000000-0000-0000-0000-000000000008',
        '33333333-3333-3333-3333-333333333333',
        'dddddddd-dddd-dddd-dddd-dddddddddddd',
        'Member',
        '2026-10-01 08:15:00+02',
        NULL
    ),

    -- Weekend Plans
    (
        '10000000-0000-0000-0000-000000000009',
        '44444444-4444-4444-4444-444444444444',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Owner',
        '2026-10-03 14:00:00+02',
        NULL
    ),
    (
        '10000000-0000-0000-0000-000000000010',
        '44444444-4444-4444-4444-444444444444',
        'cccccccc-cccc-cccc-cccc-cccccccccccc',
        'Member',
        '2026-10-03 14:05:00+02',
        NULL
    ),
    (
        '10000000-0000-0000-0000-000000000011',
        '44444444-4444-4444-4444-444444444444',
        'dddddddd-dddd-dddd-dddd-dddddddddddd',
        'Member',
        '2026-10-03 14:10:00+02',
        NULL
    );


-- ------------------------------------------------------------
-- Messages
-- ------------------------------------------------------------

INSERT INTO messages (
    id,
    chat_id,
    sender_user_id,
    content,
    sent_at
)
VALUES
    -- Alice <-> Bob
    (
        '20000000-0000-0000-0000-000000000001',
        '11111111-1111-1111-1111-111111111111',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Hey Bob! How are you doing?',
        '2026-10-01 09:15:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000002',
        '11111111-1111-1111-1111-111111111111',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        'I am doing great! How about you?',
        '2026-10-01 09:16:30+02'
    ),
    (
        '20000000-0000-0000-0000-000000000003',
        '11111111-1111-1111-1111-111111111111',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Doing good! Are we still meeting tomorrow?',
        '2026-10-01 09:18:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000004',
        '11111111-1111-1111-1111-111111111111',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        'Absolutely. See you at 10!',
        '2026-10-01 09:20:00+02'
    ),

    -- Alice <-> Charlie
    (
        '20000000-0000-0000-0000-000000000005',
        '22222222-2222-2222-2222-222222222222',
        'cccccccc-cccc-cccc-cccc-cccccccccccc',
        'Did you finish the report?',
        '2026-10-02 11:00:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000006',
        '22222222-2222-2222-2222-222222222222',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Almost! I just need to make a few changes.',
        '2026-10-02 11:04:00+02'
    ),

    -- Project Team
    (
        '20000000-0000-0000-0000-000000000007',
        '33333333-3333-3333-3333-333333333333',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Welcome everyone to the project chat!',
        '2026-10-01 08:30:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000008',
        '33333333-3333-3333-3333-333333333333',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        'Thanks! What should we work on first?',
        '2026-10-01 08:32:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000009',
        '33333333-3333-3333-3333-333333333333',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Let us start with the database design.',
        '2026-10-01 08:35:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000010',
        '33333333-3333-3333-3333-333333333333',
        'cccccccc-cccc-cccc-cccc-cccccccccccc',
        'Sounds good. I can take care of the schema.',
        '2026-10-01 08:37:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000011',
        '33333333-3333-3333-3333-333333333333',
        'dddddddd-dddd-dddd-dddd-dddddddddddd',
        'I will look into the API requirements.',
        '2026-10-01 08:40:00+02'
    ),

    -- Weekend Plans
    (
        '20000000-0000-0000-0000-000000000012',
        '44444444-4444-4444-4444-444444444444',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        'Anyone up for hiking this weekend?',
        '2026-10-03 14:30:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000013',
        '44444444-4444-4444-4444-444444444444',
        'cccccccc-cccc-cccc-cccc-cccccccccccc',
        'Definitely! What route were you thinking?',
        '2026-10-03 14:32:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000014',
        '44444444-4444-4444-4444-444444444444',
        'dddddddd-dddd-dddd-dddd-dddddddddddd',
        'I am in too. Somewhere with a nice view would be great.',
        '2026-10-03 14:35:00+02'
    );


-- ------------------------------------------------------------
-- Message receipts
-- ------------------------------------------------------------

INSERT INTO message_receipts (
    message_id,
    user_id,
    delivered_at,
    seen_at
)
VALUES

    -- Message 1: Alice -> Bob
    (
        '20000000-0000-0000-0000-000000000001',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        '2026-10-01 09:15:01+02',
        '2026-10-01 09:15:30+02'
    ),

    -- Message 2: Bob -> Alice
    (
        '20000000-0000-0000-0000-000000000002',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        '2026-10-01 09:16:31+02',
        '2026-10-01 09:17:00+02'
    ),

    -- Message 3: Alice -> Bob, delivered but not seen
    (
        '20000000-0000-0000-0000-000000000003',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        '2026-10-01 09:18:01+02',
        NULL
    ),

    -- Message 4: Bob -> Alice
    (
        '20000000-0000-0000-0000-000000000004',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        '2026-10-01 09:20:01+02',
        '2026-10-01 09:21:00+02'
    ),

    -- Message 5: Charlie -> Alice
    (
        '20000000-0000-0000-0000-000000000005',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        '2026-10-02 11:00:01+02',
        '2026-10-02 11:02:00+02'
    ),

    -- Message 6: Alice -> Charlie, delivered but not seen
    (
        '20000000-0000-0000-0000-000000000006',
        'cccccccc-cccc-cccc-cccc-cccccccccccc',
        '2026-10-02 11:04:01+02',
        NULL
    ),

    -- Project Team message 7
    (
        '20000000-0000-0000-0000-000000000007',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        '2026-10-01 08:30:01+02',
        '2026-10-01 08:31:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000007',
        'cccccccc-cccc-cccc-cccc-cccccccccccc',
        '2026-10-01 08:30:01+02',
        '2026-10-01 08:33:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000007',
        'dddddddd-dddd-dddd-dddd-dddddddddddd',
        '2026-10-01 08:30:02+02',
        NULL
    ),

    -- Project Team message 8
    (
        '20000000-0000-0000-0000-000000000008',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        '2026-10-01 08:32:01+02',
        '2026-10-01 08:34:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000008',
        'cccccccc-cccc-cccc-cccc-cccccccccccc',
        '2026-10-01 08:32:01+02',
        '2026-10-01 08:34:30+02'
    ),
    (
        '20000000-0000-0000-0000-000000000008',
        'dddddddd-dddd-dddd-dddd-dddddddddddd',
        '2026-10-01 08:32:02+02',
        NULL
    ),

    -- Project Team message 9
    (
        '20000000-0000-0000-0000-000000000009',
        'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        '2026-10-01 08:35:01+02',
        '2026-10-01 08:36:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000009',
        'cccccccc-cccc-cccc-cccc-cccccccccccc',
        '2026-10-01 08:35:01+02',
        NULL
    ),
    (
        '20000000-0000-0000-0000-000000000009',
        'dddddddd-dddd-dddd-dddd-dddddddddddd',
        '2026-10-01 08:35:02+02',
        NULL
    ),

    -- Weekend Plans message 12
    (
        '20000000-0000-0000-0000-000000000012',
        'cccccccc-cccc-cccc-cccc-cccccccccccc',
        '2026-10-03 14:30:01+02',
        '2026-10-03 14:31:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000012',
        'dddddddd-dddd-dddd-dddd-dddddddddddd',
        '2026-10-03 14:30:01+02',
        NULL
    ),

    -- Weekend Plans message 13
    (
        '20000000-0000-0000-0000-000000000013',
        'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        '2026-10-03 14:32:01+02',
        '2026-10-03 14:33:00+02'
    ),
    (
        '20000000-0000-0000-0000-000000000013',
        'dddddddd-dddd-dddd-dddd-dddddddddddd',
        '2026-10-03 14:32:01+02',
        '2026-10-03 14:34:00+02'
    );
