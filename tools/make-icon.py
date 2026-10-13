"""Generate the app's small geometric icon; no external artwork required."""
from PIL import Image, ImageDraw
from pathlib import Path
root=Path(__file__).resolve().parents[1]
image=Image.new('RGBA',(256,256),(0,0,0,0))
d=ImageDraw.Draw(image)
d.rounded_rectangle((8,8,248,248),radius=58,fill='#102C35')
d.rounded_rectangle((52,70,204,106),radius=18,fill='#54D6B7')
d.rounded_rectangle((52,146,165,182),radius=18,fill='#8EB8FF')
image.save(root/'src'/'QuotaFloat'/'app.ico',sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
