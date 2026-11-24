# Least privilege: the app token may read the one secret it needs and nothing else.
path "kv/data/secrets" {
  capabilities = ["read"]
}
