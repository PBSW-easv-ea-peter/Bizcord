#!/usr/bin/env bash
# Smoke test for the ChatService routes in the API gateway (week 43, tasks 01 and 02; roles as of #33)
# and for East/West security (week 45, task 01; #21).
# Needs: the root docker compose stack running, with Vault set up (README). Creates its own chat and message, so no local data is assumed.
# Usage (from the repository root): bash docs/documentation/gateway-smoke-test.sh

GATEWAY="${GATEWAY:-http://localhost:8080}"
USER_ID="${USER_ID:-11111111-1111-1111-1111-111111111111}"
OTHER_USER_ID="22222222-2222-2222-2222-222222222222"

# ChatService is only reachable inside the compose network (#21). Direct calls run curl in a throwaway
# container on that network. The network is found from the running gateway container.
CHATSERVICE="${CHATSERVICE:-http://chatservice:8081}"
NETWORK="${NETWORK:-$(docker inspect -f '{{range $k, $v := .NetworkSettings.Networks}}{{$k}}{{end}}' "$(docker compose ps -q api-gateway)" 2>/dev/null)}"
CURL_IMAGE="curlimages/curl:8.10.1"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
FAILED=0

# Sets STATUS, BODY and HEADERS for the last request. If the call fails, STATUS is 000 and BODY is empty
# (the files are removed first, so an old body can never be compared by mistake).
call() {
    rm -f "$TMP/body" "$TMP/headers"
    STATUS=$(curl -sS -o "$TMP/body" -D "$TMP/headers" -w "%{http_code}" "$@" 2>/dev/null)
    BODY=$(cat "$TMP/body" 2>/dev/null)
    HEADERS=$(tr -d '\r' < "$TMP/headers" 2>/dev/null)
}

# Like call, but from inside the Docker network, i.e. past the gateway. Sets STATUS and BODY.
inside() {
    local out
    out=$(docker run --rm --network "$NETWORK" "$CURL_IMAGE" -sS -w '\n%{http_code}' "$@" 2>/dev/null)
    STATUS=$(echo "$out" | tail -n1)
    BODY=$(echo "$out" | sed '$d')
}

check() { # label expected actual
    if [ "$2" = "$3" ]; then
        printf "PASS  %-55s %s\n" "$1" "$3"
    else
        printf "FAIL  %-55s expected %s, got %s\n" "$1" "$2" "$3"
        FAILED=$((FAILED + 1))
    fi
}

json_id() { sed -E 's/^\{"id":"([^"]+)".*/\1/'; }

token() { # role [sub]
    curl -sS -X POST "$GATEWAY/faucet" -H "Content-Type: application/json" \
        -d "{\"claims\":{\"sub\":\"${2:-$USER_ID}\",\"role\":\"$1\"}}" | sed -E 's/.*"value":"([^"]+)".*/\1/'
}

if [ -z "$NETWORK" ]; then
    echo "Cannot find the compose network. Run from the repository root with the stack up, or set NETWORK=<project>_default."
    exit 1
fi

MEMBER="Authorization: Bearer $(token member)"
OTHER="Authorization: Bearer $(token member "$OTHER_USER_ID")"
GUEST="Authorization: Bearer $(token guest)"
INTERNAL="Authorization: Bearer $(token internal-service)"
INVALID="Authorization: Bearer abc.def.ghi"
JSON="Content-Type: application/json"

echo "== Setup (through the gateway)"
call -X POST -H "$MEMBER" -H "$JSON" -d '{"title":"smoke-test"}' "$GATEWAY/chats/group"
check "POST /chats/group (member)" 201 "$STATUS"
CHAT=$(echo "$BODY" | json_id)
LOCATION=$(echo "$HEADERS" | grep -i '^location:' | sed -E 's/^[Ll]ocation: //')
check "Location points at the gateway" "$GATEWAY/chats/$CHAT" "$LOCATION"

call -X POST -H "$MEMBER" -H "$JSON" -d '{"content":"smoke"}' "$GATEWAY/chats/$CHAT/messages"
check "POST /chats/{chatId}/messages (member)" 201 "$STATUS"
MESSAGE=$(echo "$BODY" | json_id)

echo "== Task 01: routing"
call -H "$MEMBER" "$GATEWAY/chats/$CHAT"
check "GET /chats/{chatId}" 200 "$STATUS"
GATEWAY_BODY="$BODY"
inside -H "$MEMBER" "$CHATSERVICE/chats/$CHAT"
if [ "$STATUS" != 200 ]; then SAME="no answer from the service ($STATUS)"
elif [ "$BODY" = "$GATEWAY_BODY" ]; then SAME=identical
else SAME="different: $BODY"; fi
check "Same body directly on the service (inside Docker)" identical "$SAME"

call -H "$MEMBER" "$GATEWAY/chats/$CHAT/messages?limit=1"
check "GET /chats/{chatId}/messages?limit=1" 200 "$STATUS"

