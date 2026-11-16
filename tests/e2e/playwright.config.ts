import { defineConfig } from '@playwright/test';
export default defineConfig({testDir:'.',testMatch:'*.spec.ts',use:{baseURL:process.env.WEB_URL??'http://localhost:5173',trace:'retain-on-failure'},projects:[{name:'chromium',use:{browserName:'chromium'}},{name:'mobile',use:{browserName:'chromium',viewport:{width:390,height:844},isMobile:true,hasTouch:true}}],workers:1});
