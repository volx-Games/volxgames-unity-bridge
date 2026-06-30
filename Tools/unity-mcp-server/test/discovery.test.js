const assert = require("node:assert/strict");
const { spawn } = require("node:child_process");
const fs = require("node:fs");
const http = require("node:http");
const os = require("node:os");
const path = require("node:path");
const test = require("node:test");

const serverScript = path.resolve(__dirname, "..", "server.js");

test("uses UNITY_BRIDGE_URL without registry discovery", async (t) => {
  const tmpHome = makeTempHome(t);
  const bridge = await startBridge(t, {
    projectName: "Explicit",
    projectPath: "/Projects/Explicit",
  });

  const client = startMcp(t, tmpHome, { UNITY_BRIDGE_URL: bridge.url });
  const result = await callTool(client, "unity_health");

  assert.equal(result.projectName, "Explicit");
  assert.equal(result.url, bridge.url);
});

test("rejects explicit Fresnel Unity bridge URLs", async (t) => {
  const tmpHome = makeTempHome(t);
  const bridge = await startBridge(t, {
    projectName: "Fresnel",
    projectPath: "/Projects/Fresnel",
    service: "fresnel-unity-bridge",
  });

  const client = startMcp(t, tmpHome, { UNITY_BRIDGE_URL: bridge.url });
  await assert.rejects(
    () => callTool(client, "unity_health"),
    /points to the Fresnel private bridge/
  );
});

test("auto-discovers a single live registry instance", async (t) => {
  const tmpHome = makeTempHome(t);
  const bridge = await startBridge(t, {
    projectName: "Solo",
    projectPath: "/Projects/Solo",
  });
  writeRegistry(tmpHome, "solo.json", bridge.health);

  const client = startMcp(t, tmpHome);
  const result = await callTool(client, "unity_health");

  assert.equal(result.projectName, "Solo");
  assert.equal(result.projectPath, "/Projects/Solo");
});

test("ignores stale registry entries", async (t) => {
  const tmpHome = makeTempHome(t);
  writeRegistry(tmpHome, "stale.json", {
    projectName: "Stale",
    projectPath: "/Projects/Stale",
    url: "http://127.0.0.1:9",
    port: 9,
  });
  const bridge = await startBridge(t, {
    projectName: "Live",
    projectPath: "/Projects/Live",
  });
  writeRegistry(tmpHome, "live.json", bridge.health);

  const client = startMcp(t, tmpHome);
  const result = await callTool(client, "unity_health");

  assert.equal(result.projectName, "Live");
});

test("ignores non-loopback registry URLs", async (t) => {
  const tmpHome = makeTempHome(t);
  writeRegistry(tmpHome, "remote.json", {
    projectName: "Remote",
    projectPath: "/Projects/Remote",
    url: "http://192.0.2.10:48761",
    port: 48761,
  });
  const bridge = await startBridge(t, {
    projectName: "Local",
    projectPath: "/Projects/Local",
  });
  writeRegistry(tmpHome, "local.json", bridge.health);

  const client = startMcp(t, tmpHome);
  const result = await callTool(client, "unity_health");

  assert.equal(result.projectName, "Local");
});

test("keeps the registry URL authoritative over the health payload URL", async (t) => {
  const tmpHome = makeTempHome(t);
  const bridge = await startBridge(t, {
    projectName: "Authoritative",
    projectPath: "/Projects/Authoritative",
    responseUrl: "http://192.0.2.20:48761",
  });
  writeRegistry(tmpHome, "authoritative.json", bridge.health);

  const client = startMcp(t, tmpHome);
  const result = await callTool(client, "unity_health");

  assert.equal(result.projectName, "Authoritative");
  assert.equal(result.url, "http://192.0.2.20:48761");
});

test("fails closed when multiple live instances exist without a target", async (t) => {
  const tmpHome = makeTempHome(t);
  const first = await startBridge(t, {
    projectName: "First",
    projectPath: "/Projects/First",
  });
  const second = await startBridge(t, {
    projectName: "Second",
    projectPath: "/Projects/Second",
  });
  writeRegistry(tmpHome, "first.json", first.health);
  writeRegistry(tmpHome, "second.json", second.health);

  const client = startMcp(t, tmpHome);
  await assert.rejects(
    () => callTool(client, "unity_health"),
    /Multiple Unity Bridge instances are running/
  );
});

test("auto-discovery rechecks live instances instead of reusing a stale single-instance cache", async (t) => {
  const tmpHome = makeTempHome(t);
  const first = await startBridge(t, {
    projectName: "First",
    projectPath: "/Projects/First",
  });
  writeRegistry(tmpHome, "first.json", first.health);

  const client = startMcp(t, tmpHome);
  const firstResult = await callTool(client, "unity_health");
  assert.equal(firstResult.projectName, "First");

  const second = await startBridge(t, {
    projectName: "Second",
    projectPath: "/Projects/Second",
  });
  writeRegistry(tmpHome, "second.json", second.health);

  await assert.rejects(
    () => callTool(client, "unity_health"),
    /Multiple Unity Bridge instances are running/
  );
});

test("selects the matching project path when multiple live instances exist", async (t) => {
  const tmpHome = makeTempHome(t);
  const first = await startBridge(t, {
    projectName: "First",
    projectPath: "/Projects/First",
  });
  const second = await startBridge(t, {
    projectName: "Second",
    projectPath: "/Projects/Second",
  });
  writeRegistry(tmpHome, "first.json", first.health);
  writeRegistry(tmpHome, "second.json", second.health);

  const client = startMcp(t, tmpHome, { UNITY_BRIDGE_PROJECT_PATH: "/Projects/Second" });
  const result = await callTool(client, "unity_health");

  assert.equal(result.projectName, "Second");
  assert.equal(result.projectPath, "/Projects/Second");
});

