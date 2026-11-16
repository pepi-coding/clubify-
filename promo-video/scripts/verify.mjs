import {createRequire} from 'node:module';
import fs from 'node:fs';
const require=createRequire(import.meta.url);const {chromium}=require('../../tests/e2e/node_modules/@playwright/test');
const browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:540,height:960}});
const src='data:video/mp4;base64,'+fs.readFileSync('out/boboteca-flow.mp4').toString('base64');
await page.setContent(`<body style="margin:0;background:#0c0e17"><video muted style="width:540px;height:960px" src="${src}"></video></body>`);
await page.waitForFunction(()=>document.querySelector('video').readyState>=2);
const metadata=await page.evaluate(async()=>{const v=document.querySelector('video');const a=new OfflineAudioContext(2,1,48000);const data=await a.decodeAudioData(await(await fetch(v.src)).arrayBuffer());let peak=0,sum=0;const samples=data.getChannelData(0);for(let i=0;i<samples.length;i++){peak=Math.max(peak,Math.abs(samples[i]));sum+=samples[i]*samples[i];}return {duration:v.duration,width:v.videoWidth,height:v.videoHeight,audioDuration:data.duration,audioPeak:peak,audioRms:Math.sqrt(sum/samples.length)};});
for(const second of [3,17,31,38,48,52,56]){await page.evaluate(t=>new Promise(resolve=>{const v=document.querySelector('video');v.onseeked=()=>resolve(true);v.currentTime=t;}),second);await page.locator('video').screenshot({path:`out/decoded-${second}.png`});}
if(Math.abs(metadata.duration-58)>.1||metadata.width!==1080||metadata.height!==1920||metadata.audioPeak<.01||metadata.audioPeak>=1)throw Error(JSON.stringify(metadata));
fs.writeFileSync('out/verification.json',JSON.stringify({...metadata,decodedAtSeconds:[3,17,31,38,48,52,56]},null,2));console.log(JSON.stringify(metadata));await browser.close();
