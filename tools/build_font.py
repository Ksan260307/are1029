"""Builds NumberRoad/Assets/NumberRoad/Resources/Fonts/UIFont.ttf for the Japanese UI.

WebGL cannot use the phone's own fonts, so the game ships one: Noto Sans JP (SIL Open Font License 1.1), set to a
bold weight and cut down to the characters the game's text uses (English plus the Japanese table in LocJa.cs), which
keeps it small. Run it again after adding text:

    python tools/build_font.py [--source PATH] [--weight 800]

The default source is the Noto Sans JP that comes with Windows 11 (C:\\Windows\\Fonts\\NotoSansJP-VF.ttf); any copy
of Noto Sans JP from https://github.com/notofonts/noto-cjk works too. The licence notice is written next to the font.
"""
import argparse
import os
import re
import sys

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SCRIPTS = os.path.join(ROOT, "NumberRoad", "Assets", "NumberRoad", "Scripts")
FONTS = os.path.join(ROOT, "NumberRoad", "Assets", "NumberRoad", "Resources", "Fonts")
DEFAULT_SOURCE = r"C:\Windows\Fonts\NotoSansJP-VF.ttf"
# punctuation the translations may use even when no string has it yet
EXTRA = "×÷…・「」『』（）！？：、。ー〜％＋－→←↑↓★☆●○"
LITERAL = re.compile(r'"((?:[^"\\]|\\.)*)"')


def characters():
    """Printable ASCII plus every other character that appears in a string literal of the game's scripts."""
    chars = {chr(c) for c in range(0x20, 0x7F)}
    chars.update(EXTRA)
    for dirpath, _, files in os.walk(SCRIPTS):
        for name in files:
            if name.endswith(".cs"):
                with open(os.path.join(dirpath, name), encoding="utf-8") as f:
                    for lit in LITERAL.findall(f.read()):
                        chars.update(c for c in lit if ord(c) >= 0x80)
    return "".join(sorted(chars))


def notice(font):
    names = {}
    for rec in font["name"].names:
        if rec.platformID == 3 and rec.langID == 0x409:
            names[rec.nameID] = rec.toUnicode()
    return (
        "UIFont.ttf is a subset of {family}, version {version}.\n"
        "{copyright}\n\n"
        "{license}\n{url}\n\n"
        "Built by tools/build_font.py: set to a bold weight and cut down to the characters the game uses.\n"
    ).format(family=names.get(1, "Noto Sans JP"), version=names.get(5, "").replace("Version ", ""),
             copyright=names.get(0, ""), license=names.get(13, "Licensed under the SIL Open Font License, Version 1.1."),
             url=names.get(14, "https://openfontlicense.org"))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--source", default=DEFAULT_SOURCE)
    parser.add_argument("--weight", type=float, default=800)
    parser.add_argument("--out", default=os.path.join(FONTS, "UIFont.ttf"))
    args = parser.parse_args()

    font = TTFont(args.source)
    if "fvar" in font:
        axes = {a.axisTag: a for a in font["fvar"].axes}
        pins = {tag: (args.weight if tag == "wght" else a.defaultValue) for tag, a in axes.items()}
        font = instancer.instantiateVariableFont(font, pins)

    options = subset.Options()
    options.name_IDs = ["*"]
    options.name_languages = ["*"]
    options.name_legacy = True
    options.layout_features = []
    options.hinting = False
    options.notdef_outline = True
    subsetter = subset.Subsetter(options)
    text = characters()
    subsetter.populate(text=text)
    subsetter.subset(font)

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    font.save(args.out)
    with open(os.path.join(os.path.dirname(args.out), "UIFont-LICENSE.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write(notice(font))
    size = os.path.getsize(args.out)
    sys.stdout.reconfigure(encoding="utf-8")
    print(f"[build_font] {len(text)} characters, {size / 1024:.0f} KB -> {args.out}")


if __name__ == "__main__":
    main()
