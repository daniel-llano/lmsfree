import { test, expect, type Page } from "playwright/test";

async function signIn(page: Page, email: string, password: string) {
  await page.goto("/login");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
}

async function signOut(page: Page) {
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
}

test.describe("LMSFree", () => {
  test("guests are sent to sign in", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
  });

  test("a teacher sets work and grades it, and the student sees the grade", async ({ page }) => {
    const title = `Loops ${Date.now()}`;

    await signIn(page, "teacher@lmsfree.local", "Teacher@1234");
    await page.getByRole("link", { name: /Introduction to Programming/ }).click();
    await page.getByRole("button", { name: "New assignment" }).click();
    await page.getByLabel("Title").fill(title);
    await page.getByLabel("Instructions").fill("Print the numbers 1 to 10.");
    await page.getByLabel("Max points").fill("10");
    await page.getByRole("button", { name: "Create assignment" }).click();
    await expect(page.getByRole("link", { name: new RegExp(title) })).toBeVisible();
    await signOut(page);

    await signIn(page, "alice@lmsfree.local", "Student@1234");
    await page.getByRole("link", { name: /Introduction to Programming/ }).click();
    await page.getByRole("link", { name: new RegExp(title) }).click();
    await page.getByLabel("Answer").fill("for i in range(1, 11): print(i)");
    await page.getByLabel(/Attachment/).setInputFiles({ name: "loops.py", mimeType: "text/x-python", buffer: Buffer.from("for i in range(1, 11):\n    print(i)\n") });
    await page.getByRole("button", { name: "Submit" }).click();
    await expect(page.getByRole("button", { name: "loops.py" })).toBeVisible();
    await signOut(page);

    await signIn(page, "teacher@lmsfree.local", "Teacher@1234");
    await page.getByRole("link", { name: /Introduction to Programming/ }).click();
    await page.getByRole("link", { name: new RegExp(title) }).click();
    const card = page.locator(".card", { hasText: "Alice Martin" });
    const download = page.waitForEvent("download");
    await card.getByRole("button", { name: "Download loops.py" }).click();
    expect((await download).suggestedFilename()).toBe("loops.py");
    await card.getByLabel(/Score/).fill("9");
    await card.getByLabel("Feedback").fill("Clean loop.");
    await card.getByRole("button", { name: "Save grade" }).click();
    await expect(card.getByText("Graded")).toBeVisible();
    await signOut(page);

    await signIn(page, "alice@lmsfree.local", "Student@1234");
    await page.getByRole("link", { name: "My grades" }).click();
    const row = page.getByRole("row", { name: new RegExp(title) });
    await expect(row).toContainText("9 / 10");
    await expect(row).toContainText("Clean loop.");
  });

  test("an admin adds a teacher and the teacher enrolls a new student", async ({ page }) => {
    const stamp = Date.now();
    await page.goto("/register");
    await page.getByLabel("Full name").fill("Nora New");
    await page.getByLabel("Email").fill(`nora${stamp}@example.com`);
    await page.getByLabel("Password").fill("Student123");
    await page.getByRole("button", { name: "Create account" }).click();
    await expect(page.getByText(/You're not enrolled in any course yet/)).toBeVisible();
    await signOut(page);

    await signIn(page, "admin@lmsfree.local", "Admin@1234");
    await page.getByRole("link", { name: "Users" }).click();
    await page.getByText("Add a user").click();
    await page.getByLabel("Full name").fill("Tom Teacher");
    await page.getByLabel("Email").fill(`tom${stamp}@example.com`);
    await page.getByLabel("Temporary password").fill("Teacher123");
    await page.getByRole("button", { name: "Add user" }).click();
    await expect(page.getByRole("cell", { name: `tom${stamp}@example.com` })).toBeVisible();
    await signOut(page);

    await signIn(page, `tom${stamp}@example.com`, "Teacher123");
    await page.getByRole("link", { name: "New course" }).click();
    await page.getByLabel("Code").fill(`BIO${stamp % 100000}`);
    await page.getByLabel("Title").fill("Cell Biology");
    await page.getByRole("button", { name: "Create course" }).click();
    await page.getByRole("button", { name: "Students" }).click();
    await page.getByLabel("Student email").fill(`nora${stamp}@example.com`);
    await page.getByRole("button", { name: "Enroll student" }).click();
    await expect(page.getByRole("cell", { name: "Nora New" })).toBeVisible();
  });

  test("password reset goes through email", async ({ page, request }) => {
    await page.goto("/forgot-password");
    await page.getByLabel("Email").fill("bob@lmsfree.local");
    await page.getByRole("button", { name: "Send reset link" }).click();
    await expect(page.getByText(/a reset link is on its way/)).toBeVisible();

    let link = "";
    await expect(async () => {
      const list = await (await request.get("http://localhost:8025/api/v1/search?query=to:bob@lmsfree.local")).json();
      const message = await (await request.get(`http://localhost:8025/api/v1/message/${list.messages[0].ID}`)).json();
      link = message.Text.match(/http\S+reset-password\?token=\w+/)[0];
    }).toPass({ timeout: 15_000 });

    await page.goto(new URL(link).pathname + new URL(link).search);
    await page.getByLabel("New password").fill("BobNew1234");
    await page.getByRole("button", { name: "Save password" }).click();
    await expect(page.getByText("Password updated.")).toBeVisible();
    await page.getByLabel("Email").fill("bob@lmsfree.local");
    await page.getByLabel("Password").fill("BobNew1234");
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page.getByRole("heading", { name: "My courses" })).toBeVisible();
  });
});
