import { login } from './auth';
import {test,expect} from '@playwright/test';
const venue='/api/v1/tenants/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/venues/aaaaaaaa-0000-0000-0000-000000000001';
const workspace='/workspace/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/aaaaaaaa-0000-0000-0000-000000000001';

async function mutate(page,url,data,method='post'){const csrf=await(await page.request.get('/api/v1/auth/csrf')).json();const r=await page.request.fetch(url,{method,data,headers:{'X-CSRF-TOKEN':csrf.token,'Idempotency-Key':crypto.randomUUID()}});expect(r.ok(),`Setup failed: ${r.status()}`).toBeTruthy();return r.json();}
async function setup(browser,baseURL,info){
 const contexts=[];const options={baseURL,viewport:info.project.name==='mobile'?{width:390,height:844}:{width:1280,height:900},isMobile:info.project.name==='mobile',hasTouch:info.project.name==='mobile'};
 async function account(email,key){const context=await browser.newContext(options);contexts.push(context);const page=await context.newPage();await login(page,email,key);return page;}
 const admin=await account('admin.a@demo.local','AdminA');const original=await(await admin.request.get(venue+'/payment-settings')).json();
 await mutate(admin,venue+'/payment-settings',{whatsAppNumber:'51900000000',instructions:'Configuración temporal E2E, sin envío de mensajes.'},'put');
 try{
 const seed=await(await admin.request.get('/api/v1/public/events/aaaaaaaa-3333-0000-0000-000000000001')).json();const now=Date.now();const iso=(minutes)=>new Date(now+minutes*60000).toISOString();
 const draft=await mutate(admin,venue+'/events',{name:`E2E F5 ${info.project.name} ${now}`,capacity:100,startsAt:iso(-30),endsAt:iso(180),entryOpensAt:iso(-60),entryClosesAt:iso(120),redemptionOpensAt:iso(-30),redemptionClosesAt:iso(150),layoutVersionId:seed.layoutVersionId});
 const event=await mutate(admin,venue+`/events/${draft.id}/publish`,{expectedRevision:draft.revision});const products=await(await admin.request.get(venue+'/products')).json();
 const offer=await mutate(admin,venue+`/events/${event.id}/offers`,{name:'Paquete F5 B3',priceCents:100000,resourceId:event.resources.find(x=>x.name==='Box B3').id,resourceUnits:1,admissions:10,drinks:[{productId:products.find(x=>x.name==='Whisky').id,quantity:1},{productId:products.find(x=>x.name==='Gaseosa').id,quantity:2}]});
 const customer=await account('customer@demo.local','Customer');const reservation=await mutate(customer,'/api/v1/reservations',{eventId:event.id,lines:[{offerVersionId:offer.id,quantity:1}]});
 await mutate(admin,venue+`/events/${event.id}/manual-payments/${reservation.orderId}/verify`,{approved:true,method:'Plin',receiptReference:'E2E-F5-'+crypto.randomUUID(),amountCents:100000,paidAt:new Date().toISOString(),note:'Prueba F5: comprobante ficticio, sin dinero real.'});
 const order=await(await customer.request.get('/api/v1/me/orders/'+reservation.orderId)).json();
 const security=await account('security@demo.local','Security');const bar=await account('bar@demo.local','Bar');
 async function open(page){await page.goto(workspace);await page.getByRole('combobox',{name:'Evento de operación',exact:true}).selectOption(event.id);}
 async function cleanup(){await mutate(admin,venue+'/payment-settings',{whatsAppNumber:original.whatsAppNumber,instructions:original.instructions},'put');for(const c of contexts)await c.close();}
 return{admin,customer,security,bar,order,event,open,cleanup};
 }catch(e){await mutate(admin,venue+'/payment-settings',{whatsAppNumber:original.whatsAppNumber,instructions:original.instructions},'put');for(const c of contexts)await c.close();throw e;}
}
test('F5 real QR decoder, separate confirmation, lost responses and partial bar delivery',async({browser,baseURL},info)=>{
 test.setTimeout(90000);const f=await setup(browser,baseURL,info);
 try{
 await f.open(f.bar);await f.bar.getByLabel('Código del vale de consumo',{exact:true}).fill(f.order.voucher.token);await f.bar.getByRole('button',{name:'Consultar código',exact:true}).click();await expect(f.bar.getByText('Debe registrarse al menos un ingreso de este pedido antes del canje.',{exact:true})).toBeVisible();await expect(f.bar.getByRole('button',{name:'Confirmar entrega seleccionada'})).toHaveCount(0);
 await f.customer.goto('/orders/'+f.order.id);const png=await f.customer.locator('.qr-card svg').first().screenshot();
 await f.open(f.security);
 await f.security.evaluate((encoded)=>{Object.defineProperty(navigator.mediaDevices,'getUserMedia',{configurable:true,value:async()=>{const image=new Image();image.src='data:image/png;base64,'+encoded;await image.decode();const canvas=document.createElement('canvas');canvas.width=640;canvas.height=480;const ctx=canvas.getContext('2d');const draw=()=>{ctx.fillStyle='white';ctx.fillRect(0,0,640,480);ctx.drawImage(image,150,70,340,340);};draw();const stream=canvas.captureStream(15);const timer=setInterval(draw,65);for(const track of stream.getVideoTracks()){const nativeStop=track.stop.bind(track);track.stop=()=>{clearInterval(timer);nativeStop();window.__cameraStopped=true;};}return stream;}});},png.toString('base64'));
 await f.security.getByRole('button',{name:'Usar cámara',exact:true}).click();await expect(f.security.getByText('Pase válido. Falta confirmar el ingreso.',{exact:true})).toBeVisible();await expect.poll(()=>f.security.evaluate(()=>window.__cameraStopped)).toBeTruthy();await expect(f.security.locator('.operation-result.approved')).toHaveCount(0);
 let detail=await(await f.customer.request.get('/api/v1/me/orders/'+f.order.id)).json();expect(detail.tickets[0].status).toBe('Issued');
 const entryRoute='**/admissions/check-in';await f.security.route(entryRoute,async route=>{const r=await route.fetch();expect(r.status()).toBe(200);await route.abort('failed');});
 await f.security.getByRole('button',{name:'Confirmar ingreso',exact:true}).click();await expect(f.security.getByText('Confirmación pendiente de recuperar',{exact:true})).toBeVisible();await expect(f.security.locator('.operation-result.approved')).toHaveCount(0);await f.security.unroute(entryRoute);await f.security.getByRole('button',{name:'Reintentar misma confirmación',exact:true}).click();await expect(f.security.getByText('Ingreso confirmado.',{exact:true})).toBeVisible();await f.security.screenshot({path:`../../docs/screenshots/f5-security-${info.project.name}.png`,fullPage:true});
 await f.security.getByRole('button',{name:'Leer siguiente código'}).click();await f.security.getByLabel('Código del pase individual',{exact:true}).fill(f.order.tickets[0].token);await f.security.getByRole('button',{name:'Consultar código',exact:true}).click();await expect(f.security.getByText('Este pase ya registró un ingreso. No permite reingreso.',{exact:true})).toBeVisible();await expect(f.security.getByRole('button',{name:'Confirmar ingreso',exact:true})).toHaveCount(0);
 await f.bar.getByRole('button',{name:'Consultar código',exact:true}).click();await expect(f.bar.getByText('Vale válido. Selecciona cantidades y confirma la entrega.',{exact:true})).toBeVisible();await f.bar.getByLabel('Entregar Gaseosa',{exact:true}).fill('1');
 const redeemRoute='**/redemptions';await f.bar.route(redeemRoute,async route=>{const r=await route.fetch();expect(r.status()).toBe(200);await route.abort('failed');});await f.bar.getByRole('button',{name:'Confirmar entrega seleccionada',exact:true}).click();await expect(f.bar.getByText('Confirmación pendiente de recuperar',{exact:true})).toBeVisible();await expect(f.bar.locator('.operation-result.approved')).toHaveCount(0);await f.bar.unroute(redeemRoute);
 await f.bar.reload();await f.bar.getByRole('combobox',{name:'Evento de operación',exact:true}).selectOption(f.event.id);await expect(f.bar.getByText('Entrega confirmada. Revisa el saldo actualizado.',{exact:true})).toBeVisible();await expect(f.bar.getByText('1 pendientes de 2 · 1 entregados',{exact:true})).toBeVisible();await expect(f.bar.getByText('1 pendientes de 1 · 0 entregados',{exact:true})).toBeVisible();await f.bar.screenshot({path:`../../docs/screenshots/f5-bar-${info.project.name}.png`,fullPage:true});
 detail=await(await f.customer.request.get('/api/v1/me/orders/'+f.order.id)).json();expect(detail.tickets.filter(t=>t.status==='Used')).toHaveLength(1);expect(detail.voucher.lines.find(x=>x.name==='Gaseosa').delivered).toBe(1);expect(detail.voucher.lines.find(x=>x.name==='Whisky').delivered).toBe(0);
 expect(await f.bar.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBeTruthy();expect(await f.security.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBeTruthy();
 const stored=await f.bar.evaluate(()=>JSON.stringify(sessionStorage));expect(stored).not.toContain(f.order.voucher.token);
 }finally{await f.cleanup();}
});
test('F5 denied camera and offline inspection keep manual fallback without showing approval',async({browser,baseURL},info)=>{
 test.setTimeout(90000);const f=await setup(browser,baseURL,info);
 try{
 await f.open(f.security);await f.security.evaluate(()=>{Object.defineProperty(navigator.mediaDevices,'getUserMedia',{configurable:true,value:async()=>{throw new DOMException('Permission denied','NotAllowedError');}});});await f.security.getByRole('button',{name:'Usar cámara',exact:true}).click();await expect(f.security.getByRole('alert')).toContainText('Permiso de cámara denegado');
 await f.security.getByLabel('Código del pase individual',{exact:true}).fill(f.order.tickets[0].token);await f.security.getByRole('button',{name:'Consultar código',exact:true}).click();await expect(f.security.getByText('Pase válido. Falta confirmar el ingreso.',{exact:true})).toBeVisible();await expect(f.security.locator('.operation-result.approved')).toHaveCount(0);
 await f.security.context().setOffline(true);await f.security.getByRole('button',{name:'Consultar código',exact:true}).click();await expect(f.security.getByRole('alert').last()).toContainText('No se pudo confirmar con el servidor');await expect(f.security.locator('.operation-result.approved')).toHaveCount(0);await expect(f.security.getByRole('button',{name:'Confirmar ingreso',exact:true})).toHaveCount(0);await f.security.context().setOffline(false);await expect(f.security.getByLabel('Código del pase individual',{exact:true})).toBeEnabled();
await f.security.getByLabel('Código del pase individual',{exact:true}).fill(f.order.voucher.token);await f.security.getByRole('button',{name:'Consultar código',exact:true}).click();await expect(f.security.getByText('Un vale de consumo no permite ingresar. Usa un pase individual.',{exact:true})).toBeVisible();await expect(f.security.locator('.operation-result.approved')).toHaveCount(0);
 }finally{await f.security.context().setOffline(false);await f.cleanup();}
});
