#!/usr/bin/env node

const path = require("path");

const cwd = process.cwd();
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const serverPath = path.join(repoRoot, "Tools", "unity-mcp-server", "server.js");
const name = process.argv[2] || "unity-bridge";
const bridgeUrl = process.env.UNITY_BRIDGE_URL || "";
const bridgeProjectPath = process.env.UNITY_BRIDGE_PROJECT_PATH || "";

const config = {
  command: "node",
  args: [serverPath]
};

if (bridgeUrl || bridgeProjectPath) {
  config.env = {};
  if (bridgeUrl) {
    config.env.UNITY_BRIDGE_URL = bridgeUrl;
  }

  if (bridgeProjectPath) {
    config.env.UNITY_BRIDGE_PROJECT_PATH = bridgeProjectPath;
  }
}

const encoded = Buffer.from(JSON.stringify(config), "utf8").toString("base64");
const deeplink =
  `cursor://anysphere.cursor-deeplink/mcp/install?name=${encodeURIComponent(name)}` +
  `&config=${encodeURIComponent(encoded)}`;

console.log(`Workspace: ${cwd}`);
console.log(`Server path: ${serverPath}`);
console.log(`Bridge URL: ${bridgeUrl || "(auto-discover)"}`);
console.log(`Bridge project path: ${bridgeProjectPath || "(not set)"}`);
console.log("");
console.log(deeplink);
