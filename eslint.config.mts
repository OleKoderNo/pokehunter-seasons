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
    // Check the tooling scripts and this TypeScript configuration.
    files: ["scripts/**/*.ts", "eslint.config.mts"],
    extends: [js.configs.recommended, tseslint.configs.recommended],
    languageOptions: {
      globals: globals.node,
    },
    rules: {
      // Explicitly mark imports that are only used as TypeScript types.
      "@typescript-eslint/consistent-type-imports": [
        "error",
        { prefer: "type-imports" },
      ],
    },
  },

  // Keep this last to disable rules that conflict with Prettier.
  prettier,
]);
