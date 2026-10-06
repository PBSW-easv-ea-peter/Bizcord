-- Dummy-data til lokal udvikling og manuel test. Køres IKKE automatisk og ikke i testene.
--   docker compose -f chatDB/compose.yaml --profile seed up seed      (fra apps/chat-context)
--   docker compose --profile seed up seed                             (fra repo-roden)
-- Idempotent: kan køres igen uden fejl (on conflict do nothing). Tidspunkter er relative til nu.
--
-- Brugere (ejes af UserService - her blot faste id'er, brug dem som X-User-Id):
--   Alice 11111111-1111-1111-1111-111111111111
--   Bob   22222222-2222-2222-2222-222222222222
--   Carol 33333333-3333-3333-3333-333333333333
--   Dave  44444444-4444-4444-4444-444444444444
--
-- Chats:
--   aaaaaaaa-0000-0000-0000-000000000001  Group "Team"        Alice Owner, Bob Admin, Carol Member, Dave har forladt den
--   aaaaaaaa-0000-0000-0000-000000000002  Direct Alice/Bob
--   aaaaaaaa-0000-0000-0000-000000000003  Direct Alice/Carol  (ingen beskeder endnu)
--   aaaaaaaa-0000-0000-0000-000000000004  Group "Fredagsbar"  Carol Owner, Bob Member - Alice er IKKE med (test 403)
--
-- Beskeder i "Team" dækker: almindelig, redigeret (bbbb...03), slettet (bbbb...05), sendt af Dave før han gik (bbbb...02),
-- og receipts i alle tilstande (kun leveret, set, ingen).

begin;

insert into chats (id, title, type, created_at, direct_key) values
    ('aaaaaaaa-0000-0000-0000-000000000001', 'Team',       'Group',  now() - interval '7 days', null),
    ('aaaaaaaa-0000-0000-0000-000000000002', null,         'Direct', now() - interval '3 days',
        '11111111-1111-1111-1111-111111111111:22222222-2222-2222-2222-222222222222'),
    ('aaaaaaaa-0000-0000-0000-000000000003', null,         'Direct', now() - interval '1 day',
        '11111111-1111-1111-1111-111111111111:33333333-3333-3333-3333-333333333333'),
    ('aaaaaaaa-0000-0000-0000-000000000004', 'Fredagsbar', 'Group',  now() - interval '2 days', null)
on conflict do nothing;

insert into chat_participants (id, chat_id, user_id, role, joined_at, left_at) values
    -- Team
    ('cccccccc-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111', 'Owner',  now() - interval '7 days', null),
    ('cccccccc-0000-0000-0000-000000000002', 'aaaaaaaa-0000-0000-0000-000000000001', '22222222-2222-2222-2222-222222222222', 'Admin',  now() - interval '7 days', null),
    ('cccccccc-0000-0000-0000-000000000003', 'aaaaaaaa-0000-0000-0000-000000000001', '33333333-3333-3333-3333-333333333333', 'Member', now() - interval '6 days', null),
    ('cccccccc-0000-0000-0000-000000000004', 'aaaaaaaa-0000-0000-0000-000000000001', '44444444-4444-4444-4444-444444444444', 'Member', now() - interval '6 days', now() - interval '1 day'),
    -- Direct Alice/Bob
    ('cccccccc-0000-0000-0000-000000000005', 'aaaaaaaa-0000-0000-0000-000000000002', '11111111-1111-1111-1111-111111111111', 'Member', now() - interval '3 days', null),
    ('cccccccc-0000-0000-0000-000000000006', 'aaaaaaaa-0000-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222', 'Member', now() - interval '3 days', null),
    -- Direct Alice/Carol
    ('cccccccc-0000-0000-0000-000000000007', 'aaaaaaaa-0000-0000-0000-000000000003', '11111111-1111-1111-1111-111111111111', 'Member', now() - interval '1 day', null),
    ('cccccccc-0000-0000-0000-000000000008', 'aaaaaaaa-0000-0000-0000-000000000003', '33333333-3333-3333-3333-333333333333', 'Member', now() - interval '1 day', null),
    -- Fredagsbar
    ('cccccccc-0000-0000-0000-000000000009', 'aaaaaaaa-0000-0000-0000-000000000004', '33333333-3333-3333-3333-333333333333', 'Owner',  now() - interval '2 days', null),
    ('cccccccc-0000-0000-0000-000000000010', 'aaaaaaaa-0000-0000-0000-000000000004', '22222222-2222-2222-2222-222222222222', 'Member', now() - interval '2 days', null)
on conflict do nothing;

insert into messages (id, chat_id, sender_user_id, content, sent_at, edited_at, deleted_at) values
    -- Team
    ('bbbbbbbb-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111',
        'Velkommen til Team-chatten!', now() - interval '6 days', null, null),
    ('bbbbbbbb-0000-0000-0000-000000000002', 'aaaaaaaa-0000-0000-0000-000000000001', '44444444-4444-4444-4444-444444444444',
        'Tak! Jeg er kun med et par dage.', now() - interval '5 days', null, null),
    ('bbbbbbbb-0000-0000-0000-000000000003', 'aaaaaaaa-0000-0000-0000-000000000001', '22222222-2222-2222-2222-222222222222',
        'Standup er flyttet til kl. 9:30 i morgen.', now() - interval '2 days', now() - interval '2 days' + interval '5 minutes', null),
    ('bbbbbbbb-0000-0000-0000-000000000004', 'aaaaaaaa-0000-0000-0000-000000000001', '33333333-3333-3333-3333-333333333333',
        'Er der nogen, der har set PR #1?', now() - interval '3 hours', null, null),
    ('bbbbbbbb-0000-0000-0000-000000000005', 'aaaaaaaa-0000-0000-0000-000000000001', '33333333-3333-3333-3333-333333333333',
        null, now() - interval '2 hours', null, now() - interval '2 hours' + interval '1 minute'),
    ('bbbbbbbb-0000-0000-0000-000000000006', 'aaaaaaaa-0000-0000-0000-000000000001', '11111111-1111-1111-1111-111111111111',
        'Jeg kigger på den nu.', now() - interval '1 hour', null, null),
    -- Direct Alice/Bob
    ('bbbbbbbb-0000-0000-0000-000000000007', 'aaaaaaaa-0000-0000-0000-000000000002', '11111111-1111-1111-1111-111111111111',
        'Hej Bob, har du tid til at pair'' på RTC i dag?', now() - interval '3 days', null, null),
    ('bbbbbbbb-0000-0000-0000-000000000008', 'aaaaaaaa-0000-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222',
        'Ja, efter frokost 👍', now() - interval '3 days' + interval '10 minutes', null, null),
    ('bbbbbbbb-0000-0000-0000-000000000009', 'aaaaaaaa-0000-0000-0000-000000000002', '11111111-1111-1111-1111-111111111111',
        'Super, jeg booker et lokale.', now() - interval '30 minutes', null, null),
    -- Fredagsbar
    ('bbbbbbbb-0000-0000-0000-000000000010', 'aaaaaaaa-0000-0000-0000-000000000004', '33333333-3333-3333-3333-333333333333',
        'Fredagsbar kl. 15 i kantinen 🍕', now() - interval '1 day', null, null)
on conflict do nothing;

-- Ingen receipt for afsenderen selv (samme regel som domænet).
insert into message_receipts (message_id, user_id, delivered_at, seen_at) values
    -- Team: Alices velkomst er set af alle
    ('bbbbbbbb-0000-0000-0000-000000000001', '22222222-2222-2222-2222-222222222222', now() - interval '6 days', now() - interval '6 days'),
    ('bbbbbbbb-0000-0000-0000-000000000001', '33333333-3333-3333-3333-333333333333', now() - interval '6 days', now() - interval '5 days'),
    ('bbbbbbbb-0000-0000-0000-000000000001', '44444444-4444-4444-4444-444444444444', now() - interval '6 days', now() - interval '5 days'),
    -- Team: Bobs (redigerede) besked - set af Alice, kun leveret til Carol
    ('bbbbbbbb-0000-0000-0000-000000000003', '11111111-1111-1111-1111-111111111111', now() - interval '2 days', now() - interval '2 days'),
    ('bbbbbbbb-0000-0000-0000-000000000003', '33333333-3333-3333-3333-333333333333', now() - interval '2 days', null),
    -- Team: Carols spørgsmål - set af Alice, Bob har ingen receipt endnu
    ('bbbbbbbb-0000-0000-0000-000000000004', '11111111-1111-1111-1111-111111111111', now() - interval '3 hours', now() - interval '1 hour'),
    -- Direct: Bob har set Alices første besked, men ikke den nyeste
    ('bbbbbbbb-0000-0000-0000-000000000007', '22222222-2222-2222-2222-222222222222', now() - interval '3 days', now() - interval '3 days'),
    ('bbbbbbbb-0000-0000-0000-000000000008', '11111111-1111-1111-1111-111111111111', now() - interval '3 days', now() - interval '3 days'),
    ('bbbbbbbb-0000-0000-0000-000000000009', '22222222-2222-2222-2222-222222222222', now() - interval '29 minutes', null)
on conflict do nothing;

commit;
