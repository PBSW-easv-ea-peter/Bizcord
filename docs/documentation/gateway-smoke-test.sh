#!/usr/bin/env bash
# Smoke test for the ChatService routes in the API gateway (week 43, tasks 01 and 02).
# Needs: the gateway and ChatService running. Creates its own chat and message, so no local data is assumed.
# Usage: bash docs/documentation/gateway-smoke-test.sh

GATEWAY="${GATEWAY:-http://localhost:8080}"
SERVICE="${SERVICE:-http://localhost:8081}"
USER_ID="${USER_ID:-11111111-1111-1111-1111-111111111111}"
OTHER_USER_ID="22222222-2222-2222-2222-222222222222"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
FAILED=0

# Sets STATUS, BODY and HEADERS for the last request.
call() {
    STATUS=$(curl -sS -o "$TMP/body" -D "$TMP/headers" -w "%{http_code}" "$@")
    BODY=$(cat "$TMP/body")
    HEADERS=$(tr -d '\r' < "$TMP/headers")
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

token() { # role
    curl -sS -X POST "$GATEWAY/faucet" -H "Content-Type: application/json" \
        -d "{\"claims\":{\"sub\":\"$USER_ID\",\"role\":\"$1\"}}" | sed -E 's/.*"value":"([^"]+)".*/\1/'
}

MEMBER="Authorization: Bearer $(token member)"
GUEST="Authorization: Bearer $(token guest)"
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
call -H "$MEMBER" "$GATEWAY/chats/$CHAT/info"
check "GET /chats/{chatId}/info" 200 "$STATUS"
GATEWAY_BODY="$BODY"
call -H "X-User-Id: $USER_ID" "$SERVICE/chats/$CHAT"
[ "$BODY" = "$GATEWAY_BODY" ] && SAME=identical || SAME="different: $BODY"
check "Same body directly on the service" identical "$SAME"

call -H "$MEMBER" "$GATEWAY/chats/$CHAT/messages?limit=1"
check "GET /chats/{chatId}/messages?limit=1" 200 "$STATUS"

call -X PUT -H "$MEMBER" -H "$JSON" -d '{"content":"smoke edited"}' "$GATEWAY/chats/$CHAT/messages/$MESSAGE"
check "PUT .../messages/{messageId} ({everything})" 200 "$STATUS"
call -X POST -H "$MEMBER" "$GATEWAY/chats/$CHAT/messages/$MESSAGE/seen"
check "POST .../messages/{messageId}/seen ({everything})" 204 "$STATUS"
call -H "$MEMBER" "$GATEWAY/chats/$CHAT/messages/$MESSAGE/receipts"
check "GET .../messages/{messageId}/receipts ({everything})" 200 "$STATUS"

call -H "$MEMBER" "$GATEWAY/chats/$CHAT"
check "GET /chats/{chatId} (aggregate)" 200 "$STATUS"
case "$BODY" in
    '{"chat":{'*'"messages":['*) SHAPE=ok ;;
    *) SHAPE="$BODY" ;;
esac
check "Aggregate has chat object and messages array" ok "$SHAPE"

call -X POST -H "$MEMBER" -H "$JSON" -d '{}' "$GATEWAY/chats/hest"
check "POST /chats/hest is rejected by the service" 405 "$STATUS"

echo "== Task 02: authentication and authorization"
for route in "GET /chats/$CHAT/info" "GET /chats/$CHAT/messages" "GET /chats/$CHAT" "PUT /chats/$CHAT/messages/$MESSAGE"; do
    method=${route%% *}; path=${route#* }; url="$GATEWAY$path"
    label=$(echo "$method $path" | sed -e "s/$MESSAGE/{messageId}/" -e "s/$CHAT/{chatId}/")
    call -X "$method" -H "$JSON" -d '{"content":"x"}' "$url";                    check "$label, no token" 401 "$STATUS"
    call -X "$method" -H "$INVALID" -H "$JSON" -d '{"content":"x"}' "$url";      check "$label, invalid token" 401 "$STATUS"
    call -X "$method" -H "$GUEST" -H "$JSON" -d '{"content":"x"}' "$url";        check "$label, role guest" 403 "$STATUS"
done
call -X POST -H "$GUEST" -H "$JSON" -d '{"title":"x"}' "$GATEWAY/chats/group"
check "POST /chats/group, role guest" 403 "$STATUS"

# The other user is not a participant, so the service answers 403 if it sees their id.
call -H "X-User-Id: $OTHER_USER_ID" "$SERVICE/chats/$CHAT"
check "Precondition: other user gets 403 directly" 403 "$STATUS"
call -H "$MEMBER" -H "X-User-Id: $OTHER_USER_ID" "$GATEWAY/chats/$CHAT/info"
check "Forged X-User-Id is overwritten by the token" 200 "$STATUS"

echo
if [ "$FAILED" -eq 0 ]; then echo "All checks passed."; else echo "$FAILED check(s) failed."; fi
exit "$FAILED"
