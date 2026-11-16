import fs from 'node:fs';
const sr=44100,seconds=58,n=sr*seconds;const data=Buffer.alloc(n*2);const beat=60/112;
const hz=m=>440*2**((m-69)/12);const notes=[45,48,52,55,43,47,50,55];
for(let i=0;i<n;i++){const t=i/sr;const b=t/beat,phase=b%1;const bar=Math.floor(b/8)%2;const bass=hz(bar?43:45);let v=0;
 const kick=Math.sin(2*Math.PI*(48*(phase*beat)+3*(1-Math.exp(-phase*beat*35))))*Math.exp(-phase*beat*23)*.22;
 const p=(b*2)%1;const note=notes[Math.floor(b*2)%notes.length]+12;const synth=(Math.sin(2*Math.PI*hz(note)*t)+.18*Math.sin(2*Math.PI*hz(note)*2*t))*Math.exp(-p*5)*.055;
 const low=Math.sin(2*Math.PI*bass*t)*Math.exp(-phase*3)*.07;
 const noise=(Math.sin(i*12.9898)*43758.5453)%1;const hat=noise*Math.exp(-((b*4)%1)*30)*.022;
 v=(kick+synth+low+hat)*3;v*=Math.min(1,t/2,(seconds-t)/2.5);data.writeInt16LE(Math.max(-32767,Math.min(32767,Math.round(v*32767))),i*2);
}
const h=Buffer.alloc(44);h.write('RIFF');h.writeUInt32LE(36+data.length,4);h.write('WAVE',8);h.write('fmt ',12);h.writeUInt32LE(16,16);h.writeUInt16LE(1,20);h.writeUInt16LE(1,22);h.writeUInt32LE(sr,24);h.writeUInt32LE(sr*2,28);h.writeUInt16LE(2,32);h.writeUInt16LE(16,34);h.write('data',36);h.writeUInt32LE(data.length,40);fs.writeFileSync(new URL('../public/soundtrack.wav',import.meta.url),Buffer.concat([h,data]));console.log('Original 58-second synthesized soundtrack created.');

