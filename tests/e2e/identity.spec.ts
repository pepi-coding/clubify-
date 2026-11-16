import { login } from './auth';
import { test, expect } from '@playwright/test';

test('admin selects a persisted venue and logout removes access',async({page})=>{
 await login(page,'admin.a@demo.local','AdminA');await page.locator('a[href="/workspace/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/aaaaaaaa-0000-0000-0000-000000000001"]').click();await expect(page.getByRole('heading',{name:'Administración de sede'})).toBeVisible();await page.getByRole('button',{name:'Usar esta sede'}).click();await expect(page.getByRole('status')).toHaveText('Contexto guardado en tu sesión.');await page.reload();await expect(page.getByRole('heading',{name:'Administración de sede'})).toBeVisible();await page.getByRole('button',{name:'Cerrar sesión'}).click();await expect(page.getByRole('link',{name:'Iniciar sesión',exact:true})).toBeVisible();
 await page.goto('/workspace/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/aaaaaaaa-0000-0000-0000-000000000001');await expect(page.getByRole('alert')).toContainText('Inicia sesión');
});
test('customer has no administrative navigation or API access',async({page})=>{
 await login(page,'customer@demo.local','Customer');await expect(page.getByRole('heading',{name:'Mi cuenta'})).toBeVisible();await expect(page.locator('a[href="/workspace/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/aaaaaaaa-0000-0000-0000-000000000001"]')).toHaveCount(0);await page.goto('/workspace/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/aaaaaaaa-0000-0000-0000-000000000001');await expect(page.getByRole('alert')).toContainText('No se encontró');
});
test('security can open only assigned venue and cannot edit',async({page})=>{
 await login(page,'security@demo.local','Security');await expect(page.locator('a[href="/workspace/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/aaaaaaaa-0000-0000-0000-000000000002"]')).toHaveCount(0);await page.locator('a[href="/workspace/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/aaaaaaaa-0000-0000-0000-000000000001"]').click();await expect(page.getByRole('heading',{name:'Seguridad',exact:true})).toBeVisible();await expect(page.getByRole('button',{name:'Guardar nombre'})).toHaveCount(0);await page.goto('/workspace/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/aaaaaaaa-0000-0000-0000-000000000002');await expect(page.getByRole('alert')).toContainText('No tienes permiso');
});
