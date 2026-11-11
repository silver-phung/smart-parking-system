import { renderHook, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { clearAuthSession, setAuthSession } from "./authSession.js";
import { useImageUrl } from "./useImageUrl.js";

afterEach(() => {
  clearAuthSession();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

it("loads database images with authentication and revokes the blob URL", async () => {
  setAuthSession({ accessToken: "staff-test" });
  const fetchMock = vi.fn(async () => new Response(new Blob(["image"]), { status: 200 }));
  vi.stubGlobal("fetch", fetchMock);
  const create = vi.fn(() => "blob:stored-image");
  const revoke = vi.fn();
  class ImageURL extends URL {}
  ImageURL.createObjectURL = create;
  ImageURL.revokeObjectURL = revoke;
  vi.stubGlobal("URL", ImageURL);
  const url = "/api/v1/images/content/0123456789abcdef0123456789abcdef";
  const { result, unmount } = renderHook(() => useImageUrl(url));
  await waitFor(() => expect(result.current.source).toBe("blob:stored-image"));
  expect(fetchMock.mock.calls[0][0]).toBe(url);
  expect(fetchMock.mock.calls[0][1].headers.Authorization).toBe("Bearer staff-test");
  unmount();
  expect(revoke).toHaveBeenCalledWith("blob:stored-image");
});

it("reports an authorization failure without displaying an image", async () => {
  setAuthSession({ accessToken: "staff-test" });
  vi.stubGlobal("fetch", vi.fn(async () => Response.json({}, { status: 404 })));
  const { result } = renderHook(() =>
    useImageUrl("/api/v1/images/content/0123456789abcdef0123456789abcdef"),
  );
  await waitFor(() => expect(result.current.error).toBe(true));
  expect(result.current.source).toBeNull();
});
