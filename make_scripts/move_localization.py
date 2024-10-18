#!/usr/bin/env python
# pylint: disable=C0103
"""Move localization from file to file"""
import os
from lxml import etree as ET

pj = os.path.join
pn = os.path.normpath

script_path = os.path.dirname(os.path.realpath(__file__))
main_path = pn(pj(script_path, ".."))
src_path = pj(main_path, "src")
localization_path = pj(src_path, "Localization")


keys_to_move = {}
with open(pj(script_path, "config", "stringsToMove.txt"),
          "r", encoding="utf-8") as keys_to_move_content:
    for line in keys_to_move_content:
        if line := line.strip():
            keys_to_move[line] = ""

xmlns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
xmlns_x = "http://schemas.microsoft.com/winfx/2006/xaml"
xmlns_sys = "clr-namespace:System;assembly=mscorlib"

NSMAP = {None: xmlns,
        "sys": xmlns_sys,
        "x":  xmlns_x}

for filename in os.listdir(pj(localization_path)):
    # Move strings
    path = os.path.join(localization_path, filename)
    if os.path.isdir(path):
        continue
    if "legendary" in filename:
        continue
    source_loc = ET.parse(pj(localization_path, filename))

    xml_root = ET.Element("ResourceDictionary", nsmap=NSMAP)
    xml_doc = ET.ElementTree(xml_root)

    source_root = source_loc.getroot()

    for child in source_root:
        key = child.get(ET.QName(xmlns_x, "Key"))
        if key in keys_to_move:
            child.getparent().remove(child)
            xml_root.append(child)

    legendary_loc_file = pj(src_path, "Localization", f"{os.path.splitext(filename)[0]}-legendary.xaml")
    if os.path.exists(legendary_loc_file):
        legendary_tree = ET.parse(legendary_loc_file)
        for child in legendary_tree.getroot():
            key = child.get(ET.QName(xmlns_x, "Key"))
            if not key in keys_to_move:
                xml_root.append(child)


    ET.indent(xml_doc, level=0)
    ET.indent(source_root, level=0)

    with open(pj(src_path, "Localization", f"{os.path.splitext(filename)[0]}-legendary.xaml"), "w", encoding="utf-8") as i18n_file:
        i18n_file.write(ET.tostring(xml_doc, encoding="utf-8", xml_declaration=True, pretty_print=True).decode())

    # Remove strings from source
    with open(pj(src_path, "Localization", f"{filename}"), "w", encoding="utf-8") as i18n_file:
        i18n_file.write(ET.tostring(source_root, encoding="utf-8", xml_declaration=True, pretty_print=True).decode())
