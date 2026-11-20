# Logging sidecar – design choices

Week 47: collect logs from ChatService and RTC with a Fluentd container, without the services knowing where their logs end up.

## Context

Both services already write structured JSON to stdout (`libs/Bizcord.Logging`, format in `docs/logging-template.json`). Shipping logs is a peripheral concern, so it lives in a separate container instead of in the services. The services can then change their log destination without a code change or redeploy.

## How it works

1. Docker's `json-file` driver writes each container's stdout to `/var/lib/docker/containers/<id>/<id>-json.log`.
2. Services opt in with the label `logging=fluentd`. `logging.options.labels` copies the label into every log line as `attrs.logging`.
3. Fluentd tails all log files (`in_tail`) and runs two filters:
   - `grep` keeps only lines where `attrs.logging == fluentd`. This also stops Fluentd from re-reading its own output (a feedback loop).
   - `parser` turns the Serilog JSON in Docker's `log` field into real fields (`Level`, `Location.Service`, `Tracing.TraceId`, ...).
4. Output is Fluentd's stdout, which stands in for a central log system.

## Choices and trade-offs

| Choice | Why | Cost |
|---|---|---|
| One node-level Fluentd (the lab's setup) | Simple, one container for all services | It is really a node agent (Kubernetes: DaemonSet), not a per-service sidecar. It reads every container's log on the host, not only ours |
| Opt-in label instead of a list of service names | The service declares itself; Fluentd does not need to know service names | A service without the label is silently not collected |
| `user: root` on Fluentd | `/var/lib/docker/containers` is root-only (mode 700) | Fluentd can read every container's log. Mounted `:ro` to limit the damage |
| Pinned image `fluent/fluentd:v1.17-1` | Same behaviour for everyone in the group | Upgrades are manual |
| Position file on a named volume | Fluentd resumes where it left off after `down`/`up` | Lines logged while Fluentd is down are still read on restart, but lines from containers removed in the meantime are lost |
| `read_from_head false` | Old logs from unrelated containers are not replayed at first start | Lines written before Fluentd's first start are not collected |

Fluentd's own warnings include the raw log line. This is one more reason the services log ids only, never message content (GDPR).

## Before Kubernetes

The current setup does not move 1:1 to Kubernetes: a container in a pod cannot read another container's stdout. A real per-pod sidecar needs:

- an extra file sink in `Bizcord.Logging` that writes to a shared `emptyDir` volume (stdout stays for `kubectl logs`)
- a Fluentd container in each pod that tails that file, using the same `fluent.conf` minus the label filter
- no root, because Fluentd then only reads its own pod's volume

## Verify

```bash
docker compose up -d --build
curl -s -X POST localhost:8081/chats/group -H "X-User-Id: 11111111-1111-1111-1111-111111111111" -H "Content-Type: application/json" -d '{"title":"Sidecar test"}'
docker compose logs --tail 5 fluentd   # expect a line with "Service":"ChatService" and "Message":"Group chat created."