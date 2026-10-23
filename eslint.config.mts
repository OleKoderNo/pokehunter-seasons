import js from "@eslint/js";
import { defineConfig } from "eslint/config";
import prettier from "eslint-config-prettier";
import globals from "globals";
import tseslint from "typescript-eslint";

export default defineConfig([
  {
    // Exclude dependencies, generated output, and local runtime data.
    ignores: [
      "**/node_modules/**",
      "**/dist/**",
      "**/.cache/**",
      "**/cache/**",
      "**/runtime/**",
      "**/backups/**",
      "**/bin/**",
      "**/obj/**",
    ],
  },
  {
    // Shared rules for Node tooling and browser overlay source.
    files: ["scripts/**/*.ts", "overlay/src/**/*.ts", "eslint.config.mts"],
    extends: [js.configs.recommended, tseslint.configs.recommended],
    rules: {
      // Imports used only as types must be explicitly marked.
      "@typescript-eslint/consistent-type-imports": [
        "error",
        { prefer: "type-imports" },
      ],
    },
  },
  {
    // Importers, the local server, tests, and ESLint configuration run in Node.
    files: ["scripts/**/*.ts", "eslint.config.mts"],
    languageOptions: {
      globals: globals.node,
    },
  },
  {
    // The overlay runs inside the OBS browser source.
    files: ["overlay/src/**/*.ts"],
    languageOptions: {
      globals: globals.browser,
    },
  },

  // Keep this last to disable rules that conflict with Prettier.
  prettier,
]);
