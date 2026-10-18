# 외부 픽셀 아트 시트(Assets/_External/...)를 잘라서 한 장의 아틀라스 + 애니메이션 표로 만든다.
# 결과: Assets/_External/_Generated/Resources/PixelAtlas.png (+ .meta: 점 필터/무압축), PixelAtlasData.json
# 게임은 이 파일을 Resources 로 읽어(World/PixelArt.cs) 없으면 예전 도형 그림으로 돌아간다. _External 은 .gitignore 라 아틀라스는 올라가지 않는다.
# 쓰는 법:  python Tools/build-pixel-atlas.py     (시트 파일이 바뀌거나 캐릭터 목록을 바꿨을 때 다시 실행하고 Unity 가 다시 불러오게 한다)
# 새 캐릭터를 넣으려면 아래 build_sets() 에 한 줄 추가. 시트 칸 크기와 줄 번호는 Aseprite 태그(.ase)를 보고 정했다.
import json
import os
import sys
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_External")
OUT_DIR = os.path.join(ROOT, "_Generated", "Resources")
NPC = os.path.join(ROOT, "GandalfHardcore Modern NPCs")
ZOM = os.path.join(ROOT, "GandalfHardcore Zombies")
ENE = os.path.join(ROOT, "GandalfHardcore Modern Pixel Art Enemies")
FARM = os.path.join(ROOT, "GandalfHardcore Farm Animals")
HELL = os.path.join(ROOT, "GandalfHardcore Hell Asset Pack 32x32", "GandalfHardcore The Damned Enemies and NPCs")


def load(path):
    return Image.open(path).convert("RGBA")


def cell(img, cw, ch, col, row):
    return img.crop((col * cw, row * ch, (col + 1) * cw, (row + 1) * ch))


