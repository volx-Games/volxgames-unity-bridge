#!/usr/bin/env node

const path = require("path");

const cwd = process.cwd();
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const serverPath = path.join(repoRoot, "Tools", "unity-mcp-server", "server.js");
const name = process.argv[2] || "unity-bridge";
const bridgeUrl = process.env.UNITY_BRIDGE_URL || "http://127.0.0.1:48761";

const config = {
  command: "node",
  args: [serverPath],
  env: {
    UNITY_BRIDGE_URL: bridgeUrl
  }
};

const encoded = Buffer.from(JSON.stringify(config), "utf8").toString("base64");
const deeplink =
  `cursor://anysphere.cursor-deeplink/mcp/install?name=${encodeURIComponent(name)}` +
  `&config=${encodeURIComponent(encoded)}`;

console.log(`Workspace: ${cwd}`);
console.log(`Server path: ${serverPath}`);
console.log(`Bridge URL: ${bridgeUrl}`);
console.log("");
console.log(deeplink);
