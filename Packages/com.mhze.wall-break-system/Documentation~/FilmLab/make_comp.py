import cv2, os, shutil
import numpy as np

frames_dir = os.environ.get('WALL_FRAMES', 'Frames')
comp = os.environ.get('WALL_COMP', 'comp')
shutil.rmtree(comp, ignore_errors=True)
os.makedirs(comp)
count = len([f for f in os.listdir(frames_dir) if f.startswith('A_')])
W, H = 960, 540
fps = 30
hit_times = [0.7, 1.7, 2.6, 3.4, 4.1, 4.8]

def label(img, text, x, y, scale):
    cv2.putText(img, text, (x, y), cv2.FONT_HERSHEY_SIMPLEX, scale, (0, 0, 0), 4, cv2.LINE_AA)
    cv2.putText(img, text, (x, y), cv2.FONT_HERSHEY_SIMPLEX, scale, (255, 255, 255), 2, cv2.LINE_AA)

def frame(i, caption):
    a = cv2.imread(os.path.join(frames_dir, f'A_{i:04d}.jpg'))
    b = cv2.imread(os.path.join(frames_dir, f'B_{i:04d}.jpg'))
    label(a, 'Player side', 14, 34, 0.9)
    label(b, 'Behind the wall', 14, 34, 0.9)
    img = np.hstack([a, b])
    label(img, caption, 14, H - 18, 0.8)
    return img

n = 0
def emit(img, times=1):
    global n
    for _ in range(times):
        n += 1
        cv2.imwrite(os.path.join(comp, f'f_{n:05d}.jpg'), img, [cv2.IMWRITE_JPEG_QUALITY, 93])

for i in range(count):
    t = i / fps
    k = sum(1 for h in hit_times if t >= h)
    emit(frame(i, f'Real time    blows: {k}/6' + ('    THE WALL GIVES WAY' if k == 6 else '')))
start, end = int(4.5 * fps), min(int(8.5 * fps), count)
for i in range(start, end):
    emit(frame(i, 'Slow motion x3: the last blow and the collapse'), 3)
print('frames', n)
