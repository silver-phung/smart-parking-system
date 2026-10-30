import { afterEach, vi } from "vitest";
import { cleanup } from "@testing-library/react";

process.env.DEBUG_PRINT_LIMIT = "1200";
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});
URL.createObjectURL = vi.fn(() => "blob:staff-test-photo");
URL.revokeObjectURL = vi.fn();
Element.prototype.scrollIntoView = vi.fn();
