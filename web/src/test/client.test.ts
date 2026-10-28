import { describe, expect, it, vi } from "vitest";
import { ApiError, readSession, request, writeSession } from "@/api/client";


function mockFetch(status: number, body?: unknown) {
  return vi.spyOn(globalThis, "fetch").mockResolvedValue(
    new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { "Content-Type": "application/json" },
    }),
  );
}

describe("request", () => {
  it("sends the stored token", async () => {
    writeSession({ token: "abc", expiresAt: new Date(Date.now() + 60_000).toISOString() });
    const fetch = mockFetch(200, { ok: true });

    await request("/api/auth/me");

    const headers = fetch.mock.calls[0][1]!.headers as Headers;
    expect(headers.get("Authorization")).toBe("Bearer abc");
  });

  it("ignores an expired session", () => {
    writeSession({ token: "old", expiresAt: new Date(Date.now() - 1000).toISOString() });
    expect(readSession()).toBeNull();
  });

  it("turns validation problems into field messages", async () => {
    mockFetch(400, { title: "Validation failed", errors: { password: ["Use at least one letter and one digit."] } });

    const error = (await request("/api/auth/register", { method: "POST", body: "{}" }).catch((e) => e)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(400);
    expect(error.describe()).toBe("Use at least one letter and one digit.");
  });

  it("returns undefined for 204", async () => {
    mockFetch(204);
    await expect(request("/api/movies/1", { method: "DELETE" })).resolves.toBeUndefined();
  });

  it("reports network failures", async () => {
    vi.spyOn(globalThis, "fetch").mockRejectedValue(new TypeError("Failed to fetch"));
    await expect(request("/api/movies")).rejects.toMatchObject({ status: 0 });
  });
});

