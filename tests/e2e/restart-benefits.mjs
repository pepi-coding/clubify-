import { request } from '@playwright/test';
import { readFile } from 'node:fs/promises';
const baseURL=process.env.WEB_URL??'http://localhost:5081';
if(process.argv[2]==='capture'){
 const c=await request.newContext({baseURL});const csrf=await (await c.get('/api/v1/auth/csrf')).json();const login=await c.post('/api/v1/auth/login',{headers:{'X-CSRF-TOKEN':csrf.token},data:{email:'customer@demo.local',password:process.env.Seed__Passwords__Customer}});if(!login.ok())throw Error('Login failed');await c.storageState({path:'../../.local/f4-session.json'});await c.dispose();console.log('Captured private demo session for restart verification.');
}else{
 const c=await request.newContext({baseURL,storageState:'../../.local/f4-session.json'});
 for(const project of ['chromium','mobile']){const saved=JSON.parse(await readFile(`../../.local/f4-restart-${project}.json`,'utf8'));const r=await c.get(`/api/v1/me/orders/${saved.id}`);if(!r.ok())throw Error(`Order recovery failed: ${r.status()}`);const d=await r.json();if(d.status!=='Confirmed'||JSON.stringify(d.tickets.map(t=>t.token))!==JSON.stringify(saved.tokens)||d.voucher.token!==saved.voucher)throw Error('Issued credentials changed after restart');console.log(`${project}: persisted session, ten tickets and voucher recovered unchanged after container restart.`);}
 await c.dispose();
}
