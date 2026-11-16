import {expect,type Page} from '@playwright/test';
export async function login(page:Page,email:string,key:string){
 const password=process.env['Seed__Passwords__'+key];if(!password)throw Error('Missing password for '+key);
 await page.goto('/login');await page.getByLabel('Correo electrónico').fill(email);await page.getByLabel('Contraseña',{exact:true}).fill(password);
 const attempt=async()=>{const response=page.waitForResponse(r=>r.url().endsWith('/api/v1/auth/login')&&r.request().method()==='POST');await page.getByRole('button',{name:'Entrar',exact:true}).click();return response;};
 let response=await attempt();
 // The expanded suite deliberately honors the real server's 20/minute limit.
 // Retry only 429; invalid credentials must fail without repeated login attempts.
 if(response.status()===429)await expect(async()=>{response=await attempt();expect(response.status()).not.toBe(429);}).toPass({intervals:[15000],timeout:75000});
 expect(response.status(),'Configured demo account should authenticate').toBe(200);await expect(page.getByRole('button',{name:'Cerrar sesión'})).toBeVisible();
}