call -X PUT -H "$MEMBER" -H "$JSON" -d '{"content":"smoke edited"}' "$GATEWAY/chats/$CHAT/messages/$MESSAGE"
check "PUT .../messages/{messageId}" 200 "$STATUS"
call -X POST -H "$MEMBER" "$GATEWAY/chats/$CHAT/messages/$MESSAGE/seen"
check "POST .../messages/{messageId}/seen" 204 "$STATUS"
call -H "$MEMBER" "$GATEWAY/chats/$CHAT/messages/$MESSAGE/receipts"
check "GET .../messages/{messageId}/receipts" 200 "$STATUS"

call -H "$MEMBER" "$GATEWAY/chats/$CHAT/overview"
check "GET /chats/{chatId}/overview (aggregate)" 200 "$STATUS"
case "$BODY" in
    '{"chat":{'*'"messages":['*) SHAPE=ok ;;
    *) SHAPE="$BODY" ;;
esac
check "Aggregate has chat object and messages array" ok "$SHAPE"

call -X POST -H "$MEMBER" -H "$JSON" -d '{}' "$GATEWAY/chats/hest"
check "POST /chats/hest is rejected by the service" 405 "$STATUS"

echo "== Task 02: authentication and authorization"
for route in "GET /chats/$CHAT" "GET /chats/$CHAT/messages" "GET /chats/$CHAT/overview" "PUT /chats/$CHAT/messages/$MESSAGE"; do
    method=${route%% *}; path=${route#* }; url="$GATEWAY$path"
    label=$(echo "$method $path" | sed -e "s/$MESSAGE/{messageId}/" -e "s/$CHAT/{chatId}/")
    call -X "$method" -H "$JSON" -d '{"content":"x"}' "$url";                    check "$label, no token" 401 "$STATUS"
    call -X "$method" -H "$INVALID" -H "$JSON" -d '{"content":"x"}' "$url";      check "$label, invalid token" 401 "$STATUS"
    call -X "$method" -H "$GUEST" -H "$JSON" -d '{"content":"x"}' "$url";        check "$label, role guest" 403 "$STATUS"
    # A service token is not a user: user routes need member (#19, #20).
    call -X "$method" -H "$INTERNAL" -H "$JSON" -d '{"content":"x"}' "$url";     check "$label, role internal-service" 403 "$STATUS"
done
call -X POST -H "$GUEST" -H "$JSON" -d '{"title":"x"}' "$GATEWAY/chats/group"
check "POST /chats/group, role guest" 403 "$STATUS"

# The internal route is the other way round.
call "$GATEWAY/chat/health";                    check "GET /chat/health, no token" 401 "$STATUS"
call -H "$MEMBER" "$GATEWAY/chat/health";       check "GET /chat/health, role member" 403 "$STATUS"
call -H "$INTERNAL" "$GATEWAY/chat/health";     check "GET /chat/health, role internal-service" 200 "$STATUS"

# The other user is not a participant, so ChatService answers 403 if it sees their id.
inside -H "$OTHER" "$CHATSERVICE/chats/$CHAT"
check "Precondition: other user gets 403 directly" 403 "$STATUS"
call -H "$MEMBER" -H "X-User-Id: $OTHER_USER_ID" "$GATEWAY/chats/$CHAT"
check "Forged X-User-Id is overwritten by the token" 200 "$STATUS"

echo "== Week 45: East/West security"
# Only the gateway is published on the host.
for port in 8081 8082 8083; do
    call "http://localhost:$port/health"
    check "localhost:$port is not reachable from the host" 000 "$STATUS"
done
# Past the gateway, ChatService checks the token itself (defense in depth).
inside "$CHATSERVICE/chats/$CHAT";                       check "ChatService directly, no token" 401 "$STATUS"
inside -H "$INVALID" "$CHATSERVICE/chats/$CHAT";         check "ChatService directly, invalid token" 401 "$STATUS"
inside -H "$GUEST" "$CHATSERVICE/chats/$CHAT";           check "ChatService directly, role guest" 403 "$STATUS"
inside -H "$MEMBER" "$CHATSERVICE/chats/$CHAT";          check "ChatService directly, role member" 200 "$STATUS"
inside -H "$MEMBER" "$CHATSERVICE/health";               check "ChatService /health directly, role member" 403 "$STATUS"
inside -H "$INTERNAL" "$CHATSERVICE/health";             check "ChatService /health directly, role internal-service" 200 "$STATUS"
# The service also ignores a forged header: the user comes from the token's sub.
inside -H "$MEMBER" -H "X-User-Id: $OTHER_USER_ID" "$CHATSERVICE/chats/$CHAT"
check "ChatService directly, forged X-User-Id is ignored" 200 "$STATUS"

echo
if [ "$FAILED" -eq 0 ]; then echo "All checks passed."; else echo "$FAILED check(s) failed."; fi
exit "$FAILED"
