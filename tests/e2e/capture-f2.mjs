import { chromium } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
const browser=await chromium.launch();const page=await browser.newPage({viewport:{width:1366,height:1000}});const base=process.env.WEB_URL??'http://localhost:5173';
await page.goto(base+'/login');await page.getByLabel('Correo electrónico').fill('admin.a@demo.local');await page.getByLabel('Contraseña',{exact:true}).fill(process.env.Seed__Passwords__AdminA);await page.getByRole('button',{name:'Entrar',exact:true}).click();await page.getByRole('button',{name:'Cerrar sesión'}).waitFor();
const me=await (await page.request.get(base+'/api/v1/auth/me')).json();let target;
for(const venue of me.venues.filter(x=>x.name.startsWith('E2E'))){const layouts=await(await page.request.get(`${base}/api/v1/tenants/${venue.tenantId}/venues/${venue.id}/layouts`)).json();if(layouts.length){const detail=await(await page.request.get(`${base}/api/v1/tenants/${venue.tenantId}/venues/${venue.id}/layouts/${layouts[0].id}`)).json();if(detail.versions.length){target={venue,id:layouts[0].id};break;}}}
if(!target)throw Error('Run planning E2E first');
await page.goto(`${base}/workspace/${target.venue.tenantId}/${target.venue.id}/layouts/${target.id}`);await page.getByRole('button',{name:/Box modificado, Exclusivo/}).click();await mkdir('../../docs/screenshots',{recursive:true});
await page.screenshot({path:'../../docs/screenshots/f2-editor-desktop.png',fullPage:true});await page.setViewportSize({width:390,height:844});await page.screenshot({path:'../../docs/screenshots/f2-editor-mobile.png',fullPage:true});
if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))throw Error('Mobile editor overflows');await browser.close();
