import { createServer } from "node:http";
import type { ServerResponse } from "node:http";
import { appendFile, mkdir, readFile } from "node:fs/promises";
import { isJsonObject } from "./lib/pokeapi.js";

const HOST = "127.0.0.1";
const PORT = 8787;
const ORIGIN = `http://${HOST}:${PORT}`;
const ROOT = new URL("../", import.meta.url);

const clients = new Set<ServerResponse>();

const files = new Map([
  ["/", { path: "overlay/index.html", type: "text/html" }],
  ["/overlay.css", { path: "overlay/overlay.css", type: "text/css" }],
  ["/overlay.js", { path: "overlay/dist/overlay.js", type: "text/javascript" }],
  ["/config", { path: "config/catch-overlay.json", type: "application/json" }],
]);

function validAsset(value: unknown): boolean {
  if (value === null) return true;
  if (typeof value !== "string") return false;

  try {
    return ["https:", "http:"].includes(new URL(value).protocol);
  } catch {
    return false;
  }
}

function validText(value: unknown): boolean {
  return (
    typeof value === "string" && value.trim().length > 0 && value.length <= 200
  );
}

const server = createServer((request, response) => {
  void (async () => {
    const url = new URL(request.url ?? "/", ORIGIN);

    if (request.headers.host !== `${HOST}:${PORT}`) {
      response.writeHead(403).end("Unexpected host.");
      return;
    }

    if (request.method === "GET" && url.pathname === "/events") {
      response.writeHead(200, {
        "Content-Type": "text/event-stream",
        "Cache-Control": "no-cache",
        Connection: "keep-alive",
      });

      response.write(": connected\n\n");
      clients.add(response);

      const heartbeat = setInterval(() => {
        response.write(": heartbeat\n\n");
      }, 15_000);

      response.on("close", () => {
        clearInterval(heartbeat);
        clients.delete(response);
      });

      return;
    }

    if (request.method === "POST" && url.pathname === "/test-catch") {
      const origin = request.headers.origin;

      if (origin !== undefined && origin !== ORIGIN) {
        response.writeHead(403).end("Unexpected origin.");
        return;
      }

      if (clients.size === 0) {
        response
          .writeHead(409)
          .end("No overlay is connected. Open the OBS source.");
        return;
      }

      const chunks: Buffer[] = [];
      let size = 0;

      for await (const chunk of request) {
        const buffer = Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk);
        size += buffer.length;

        if (size > 32_768) {
          response.writeHead(413).end("Notification too large.");
          return;
        }

        chunks.push(buffer);
      }

      let payload: unknown;

      try {
        payload = JSON.parse(Buffer.concat(chunks).toString("utf8"));
      } catch {
        response.writeHead(400).end("Invalid JSON.");
        return;
      }

      if (
        !isJsonObject(payload) ||
        payload.schemaVersion !== 1 ||
        payload.isTest !== true ||
        payload.login !== "kapteinole" ||
        typeof payload.userId !== "string" ||
        !/^\d+$/.test(payload.userId) ||
        !validText(payload.displayName) ||
        !validText(payload.pokemonName) ||
        typeof payload.isShiny !== "boolean" ||
        !validAsset(payload.profileImageUrl) ||
        !validAsset(payload.spriteUrl) ||
        !isJsonObject(payload.cries) ||
        !validAsset(payload.cries.latest) ||
        !validAsset(payload.cries.legacy)
      ) {
        response.writeHead(400).end("Invalid KapteinOle test notification.");
        return;
      }

      const record = {
        ...payload,
        receivedAtUtc: new Date().toISOString(),
      };

      await mkdir(new URL("runtime/", ROOT), { recursive: true });

      await appendFile(
        new URL("runtime/overlay-tests.jsonl", ROOT),
        `${JSON.stringify(record)}\n`,
        "utf8",
      );

      const encoded = `data: ${JSON.stringify(record)}\n\n`;

      for (const client of clients) {
        client.write(encoded);
      }

      response.writeHead(200, { "Content-Type": "application/json" });
      response.end(
        JSON.stringify({ accepted: true, connections: clients.size }),
      );
      return;
    }

    const file = files.get(url.pathname);

    if (request.method === "GET" && file !== undefined) {
      const contents = await readFile(new URL(file.path, ROOT));

      response.writeHead(200, {
        "Content-Type": `${file.type}; charset=utf-8`,
        "Cache-Control": "no-store",
      });

      response.end(contents);
      return;
    }

    response.writeHead(404).end("Not found.");
  })().catch((error: unknown) => {
    console.error(error);

    if (!response.headersSent) {
      response.writeHead(500).end("Overlay server error. Check the terminal.");
    } else {
      response.end();
    }
  });
});

server.on("error", (error) => {
  console.error("Could not start the overlay server:", error.message);
  process.exitCode = 1;
});

server.listen(PORT, HOST, () => {
  console.log(`Landscape: ${ORIGIN}/`);
  console.log(`Portrait:  ${ORIGIN}/?layout=portrait&audio=0`);
  console.log("Keep this terminal running while testing the overlay.");
});
