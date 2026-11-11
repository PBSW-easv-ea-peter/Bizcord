#!/usr/bin/env python3
"""Exercise the published gateway against an isolated temporary HTTP downstream."""
import argparse
import base64
import hashlib
import hmac
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import threading
import time
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROOT = Path(__file__).resolve().parents[1]


class Downstream(BaseHTTPRequestHandler):
    def do_GET(self):
        body = json.dumps({"path": self.path, "userId": self.headers.get("X-UserId")}).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *_):
        pass


def free_port():
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def request(url, data=None, token=None, headers=None):
    values = dict(headers or {})
    if token:
        values["Authorization"] = "Bearer " + token
    if data is not None:
        values["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=json.dumps(data).encode() if data is not None else None, headers=values)
    try:
        response = urllib.request.urlopen(req, timeout=5)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        body = response.read().decode()
        try:
            body = json.loads(body)
        except json.JSONDecodeError:
            pass
        return response.status, body


def check(condition, label):
    if not condition:
        raise AssertionError(label)
    print("PASS", label, flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--docker-image", help="Test this built image on Linux using host networking")
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix="api-gateway-smoke-") as folder:
        app = Path(folder) / "publish"
        subprocess.run([args.dotnet, "publish", str(ROOT / "src/api-gateway.csproj"), "-c", "Release", "-o", str(app)], check=True)
        downstream = ThreadingHTTPServer(("127.0.0.1", 0), Downstream)
        threading.Thread(target=downstream.serve_forever, daemon=True).start()
        port = free_port()
        base = "http://127.0.0.1:" + str(port)
        config = app / "Configuration"
        for file in config.glob("ocelot.*.json"):
            file.unlink()
        (config / "ocelot.global.json").write_text(json.dumps({"GlobalConfiguration": {"BaseUrl": base}}))

        def route(upstream, path, protected=False, scope=False, role=False, header=False):
            value = {"UpstreamPathTemplate": upstream, "UpstreamHttpMethod": ["GET"],
                     "DownstreamPathTemplate": path, "DownstreamScheme": "http",
                     "DownstreamHostAndPorts": [{"Host": "127.0.0.1", "Port": downstream.server_port}]}
            if protected:
                value["AuthenticationOptions"] = {"AuthenticationProviderKeys": ["Bearer"], "AllowedScopes": ["channel.read"] if scope else []}
            if role:
                value["RouteClaimsRequirement"] = {"role": "admin"}
            if header:
                value["AddHeadersToRequest"] = {"X-UserId": "Claims[sub] > value"}
            return value

        paths = [route("/public/{id}", "/api/example/{id}"),
                 route("/protected", "/protected", protected=True, header=True),
                 route("/scoped", "/scoped", protected=True, scope=True),
                 route("/admin", "/admin", protected=True, role=True)]
        identity = route("/me", "/api/users/{subjectId}", protected=True)
        identity["ChangeDownstreamPathTemplate"] = {"subjectId": "Claims[sub] > value"}
        paths.append(identity)
        routes_file = config / "ocelot.smoke.json"
        routes_file.write_text(json.dumps({"Routes": paths}))
        environment = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development", ASPNETCORE_URLS=base)
        process = None
        log_path = Path(folder) / "gateway.log"
        log = log_path.open("w")
        container_name = "api-gateway-smoke-" + str(os.getpid())

        def start():
            command = [args.dotnet, str(app / "Bizcord.ApiGateway.dll")]
            if args.docker_image:
                command = ["docker", "run", "--rm", "--name", container_name, "--network", "host",
                           "-e", "ASPNETCORE_ENVIRONMENT=Development", "-e", "ASPNETCORE_URLS=" + base,
                           "-v", str(config) + ":/app/Configuration", args.docker_image]
            running = subprocess.Popen(command, cwd=app, env=environment, stdout=log, stderr=subprocess.STDOUT)
            deadline = time.monotonic() + 30
            while time.monotonic() < deadline:
                if running.poll() is not None:
                    raise RuntimeError(log_path.read_text())
                try:
                    if request(base + "/swagger/v1/swagger.json")[0] == 200:
                        return running
                except (OSError, urllib.error.URLError):
                    pass
                time.sleep(0.1)
            stop(running)
            raise RuntimeError("Gateway startup timed out\n" + log_path.read_text())

        def stop(running):
            if running is not None and running.poll() is None:
                if args.docker_image:
                    subprocess.run(["docker", "stop", "--time", "2", container_name], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=15)
                else:
                    running.terminate()
                running.wait(timeout=10)

        def token(claims):
            status, body = request(base + "/faucet", {"claims": claims})
            check(status == 200 and isinstance(body.get("value"), str), "faucet contract")
            return body["value"]

        try:
            process = start()
            check(request(base + "/swagger/index.html")[0] == 200, "Swagger UI")
            status, body = request(base + "/public/42")
            check(status == 200 and body["path"] == "/api/example/42", "unprotected route and path substitution")
            check(request(base + "/protected")[0] == 401, "missing token rejected")
            check(request(base + "/protected", token="invalid.token.value")[0] == 401, "invalid token rejected")
            member = token({"sub": "student-123", "role": "member", "scope": "channel.read"})
            status, body = request(base + "/protected", token=member, headers={"X-UserId": "spoofed-client-id"})
            check(status == 200 and body["userId"] == "student-123", "sub preserved and spoofed identity header replaced")
            check(request(base + "/scoped", token=member)[0] == 200, "allowed scope")
            wrong_scope = token({"sub": "student-123", "scope": "channel.write"})
            check(request(base + "/scoped", token=wrong_scope)[0] == 403, "disallowed scope rejected")
            check(request(base + "/admin", token=member)[0] == 403, "disallowed role rejected")
            admin = token({"sub": "teacher-1", "role": "admin"})
            check(request(base + "/admin", token=admin)[0] == 200, "allowed role")
            status, body = request(base + "/me", token=member)
            check(status == 200 and body["path"] == "/api/users/student-123", "sub transformed into downstream path")
            encode = lambda value: base64.urlsafe_b64encode(json.dumps(value).encode()).rstrip(b"=")
            unsigned = encode({"alg": "HS256", "typ": "JWT"}) + b"." + encode({"sub": "expired", "exp": int(time.time()) - 60})
            secret = json.loads((app / "appsettings.Development.json").read_text())["AuthenticationProviderKey"]
            signature = base64.urlsafe_b64encode(hmac.new(secret.encode(), unsigned, hashlib.sha256).digest()).rstrip(b"=")
            expired = (unsigned + b"." + signature).decode()
            check(request(base + "/protected", token=expired)[0] == 401, "expired token rejected")
            stop(process)
            paths.append(route("/after-restart", "/new-config"))
            routes_file.write_text(json.dumps({"Routes": paths}))
            process = start()
            status, body = request(base + "/after-restart")
            check(status == 200 and body["path"] == "/new-config", "configuration files merged after restart")
        except Exception:
            print(log_path.read_text())
            raise
        finally:
            stop(process)
            downstream.shutdown()
            downstream.server_close()
            log.close()
    print("All smoke checks passed.", flush=True)


if __name__ == "__main__":
    main()