test("matches project paths case-insensitively on macOS and Windows", async (t) => {
  if (process.platform !== "darwin" && process.platform !== "win32") {
    t.skip("case-insensitive filesystem path matching is only expected on macOS and Windows");
    return;
  }

  const tmpHome = makeTempHome(t);
  const bridge = await startBridge(t, {
    projectName: "Case Match",
    projectPath: "/Projects/Fresnel",
  });
  writeRegistry(tmpHome, "case-match.json", bridge.health);

  const client = startMcp(t, tmpHome, { UNITY_BRIDGE_PROJECT_PATH: "/projects/fresnel" });
  const result = await callTool(client, "unity_health");

  assert.equal(result.projectName, "Case Match");
});

test("reports a missing targeted project clearly", async (t) => {
  const tmpHome = makeTempHome(t);
  const bridge = await startBridge(t, {
    projectName: "Other",
    projectPath: "/Projects/Other",
  });
  writeRegistry(tmpHome, "other.json", bridge.health);

  const client = startMcp(t, tmpHome, { UNITY_BRIDGE_PROJECT_PATH: "/Projects/Missing" });
  await assert.rejects(
    () => callTool(client, "unity_health"),
    /No live Unity Bridge instance found for UNITY_BRIDGE_PROJECT_PATH=/
  );
});

function makeTempHome(t) {
  const tmpHome = fs.mkdtempSync(path.join(os.tmpdir(), "unity-bridge-test-"));
  t.after(() => fs.rmSync(tmpHome, { recursive: true, force: true }));
  return tmpHome;
}

function registryDirectory(home) {
  if (process.platform === "darwin") {
    return path.join(home, "Library", "Application Support", "VolxGames", "UnityBridge", "instances");
  }

  if (process.platform === "win32") {
    return path.join(home, "AppData", "Roaming", "VolxGames", "UnityBridge", "instances");
  }

  return path.join(home, ".config", "VolxGames", "UnityBridge", "instances");
}

function writeRegistry(home, name, instance) {
  const dir = registryDirectory(home);
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, name), JSON.stringify(instance), "utf8");
}

async function startBridge(t, health) {
  const { responseUrl, ...healthPayload } = health;
  const httpServer = http.createServer((request, response) => {
    if (request.method !== "GET" || request.url !== "/health") {
      response.writeHead(404, { "content-type": "application/json" });
      response.end(JSON.stringify({ ok: false, message: "Not found." }));
      return;
    }

    response.writeHead(200, { "content-type": "application/json" });
    response.end(JSON.stringify({ ok: true, running: true, ...healthPayload, url: responseUrl || bridgeUrl }));
  });

  let bridgeUrl = "";
  await new Promise((resolve) => {
    httpServer.listen(0, "127.0.0.1", () => {
      const address = httpServer.address();
      bridgeUrl = `http://127.0.0.1:${address.port}`;
      resolve();
    });
  });

  t.after(() => httpServer.close());
  return {
    url: bridgeUrl,
    health: {
      ok: true,
      running: true,
      ...healthPayload,
      url: bridgeUrl,
      port: Number(new URL(bridgeUrl).port),
    },
  };
}

function startMcp(t, home, extraEnv = {}) {
  const child = spawn(process.execPath, [serverScript], {
    env: {
      ...process.env,
      HOME: home,
      USERPROFILE: home,
      APPDATA: path.join(home, "AppData", "Roaming"),
      XDG_CONFIG_HOME: path.join(home, ".config"),
      ...extraEnv,
    },
    stdio: ["pipe", "pipe", "pipe"],
  });

  const client = {
    child,
    nextId: 1,
    pending: new Map(),
    stderr: "",
    stdoutBuffer: "",
  };

  child.stdout.setEncoding("utf8");
  child.stderr.setEncoding("utf8");
  child.stdout.on("data", (chunk) => {
    client.stdoutBuffer += chunk;
    const lines = client.stdoutBuffer.split("\n");
    client.stdoutBuffer = lines.pop();
    for (const line of lines) {
      if (line.trim()) {
        handleRpcLine(client, line);
      }
    }
  });
  child.stderr.on("data", (chunk) => {
    client.stderr += chunk;
  });
  child.on("exit", () => {
    for (const pending of client.pending.values()) {
      pending.reject(new Error(`MCP server exited. stderr: ${client.stderr}`));
    }
    client.pending.clear();
  });

  t.after(() => {
    child.stdin.end();
    child.kill();
  });

  return client;
}

async function callTool(client, name) {
  const response = await sendRpc(client, "tools/call", { name, arguments: {} });
  const text = response.content[0].text;
  return JSON.parse(text);
}

function sendRpc(client, method, params) {
  const id = client.nextId++;
  const message = { jsonrpc: "2.0", id, method, params };
  const promise = new Promise((resolve, reject) => {
    client.pending.set(id, { resolve, reject });
  });
  client.child.stdin.write(`${JSON.stringify(message)}\n`);
  return promise;
}

function handleRpcLine(client, line) {
  const message = JSON.parse(line);
  const pending = client.pending.get(message.id);
  if (!pending) {
    return;
  }

  client.pending.delete(message.id);
  if (message.error) {
    pending.reject(new Error(message.error.message));
  } else {
    pending.resolve(message.result);
  }
}
