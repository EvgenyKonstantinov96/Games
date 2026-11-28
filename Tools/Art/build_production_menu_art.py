"""Prepare the new original menu illustration for fixed Unity UI viewports."""
import argparse
from pathlib import Path
from uuid import NAMESPACE_URL, uuid5
from PIL import Image, ImageOps

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/"Assets/Resources/UI"
parser=argparse.ArgumentParser()
parser.add_argument("source",type=Path,nargs="?",default=ROOT/"Tools/Art/Source/survivors-menu.png")
args=parser.parse_args()
source=Image.open(args.source).convert("RGB")


def save(image,name):
    target=OUT/(name+".png")
    image.save(target,optimize=True)
    meta=target.with_suffix(".png.meta")
    if not meta.exists():
        text=(OUT/"campaign_cover_max.png.meta").read_text()
        import re
        text=re.sub(r"guid: [0-9a-f]{32}","guid: "+uuid5(NAMESPACE_URL,target.as_posix()).hex,text)
        meta.write_text(text)


save(ImageOps.fit(source,(1024,1050),Image.Resampling.LANCZOS,centering=(.5,.25)),"survivors_cover")
for hero,left,right in (("max",0,.5),("eva",.5,1)):
    crop=source.crop((round(left*source.width),0,round(right*source.width),round(source.height*.81)))
    save(ImageOps.fit(crop,(512,734),Image.Resampling.LANCZOS,centering=(.5,.12)),"hero_"+hero)
for hero,box in (("max",(.16,.055,.37,.265)),("eva",(.60,.10,.84,.34))):
    face=source.crop(tuple(round(value*source.width) for value in box))
    save(face.resize((256,256),Image.Resampling.LANCZOS),"dialogue_face_"+hero)
