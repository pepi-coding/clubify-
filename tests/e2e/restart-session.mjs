import { request, expect } from '@playwright/test';
import { unlink } from 'node:fs/promises';
const baseURL=process.env.WEB_URL??'http://localhost:5081';
const path='../../.local/restart-cookie.json';
if(process.argv[2]==='issue') {
 const ctx=await request.newContext({baseURL});
 const csrf=await (await ctx.get('/api/v1/auth/csrf')).json();
 const login=await ctx.post('/api/v1/auth/login',{headers:{'X-CSRF-TOKEN':csrf.token},data:{email:'admin.a@demo.local',password:process.env.Seed__Passwords__AdminA}});
 expect(login.status()).toBe(200);await ctx.storageState({path});await ctx.dispose();
 console.log('Created session for restart verification.');
} else {
 const ctx=await request.newContext({baseURL,storageState:path});
 expect((await ctx.get('/api/v1/auth/me')).status()).toBe(200);
 await ctx.dispose();await unlink(path);console.log('Session survived API restart using persistent keys and database.');
}
