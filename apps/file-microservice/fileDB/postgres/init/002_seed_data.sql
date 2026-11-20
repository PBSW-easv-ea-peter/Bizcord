-- Same users as ChatService's seed data. Bob and Dave have no row, so they get the standard tier.
INSERT INTO user_quotas (user_id, tier, limit_bytes, used_bytes)
VALUES
    -- Alice: Premium, 1 GB
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Premium', 1073741824, 0),

    -- Charlie: Standard (20 MB), 19 MB already used - any file over 1 MB is rejected. Handy for demoing the failure path.
    ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'Standard', 20971520, 19922944);
