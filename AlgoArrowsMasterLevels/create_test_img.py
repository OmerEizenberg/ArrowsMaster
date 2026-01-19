from PIL import Image, ImageDraw
import os

def create_circle(size=33):
    img = Image.new('RGBA', (size, size), (255, 255, 255, 0))
    draw = ImageDraw.Draw(img)
    margin = 2
    draw.ellipse([margin, margin, size-margin-1, size-margin-1], fill=(255, 0, 0, 255))
    img.save('circ.png')
    print(f"Created circ.png with size {size}x{size}")

if __name__ == "__main__":
    create_circle(33)