def row_frames(img, cw, ch, row, count=None):
    out = []
    for c in range(img.size[0] // cw):
        f = cell(img, cw, ch, c, row)
        if f.split()[3].getbbox():
            out.append(f)
    return out[:count] if count else out


def seq_frames(img, cw, ch, first, last):
    cols = img.size[0] // cw
    return [cell(img, cw, ch, i % cols, i // cols) for i in range(first, last + 1)]


class S:
    def __init__(self, cat, name, ppu_mode, target):
        self.cat, self.name, self.mode, self.target = cat, name, ppu_mode, target
        self.clips = []  # (name, fps, loop, [frames])
        self.center = False   # True 면 칸 가운데를 기준점으로 (공중에 뜬 그림)
        self.extra = {}

    def add(self, name, fps, loop, frames):
        if frames:
            self.clips.append((name, fps, loop, frames))
        return self


def build_sets():
    sets = []

    # 보행자: 줄 0 = 서 있기(5칸), 줄 1 = 걷기(8칸)
    for n in ["Biker Girl", "Businessman", "Female doctor", "Japanese guy", "Noir Lady", "Nurse",
              "Cowgirl", "Dealer", "Latina Nurse", "Secretary", "Ambulance", "Photographer"]:
        im = load(os.path.join(NPC, n + " sheet.png"))
        sets.append(S("npc", n, "h", 1.75)
                    .add("idle", 6, True, row_frames(im, 64, 64, 0))
                    .add("walk", 10, True, row_frames(im, 64, 64, 1)))
    # 경찰/강도/특공대: 줄 0 서기, 줄 2 달리기(8칸), 줄 3 쓰러짐(7칸)
    for n in ["Policeman sheet", "Policeman sheet 2", "Policeman woman sheet", "SWAT sheet", "SWAT sheet 2"]:
        im = load(os.path.join(NPC, n + ".png"))
        sets.append(S("npc", n.replace(" sheet", ""), "h", 1.75)
                    .add("idle", 6, True, row_frames(im, 64, 64, 0))
                    .add("walk", 11, True, row_frames(im, 64, 64, 2))
                    .add("death", 10, False, row_frames(im, 64, 64, 3)))

    # 은행 강도 손님 전용: Robber (줄 0 서기, 줄 2 달리기, 줄 3 쓰러짐)
    im = load(os.path.join(NPC, "Robber sheet.png"))
    sets.append(S("robber", "Robber", "h", 1.75)
                .add("idle", 6, True, row_frames(im, 64, 64, 0))
                .add("walk", 11, True, row_frames(im, 64, 64, 2))
                .add("death", 10, False, row_frames(im, 64, 64, 3)))

    # 좀비 4종: 한 줄로 이어진 프레임 (Idle 0-6, Walk 7-14, Death 41-54)
    for v in range(1, 5):
        im = load(os.path.join(ZOM, "GandalfHardcore Zombie v%d sheet.png" % v))
        sets.append(S("zombie", "Zombie%d" % v, "h", 1.75)
                    .add("idle", 6, True, seq_frames(im, 64, 64, 0, 6))
                    .add("walk", 8, True, seq_frames(im, 64, 64, 7, 14))
                    .add("death", 14, False, seq_frames(im, 64, 64, 41, 54)))

    # 외계인: 줄 0 서기, 줄 2 달리기, 줄 6 사라짐
    im = load(os.path.join(ENE, "GandalfHardcore Alien sheet.png"))
    sets.append(S("alien", "Alien", "h", 1.5)
                .add("idle", 6, True, row_frames(im, 64, 64, 0))
                .add("walk", 10, True, row_frames(im, 64, 64, 2))
                .add("death", 10, False, row_frames(im, 64, 64, 6)))

    # UFO: GandalfHardcore UFO sheet (160x64, 8칸). 칸 가운데가 기준점. 레이저는 접시 아래 가운데에서 땅으로 쏜다(게임이 그린다): 가운데에서 아래로 18픽셀
    im = load(os.path.join(ENE, "GandalfHardcore UFO sheet.png"))
    ufo = S("ufo", "UFO", "w", 12.0)
    ufo.center = True
    ufo.extra = {"mx": 0, "my": -18}
    sets.append(ufo.add("idle", 10, True, row_frames(im, 160, 64, 0)))

    # 돼지 3종: 80x48, 줄 0 서기(8), 줄 1 걷기(7)
    for n in ["Pink Pig", "Brown Pig", "Black Pig"]:
        im = load(os.path.join(FARM, n + " sheet.png"))
        sets.append(S("pig", n.replace(" ", ""), "w", 1.45)
                    .add("idle", 6, True, row_frames(im, 80, 48, 0))
                    .add("walk", 9, True, row_frames(im, 80, 48, 1)))

    # 경찰차 (옆모습, 128x48): 줄 0 = 경광등이 번갈아 켜지는 5칸
    im = load(os.path.join(NPC, "Police car sheet.png"))
    sets.append(S("police", "PoliceCar", "w", 4.4).add("idle", 6, True, row_frames(im, 128, 48, 0)))

    # 로비의 주인공 택시: Retro car (옆모습 128x48, 줄 0 서 있기 5칸, 줄 1 달리기 4칸). 오른쪽을 본다
    im = load(os.path.join(NPC, "Retro car sheet.png"))
    sets.append(S("lobbycar", "RetroCar", "w", 5.0)
                .add("idle", 6, True, row_frames(im, 128, 48, 0))
                .add("walk", 10, True, row_frames(im, 128, 48, 1)))

    # 로비에 세워 둔 차들(옆모습 128x48, 줄 0 = 5칸): 사장님 차(하얀 고급차), 동료 택시로 쓸 머슬카 둘. 오른쪽을 본다
    for n, key in [("Luxury car sheet", "LuxuryCar"), ("Muscle car sheet", "MuscleCar"), ("Old muscle car sheet", "OldMuscleCar")]:
        im = load(os.path.join(NPC, n + ".png"))
        sets.append(S("parkcar", key, "w", 5.0).add("idle", 4, True, row_frames(im, 128, 48, 0)))

    # 괴수: 가마솥 악마 "The Devil" (96x112, 한 줄 7칸. 정면을 보고 있다)
    im = load(os.path.join(HELL, "The Devil.png"))
    sets.append(S("kaiju", "TheDevil", "w", 16.0).add("idle", 6, True, row_frames(im, 96, 112, 0, 7)))

    # 손님(길가에서 기다리는 사람): 걷기 없이 가만히 서 있는 그림들. 사람 크기는 걷는 사람과 같게 고정(앉은 그림이 커지지 않게)
    for n, row in [("Lady with handbag sheet", 0), ("Lady with tablet sheet", 0), ("Military lady sheet", 0), ("Coffee girl sheet", 0), ("Bodybuilder sheet", 0), ("Flight Attendant sheet", 0)]:
        im = load(os.path.join(NPC, n + ".png"))
        sets.append(S("stand", n.replace(" sheet", ""), "p", 25.7).add("idle", 6, True, row_frames(im, 64, 64, row)))

    # 택시 회사 사장(로비의 상점 NPC): 안락의자에 앉은 마피아 보스
    im = load(os.path.join(NPC, "Mafia Boss sheet.png"))
    sets.append(S("boss", "Boss", "p", 25.7).add("idle", 5, True, row_frames(im, 64, 64, 0)))
    return sets


def metrics(s):
    """첫 칸(서 있기)의 몸 크기로 한 칸이 몇 픽셀/미터인지(ppu)와 발 위치(pivot)를 정한다."""
    f = s.clips[0][3][0]
    bb = f.split()[3].getbbox()
    left, top, right, bottom = bb
    size = (bottom - top) if s.mode == "h" else (right - left)
    ppu = s.target if s.mode == "p" else size / s.target       # "p" = 칸 크기를 그대로 (픽셀/미터를 직접 정한다)
    px = (left + right) / 2.0
    py = f.size[1] - bottom            # 아래에서부터 발까지 (픽셀)
    if s.center:
        px, py = f.size[0] / 2.0, f.size[1] / 2.0
    return ppu, px, py


def head_rect(s):
    """대화창 얼굴용: 첫 칸(서 있기)에서 몸이 시작하는 맨 위 줄부터 키의 약 1/3 높이까지를 머리로 보고,
    그 안에서 그림이 있는 가로 범위를 정사각형으로 맞춰 [x, y, w, h](칸 안 좌표, 위에서부터)로 돌려준다."""
    f = s.clips[0][3][0]
    alpha = f.split()[3]
    left, top, right, bottom = alpha.getbbox()
    frac = 0.28 if s.cat in ("npc", "stand", "boss", "robber") else (0.34 if s.cat == "zombie" else 0.5)
    hh = max(6, int(round((bottom - top) * frac)))
    band = alpha.crop((0, top, f.size[0], min(f.size[1], top + hh)))
    bl, bt, br, bb = band.getbbox()
    # 가로 범위는 머리가 있는 위쪽 60% 줄로만 정한다 (들고 있는 총/몽둥이가 얼굴 칸을 넓히지 않게)
    upper = alpha.crop((0, top, f.size[0], top + max(4, int(hh * 0.6)))).getbbox()
    if upper:
        bl, br = upper[0], upper[2]
        bb = max(bt + 1, min(bb, bt + (br - bl)))
    side = max(br - bl, bb - bt) + 2
    cx = (bl + br) / 2.0
    x = int(round(cx - side / 2.0))
    y = top + bt - 1
    x = max(0, min(f.size[0] - side, x))
    y = max(0, min(f.size[1] - side, y))
    side = min(side, f.size[0], f.size[1])
    return [x, y, side, side]


def bust_rect(s, head):
    """룸미러용 상반신: 머리 맨 위부터 가슴께(키의 절반쯤)까지를 정사각형으로 [x, y, w, h]."""
    f = s.clips[0][3][0]
    left, top, right, bottom = f.split()[3].getbbox()
    frac = 0.52 if s.cat in ("npc", "stand", "boss", "robber") else (0.58 if s.cat == "zombie" else 0.9)
    side = max(head[2] + 6, int(round((bottom - top) * frac)))
    cx = head[0] + head[2] / 2.0
    x = int(round(cx - side / 2.0))
    y = max(0, head[1] - 1)
    side = min(side, f.size[0], f.size[1])
    x = max(0, min(f.size[0] - side, x))
    y = max(0, min(f.size[1] - side, y))
    return [x, y, side, side]


def main():
    sets = build_sets()
    items = []  # (set, clip index, frame index, image)
    for si, s in enumerate(sets):
        for ci, (name, fps, loop, frames) in enumerate(s.clips):
            for fi, f in enumerate(frames):
                items.append((si, ci, fi, f))

    # 높이가 같은 것끼리 줄을 맞춰 위에서부터 채운다
    items.sort(key=lambda it: (-it[3].size[1], it[3].size[0]))
    W, gap = 2048, 2
    x = y = rowh = 0
    pos = {}
    for si, ci, fi, f in items:
        w, h = f.size
        if x + w > W:
            x = 0
            y += rowh + gap
            rowh = 0
        pos[(si, ci, fi)] = (x, y)
        x += w + gap
        rowh = max(rowh, h)
    H = 1
    while H < y + rowh:
        H *= 2
    atlas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for si, ci, fi, f in items:
        atlas.paste(f, pos[(si, ci, fi)])

    data = {"w": W, "h": H, "sets": []}
    for si, s in enumerate(sets):
        ppu, px, py = metrics(s)
        clips = []
        for ci, (name, fps, loop, frames) in enumerate(s.clips):
            rects = []          # JsonUtility 가 안쪽 배열을 못 읽어서 x,y,w,h 를 한 줄로 이어 붙인다
            for fi, f in enumerate(frames):
                ax, ay = pos[(si, ci, fi)]
                rects += [ax, ay, f.size[0], f.size[1]]
            clips.append({"name": name, "fps": fps, "loop": loop, "rects": rects})
        # 그림이 기본으로 어느 쪽을 보는가: 사람/좀비/외계인은 왼쪽, 돼지/경찰차는 오른쪽 (게임이 방향에 맞춰 뒤집는다)
        face_left = s.cat in ("npc", "zombie", "alien", "stand", "boss", "robber")
        data["sets"].append({"cat": s.cat, "name": s.name, "ppu": round(ppu, 3), "px": px, "py": py, "faceLeft": face_left, "head": head_rect(s), "bust": bust_rect(s, head_rect(s)), "clips": clips, **s.extra})

    os.makedirs(OUT_DIR, exist_ok=True)
    png = os.path.join(OUT_DIR, "PixelAtlas.png")
    atlas.save(png)
    with open(os.path.join(OUT_DIR, "PixelAtlasData.json"), "w", encoding="utf-8") as fh:
        json.dump(data, fh, ensure_ascii=False)

    # 하얀 실루엣 버전: 같은 배치에서 그림이 있는 칸을 전부 흰색으로 칠한 것 (무단횡단하려는 사람이 하얗게 깜빡이는 연출용)
    white = atlas.copy()
    px = white.load()
    for yy in range(white.size[1]):
        for xx in range(white.size[0]):
            a = px[xx, yy][3]
            if a:
                px[xx, yy] = (255, 255, 255, a)
    wpng = os.path.join(OUT_DIR, "PixelAtlasWhite.png")
    white.save(wpng)

    # 유니티가 처음 불러올 때 점 필터/무압축으로 읽게 .meta 를 미리 만든다 (이미 있으면 건드리지 않는다)
    meta = png + ".meta"
    if not os.path.exists(meta):
        with open(meta, "w", encoding="utf-8") as fh:
            fh.write(META)
    wmeta = wpng + ".meta"
    if not os.path.exists(wmeta):
        with open(wmeta, "w", encoding="utf-8") as fh:
            fh.write(META.replace("7f3c2a9d5b1e4c6f8a0d9e2b4c7a1f53", "7f3c2a9d5b1e4c6f8a0d9e2b4c7a1f54"))
    print("아틀라스 %dx%d, 세트 %d개, 칸 %d개 -> %s" % (W, H, len(sets), len(items), png))


META = """fileFormatVersion: 2
guid: 7f3c2a9d5b1e4c6f8a0d9e2b4c7a1f53
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 4096
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 4096
    resizeAlgorithm: 0
    textureFormat: 4
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 1
    ignorePlatformSupport: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

if __name__ == "__main__":
    sys.exit(main())
