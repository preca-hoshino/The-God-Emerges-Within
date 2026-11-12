"""Prepare stepped mouse/neuron contraction frames and a rotating dashed ring from authored PNGs."""
from pathlib import Path
import math, uuid
import numpy as np
from PIL import Image, ImageFilter
ROOT=Path(__file__).resolve().parents[1]
UI=ROOT/'New Unity Project/Assets/Via/UI'
OUT=UI/'Art/MainMenu/Flipbook'; OUT.mkdir(exist_ok=True)
META=(UI/'Art/MainMenu/IntroMouse01.png.meta').read_text()
CONTRACTION=[1.0,.94,.86,.80,.86,.94]
def save(im,name):
 p=OUT/(name+'.png'); im.save(p)
 meta=Path(str(p)+'.meta')
 if not meta.exists(): meta.write_text(META.replace('c397223b49d05114aeb6315c436ff996',uuid.uuid4().hex))
 return p
# Each mouse and its neuron use the same frame phase and contraction factor.
headings=[-90,0,45,90,125,180]
for i,heading in enumerate(headings):
 im=Image.open(UI/f'Art/MainMenu/IntroMouse{i+1:02}.png').convert('RGBA')
 im=im.rotate(heading,Image.Resampling.BICUBIC,expand=True)
 a=np.asarray(im); body=(a[:,:,0]>220)&(a[:,:,1]>220)&(a[:,:,2]>220)&(a[:,:,3]>160)
 yy,xx=np.where(body); cx,cy=xx.mean(),yy.mean()
 scale=min(math.sqrt(6000/len(xx)),360/im.width,235/im.height)*CONTRACTION[i]
 im=im.resize((round(im.width*scale),round(im.height*scale)),Image.Resampling.LANCZOS)
 frame=Image.new('RGBA',(384,288)); frame.alpha_composite(im,(round(192-cx*scale),round(144-cy*scale)))
 save(frame,f'MousePose{i:02}')
 nerve=Image.open(UI/f'开始页面/UI_Nerve_{i+1:02}.png').convert('RGBA')
 a=np.asarray(nerve); red=(a[:,:,0]>a[:,:,1]*1.15)&(a[:,:,0]>90)&(a[:,:,3]>160)
 yy,xx=np.where(red); cx,cy=xx.mean(),yy.mean()
 clean=np.array(nerve); outline=np.array(Image.fromarray(np.uint8(red)*255).filter(ImageFilter.MaxFilter(5)))
 clean[:,:,3]=np.minimum(clean[:,:,3],outline); nerve=Image.fromarray(clean)
 scale=min(math.sqrt(18000/len(xx)),460/nerve.width,460/nerve.height)*CONTRACTION[i]
 nerve=nerve.resize((round(nerve.width*scale),round(nerve.height*scale)),Image.Resampling.LANCZOS)
 frame=Image.new('RGBA',(512,512)); frame.alpha_composite(nerve,(round(256-cx*scale),round(256-cy*scale)))
 save(frame,f'NeuronPose{i:02}')
# A clean stationary dish is essential: no static neurons may remain below the shrinking frames.
source=Image.open(UI/'Art/MainMenu/CultureDishWithoutMice.png').convert('RGBA'); a=np.array(source)
fill=a[540,960].copy()
for row in a:
 interior=np.flatnonzero(np.all(row==fill,axis=1))
 if len(interior): row[interior[0]:interior[-1]+1]=fill
save(Image.fromarray(a),'CultureDishBase')
# Extract the original irregular dashed strokes, then build one complete, unclipped ring.
src=Image.open(UI/'开始页面/UI_Circle_01.png').convert('RGBA'); alpha=np.asarray(src)[:,:,3]>100
seen=np.zeros(alpha.shape,bool); patches=[]
for y,x in zip(*np.where(alpha)):
 if seen[y,x]: continue
 stack=[(int(y),int(x))]; seen[y,x]=True; points=[]
 while stack:
  sy,sx=stack.pop(); points.append((sx,sy))
  for ny,nx in ((sy-1,sx),(sy+1,sx),(sy,sx-1),(sy,sx+1)):
   if 0<=ny<alpha.shape[0] and 0<=nx<alpha.shape[1] and alpha[ny,nx] and not seen[ny,nx]: seen[ny,nx]=True; stack.append((ny,nx))
 if len(points)<80: continue
 pts=np.array(points); low=pts.min(axis=0); high=pts.max(axis=0)+1
 patch=src.crop((max(0,low[0]-2),max(0,low[1]-2),min(src.width,high[0]+2),min(src.height,high[1]+2)))
 _,vecs=np.linalg.eigh(np.cov(pts.T)); axis=vecs[:,-1]; angle=math.degrees(math.atan2(axis[1],axis[0]))
 patch=patch.rotate(angle,Image.Resampling.BICUBIC,expand=True); patches.append(patch.crop(patch.getbbox()))
ring=Image.new('RGBA',(1536,1536))
for j in range(48):
 angle=j*math.tau/48; radius=620+36*math.sin(angle*6)
 x=768+radius*math.cos(angle); y=768+radius*math.sin(angle)
 # Tangent follows the six-lobed ring rather than spinning the mouse/neuron stations.
 derivative=216*math.cos(angle*6)
 tx=derivative*math.cos(angle)-radius*math.sin(angle)
 ty=derivative*math.sin(angle)+radius*math.cos(angle)
 heading=math.degrees(math.atan2(ty,tx))
 patch=patches[j%len(patches)].resize((round(45+6*math.sin(j*2.3)),round(21+3*math.sin(j*1.7))),Image.Resampling.LANCZOS)
 patch=patch.rotate(-heading,Image.Resampling.BICUBIC,expand=True)
 ring.alpha_composite(patch,(round(x-patch.width/2),round(y-patch.height/2)))
save(ring,'DashedRing')
folder=Path(str(OUT)+'.meta')
if not folder.exists(): folder.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
print('Prepared six synchronized contraction pairs, one dashed ring and a clean dish.')

