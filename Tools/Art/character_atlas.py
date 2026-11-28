"""One 2048 atlas: skin resolution is reserved before cloth and small equipment."""
SIZE = 2048
# Pixel rectangles use the image's top-left origin. Blender UVs use bottom-left.
SLOTS = {i:(512+i%8*64,1536+i//8*64,64,64) for i in range(24)}
SLOTS.update({24:(0,0,768,768),25:(768,0,768,768),
              26:(1024,1536,256,256),27:(1280,1536,256,256),
              28:(1024,1792,256,256),29:(1280,1792,256,256),
              30:(1536,1536,256,256),31:(1792,1536,256,256),
              32:(512,1792,256,128),33:(768,1792,256,128),
              34:(512,1920,256,128),35:(768,1920,256,128),
              36:(1536,1792,128,128),37:(1664,1792,128,128),38:(1792,1792,128,128),
              40:(0,768,768,768),41:(768,768,768,768),
              42:(1536,0,512,512),43:(1536,512,512,512),
              44:(1536,1024,512,512),45:(0,1536,512,512)})


def uv(slot,u,v):
    x,y,w,h=SLOTS[slot]
    pad=2 if slot<24 else 3
    return ((x+pad+u*(w-pad*2))/SIZE,1-(y+pad+(1-v)*(h-pad*2))/SIZE)


def box(slot):
    x,y,w,h=SLOTS[slot]
    return x,y,x+w,y+h
