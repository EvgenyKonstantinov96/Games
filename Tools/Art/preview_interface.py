"""Reconstruct exported C# UI coordinates with Pillow. These are not Unity screenshots."""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

import argparse
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('layout',type=Path,help='Directory exported by check_interface.py --output')
args=parser.parse_args()
ROOT=Path(__file__).resolve().parents[2]/'Assets/Resources'
SCALE = 2


def pixels(n):
    return round(n*SCALE)


def channels(bands):
    return tuple(round(max(0,min(1,v))*255) for v in bands)


def sliced(src, width, height):
    dest = Image.new('RGBA',(width,height))
    border = 20
    x_dst = [0,min(border, width//2),width-min(border,width//2),width]
    y_dst = [0,min(border, height//2),height-min(border,height//2),height]
    x_src = [0,border,src.width-border,src.width]
    y_src = [0,border,src.height-border,src.height]
    for y in range(3):
        for x in range(3):
            size = (x_dst[x+1]-x_dst[x], y_dst[y+1]-y_dst[y])
            if min(size) < 1:
                continue
            cell = src.crop((x_src[x],y_src[y],x_src[x+1],y_src[y+1])).resize(size,Image.Resampling.BILINEAR)
            dest.paste(cell,(x_dst[x],y_dst[y]))
    return dest


def render(name):
    page = json.loads((args.layout/(name+'.json')).read_text())
    result = Image.new('RGBA',(pixels(page['width']),pixels(page['height'])), '#0a111c')
    for item in page['items']:
        x,y,w,h = (pixels(item[k]) for k in ['x','y','w','h'])
        if min(w,h) <= 0:
            continue
        rgba = channels(item['color'])
        if item['kind']=='text':
            layer = Image.new('RGBA',(w,h))
            font = ImageFont.truetype(str(ROOT/((item.get('font') or 'UI/ZSRMenuFont')+'.ttf')),max(1,pixels(item['size'])))
            draw = ImageDraw.Draw(layer)
            centered=item['align'].endswith('Center')
            right=item['align'].endswith('Right')
            upper=item['align'].startswith('Upper')
            lines=[]
            for paragraph in item['text'].split('\n'):
                line=''
                for word in paragraph.split():
                    test=(line+' '+word).strip()
                    if draw.textlength(test,font=font)>w and line:
                        lines.append(line)
                        line=word
                    else:
                        line=test
                lines.append(line)
            line_height=pixels(item['size']*1.18)
            ink = line_height * len(lines)
            if ink > h + pixels(item['size'] * .44) and item['text']:
                print(f"{name} overflow {item['text']} (uses {ink/SCALE:.1f} in {h/SCALE:.1f})")
            start=line_height*.5 if upper else h/2-(len(lines)-1)*.5*line_height
            for j,line in enumerate(lines):
                draw.text((w/2 if centered else w if right else 0,start+j*line_height),line,
                          fill=rgba,font=font,anchor='mm' if centered else 'rm' if right else 'lm')
        elif item.get('path'):
            src_path=ROOT/(item['path']+'.png')
            if not src_path.exists():
                src_path=src_path.with_suffix('.jpg')
            src = Image.open(src_path).convert('RGBA')
            if item['kind']=='raw':
                u,v,du,dv=item['uv']
                src=src.crop((round(u*src.width),round((1-v-dv)*src.height),round((u+du)*src.width),round((1-v)*src.height)))
                layer = src.resize((w,h),Image.Resampling.LANCZOS)
            else:
                layer = sliced(src,w,h)
            values=np.asarray(layer,dtype=float)
            if not item.get('enabled',True):
                values *= (.70,.74,.79,.80)
            layer=Image.fromarray(np.uint8(values*np.asarray(rgba)/255))
        else:
            layer=Image.new('RGBA',(w,h),rgba)
        result.alpha_composite(layer,(x,y))
    return result.convert('RGB')


pages = [render(k) for k in ('home','heroes','arsenal','items')]
size = (415,898)
combined=Image.new('RGB',(size[0]*2+10,size[1]*2+10),'#111b29')
for i,page in enumerate(pages):
    combined.paste(page.resize(size,Image.Resampling.LANCZOS),((i%2)*(size[0]+10),(i//2)*(size[1]+10)))
combined.save(args.layout/'production-menu-preview.jpg',quality=91,optimize=True)

faces = [render(k) for k in ('dialogue-max','dialogue-eva')]
duo = Image.new('RGB',(970,1039),'#111b29')
for i,img in enumerate(faces):
    duo.paste(img.resize((480,1039),Image.Resampling.LANCZOS),(490*i,0))
duo.save(args.layout/'production-dialogue-preview.jpg',quality=92,optimize=True)
