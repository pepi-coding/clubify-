import {createRequire} from 'node:module';
import fs from 'node:fs';
const require=createRequire(import.meta.url);const {chromium}=require('../../tests/e2e/node_modules/@playwright/test');
const browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:1200,height:940},deviceScaleFactor:1});
const frames=[90,300,510,750,960,1120,1310,1440,1550,1680];await page.setContent(`<body style="margin:0;background:#242838;display:grid;grid-template-columns:repeat(5,240px);font:18px Arial;color:white">${frames.map(f=>`<div><div style="height:24px;text-align:center">${(f/30).toFixed(1)} s</div><img style="width:240px;height:426.67px" src="data:image/png;base64,${fs.readFileSync('out/frame-'+f+'.png').toString('base64')}"/></div>`).join('')}</body>`);await page.screenshot({path:'out/contact-sheet.png',fullPage:true});await browser.close();
