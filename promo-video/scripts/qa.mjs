import {bundle} from '@remotion/bundler';
import {selectComposition,renderStill} from '@remotion/renderer';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
const serveUrl=await bundle({entryPoint:fileURLToPath(new URL('../src/index.tsx',import.meta.url)),outDir:path.resolve('out/qa-bundle')});
const composition=await selectComposition({serveUrl,id:'BobotecaFlow'});
for(const frame of [90,300,510,750,960,1120,1310,1440,1550,1680]){await renderStill({serveUrl,composition,frame,output:`out/frame-${frame}.png`,imageFormat:'png'});console.log(`Checked frame ${frame}`);}
