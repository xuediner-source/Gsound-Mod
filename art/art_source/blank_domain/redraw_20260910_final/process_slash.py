from pathlib import Path
import hashlib,json,struct
import numpy as np
from PIL import Image

ROOT=Path(__file__).parent
OUT=ROOT/'processed'; OUT.mkdir(exist_ok=True)
SOURCE=ROOT/'slash-accepted-green.png'
HEAD_TOP,CHIN,ANCHOR=193.0,413.0,(1085.0,868.0)
HEAD=CHIN-HEAD_TOP
rgb=np.asarray(Image.open(SOURCE).convert('RGB')).astype(np.float32)
maximum=np.maximum(rgb[:,:,0],rgb[:,:,2]); dominance=rgb[:,:,1]-maximum
matte=np.clip(dominance/np.maximum(255-maximum,1),0,1); matte[dominance<12]=0
alpha=1-matte; alpha[alpha<.035]=0; alpha[(rgb[:,:,1]>180)&(maximum<35)]=0; alpha[alpha>.97]=1
fg=rgb.copy(); fg[:,:,1]-=(1-alpha)*255; fg/=np.maximum(alpha[:,:,None],.001)
rgba=np.dstack((np.clip(fg,0,255),alpha*255)).round().astype(np.uint8); rgba[alpha==0]=0
im=Image.fromarray(rgba,'RGBA'); bounds=im.getbbox(); im=im.crop(bounds)
scale=112/HEAD; size=tuple(round(v*scale) for v in im.size)
im=im.convert('RGBa').resize(size,Image.Resampling.LANCZOS).convert('RGBA')
pad=10; out=Image.new('RGBA',(size[0]+2*pad,size[1]+2*pad)); out.paste(im,(pad,pad))
x=(ANCHOR[0]-bounds[0])*scale+pad; y=(ANCHOR[1]-bounds[1])*scale+pad
pivot=[x/out.width,1-y/out.height]
out.save(OUT/'slash.png')
raw=struct.pack('<ii',*out.size)+out.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes();(OUT/'blank_domain_slash_runtime.rgba').write_bytes(raw)
a=np.asarray(out).astype(np.int16); green=int(np.count_nonzero((a[:,:,1]>a[:,:,0]+40)&(a[:,:,1]>a[:,:,2]+40)&(a[:,:,1]>130)&(a[:,:,3]>100))); assert green==0
meta={'file':'slash.png','source':str(SOURCE),'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'size':list(out.size),'pivot':pivot,'head_pixels':112,'ppu':100,'scale':scale,'source_crop':list(bounds),'source_ground_anchor':list(ANCHOR),'source_head_top_y':HEAD_TOP,'source_chin_y':CHIN,'source_head_height':HEAD,'green_pixel_residue':green}
frames=json.loads((OUT/'frames.json').read_text(encoding='utf-8-sig'));frames['slash']=meta;(OUT/'frames.json').write_text(json.dumps(frames,indent=2),encoding='utf-8')
print(json.dumps(meta,indent=2))
