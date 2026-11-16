import { login } from './auth';
import { test, expect } from '@playwright/test';

test('F2 saves a moved map, publishes an overnight event and preserves its snapshot',async({page})=>{
 const venueName='E2E '+crypto.randomUUID().slice(0,8);
 await login(page,'admin.a@demo.local','AdminA');
 await page.locator('summary').filter({hasText:'Crear sede'}).click();await page.getByLabel('Nombre de nueva sede').fill(venueName);await page.getByRole('button',{name:'Crear sede',exact:true}).click();await page.getByRole('link',{name:new RegExp(venueName)}).click();
 await page.getByLabel('Nombre del mapa',{exact:true}).fill('Plano de prueba');await page.getByRole('button',{name:'Crear mapa',exact:true}).click();
 await expect(page.getByRole('heading',{name:'Plano de prueba',exact:true})).toBeVisible();
 await page.getByRole('button',{name:'+ Box',exact:true}).click();await page.getByLabel('Nombre del elemento',{exact:true}).fill('Box B3');
 const box=page.getByRole('button',{name:'Box B3, Exclusivo, 10 personas',exact:true});const rect=await box.boundingBox();if(!rect)throw Error('Missing SVG element');
 await page.mouse.move(rect.x+rect.width/2,rect.y+rect.height/2);await page.mouse.down();await page.mouse.move(rect.x+rect.width/2+25,rect.y+rect.height/2+15,{steps:6});await page.mouse.up();
 const x=Number(await page.getByLabel('Posición X (%)',{exact:true}).inputValue());expect(x).toBeGreaterThan(8);
 await page.getByLabel('Ancho (%)',{exact:true}).fill('25');await page.getByLabel('Rotación (grados)',{exact:true}).fill('30');
 await page.getByRole('button',{name:'+ General',exact:true}).click();await page.getByLabel('Capacidad del elemento',{exact:true}).fill('90');await page.getByRole('button',{name:'+ Barra',exact:true}).click();await expect(page.getByRole('combobox',{name:'Modo de venta',exact:true})).toBeDisabled();
 await page.getByRole('button',{name:'Guardar mapa',exact:true}).click();await expect(page.getByRole('status')).toContainText('Mapa guardado');await page.reload();
 await page.getByRole('button',{name:'Box B3, Exclusivo, 10 personas',exact:true}).click();expect(Number(await page.getByLabel('Posición X (%)',{exact:true}).inputValue())).toBeCloseTo(x,1);await expect(page.getByLabel('Ancho (%)',{exact:true})).toHaveValue('25');await expect(page.getByLabel('Rotación (grados)',{exact:true})).toHaveValue('30');
 await page.getByRole('button',{name:'Publicar mapa',exact:true}).click();await expect(page.getByRole('status')).toContainText('Versión 1 publicada');
 const editorUrl=page.url();await page.getByRole('link',{name:'← Volver a la sede'}).click();await page.getByRole('button',{name:'Crear evento',exact:true}).click();await page.getByLabel('Nombre del evento',{exact:true}).fill('Noche E2E');await page.getByRole('combobox',{name:'Versión de mapa',exact:true}).selectOption({label:'Plano de prueba · v1'});await page.getByRole('button',{name:'Guardar borrador',exact:true}).click();await expect(page.getByRole('heading',{name:'Noche E2E',exact:true})).toBeVisible();await page.getByRole('button',{name:'Publicar evento',exact:true}).click();await page.getByRole('link',{name:'Ver mapa publicado',exact:true}).click();
 await expect(page.getByRole('heading',{name:'Noche E2E',exact:true})).toBeVisible();const eventUrl=page.url();await page.getByRole('button',{name:'Ver lista',exact:true}).click();await expect(page.getByLabel('Lista de elementos')).toContainText('Box B3');await expect(page.getByLabel('Lista de elementos')).toContainText('Sin venta');
 await page.goto(editorUrl);await page.getByRole('button',{name:'Box B3, Exclusivo, 10 personas',exact:true}).click();await page.getByLabel('Nombre del elemento',{exact:true}).fill('Box modificado');await page.getByRole('button',{name:'Guardar mapa',exact:true}).click();await expect(page.getByRole('status')).toContainText('Mapa guardado');await page.getByRole('button',{name:'Publicar mapa',exact:true}).click();await expect(page.getByRole('status')).toContainText('Versión 2 publicada');
 await page.goto(eventUrl);await page.getByRole('button',{name:'Ver lista',exact:true}).click();await expect(page.getByLabel('Lista de elementos')).toContainText('Box B3');await expect(page.getByLabel('Lista de elementos')).not.toContainText('Box modificado');
 await page.screenshot({path:`../../docs/screenshots/f2-event-${test.info().project.name}.png`,fullPage:true});
});
