import { test, expect } from "@playwright/test";
const eventId = "aaaaaaaa-3333-0000-0000-000000000001";

test("public themes persist and venue/date filters use published data", async ({
  page,
}) => {
  const events = await (await page.request.get("/api/v1/public/events")).json();
  await page.goto("/");
  await expect(page.locator(".n-card")).toHaveCount(events.length);
  await page
    .getByRole("combobox", { name: "Estilo visual" })
    .selectOption("midnight-lounge");
  await expect(page.locator(".n-app")).toHaveAttribute(
    "data-theme",
    "midnight-lounge",
  );
  await expect(page.locator(".n-hero img")).toHaveAttribute(
    "src",
    /midnight-lounge/,
  );
  const first = events[0];
  await page.getByLabel("Sede", { exact: true }).selectOption(first.venueId);
  await page.getByLabel("Fecha", { exact: true }).fill("2099-01-01");
  await page
    .getByRole("button", { name: "Buscar eventos", exact: true })
    .click();
  await expect(
    page.getByText("No hay eventos con estos filtros.", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Limpiar", exact: true }).click();
  await expect(page.locator(".n-card")).toHaveCount(events.length);
  await page.goto("/events/" + eventId);
  await expect(page.locator(".n-app")).toHaveAttribute(
    "data-theme",
    "midnight-lounge",
  );
  await expect(
    page.getByRole("heading", { name: "Mapa del local", exact: true }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBeTruthy();
  await page.screenshot({
    path:
      "../../docs/screenshots/noche-lounge-" +
      test.info().project.name +
      ".png",
  });
});

test("published map preserves geometry and selects actual resource offers", async ({
  page,
}) => {
  const event = await (
    await page.request.get("/api/v1/public/events/" + eventId)
  ).json();
  const catalog = await (
    await page.request.get("/api/v1/public/events/" + eventId + "/catalog")
  ).json();
  await page.goto("/events/" + eventId);
  await expect(page.locator(".n-offer")).toHaveCount(
    catalog.offers.length ? 1 : 0,
  );
  for (const item of event.elements) {
    const rect = page.locator(`[data-element-id="${item.id}"] > rect`).first();
    await expect(rect).toHaveAttribute("x", String(item.geometry.x * 1000));
    await expect(rect).toHaveAttribute(
      "width",
      String(item.geometry.width * 1000),
    );
  }
  const resource = event.resources.find(
    (r) =>
      catalog.offers.some((o) => o.resourceId === r.id) &&
      catalog.inventory.some(
        (i) => i.resourceId === r.id && i.state === "Available",
      ),
  );
  expect(resource).toBeTruthy();
  await page.getByRole('button', {name:'Ampliar mapa', exact:true}).click();
  await expect(page.locator('.n-map-zoom')).toHaveCount(1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
  await page.getByRole('button', {name:'Ajustar mapa', exact:true}).click();
  const zone = page.locator(`[data-element-id="${resource.sourceElementId}"]`);
  await zone.focus();
  await page.keyboard.press("Enter");
  await expect(zone).toHaveAttribute("aria-pressed", "true");
  const names = catalog.offers
    .filter((o) => o.resourceId === resource.id)
    .map((o) => o.name);
  await expect(page.locator(".n-offer h3")).toHaveText(names[0]);
  await page.getByRole("button", { name: "Ver lista", exact: true }).click();
  await expect(
    page.locator(".n-resource-list button[aria-pressed=true]"),
  ).toContainText(resource.name);
  await page
    .getByRole("button", { name: "Ver todas las ofertas", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Ver todas las ofertas", exact: true }),
  ).toHaveCount(0);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBeTruthy();
  await page.getByRole("button", { name: "Ver mapa", exact: true }).click();
  await page.screenshot({
    path:
      "../../docs/screenshots/noche-electric-" +
      test.info().project.name +
      ".png",
  });
});

test("public presentation handles sold resources, no offers and network errors", async ({
  page,
}) => {
  const url = "/api/v1/public/events/" + eventId + "/catalog";
  const catalog = await (await page.request.get(url)).json();
  await page.route("**" + url, (route) =>
    route.fulfill({
      json: {
        ...catalog,
        offers: [],
        inventory: catalog.inventory.map((i) => ({
          ...i,
          state: "Sold",
          available: 0,
        })),
      },
    }),
  );
  await page.goto("/events/" + eventId);
  await expect(
    page.getByText("Este evento todavía no tiene ofertas.", { exact: true }),
  ).toBeVisible();
  await expect(page.locator(".n-published-map g[tabindex]")).toHaveCount(0);
  await page.getByRole("button", { name: "Ver lista", exact: true }).click();
  for (const button of await page.locator(".n-resource-list button").all())
    await expect(button).toBeDisabled();
  await page.unroute("**" + url);
  await page.route("**" + url, (route) => route.abort());
  await page.reload();
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page.locator(".n-offer")).toHaveCount(0);
});
