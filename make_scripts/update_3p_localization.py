#!/usr/bin/env python
# pylint: disable=C0103
"""Update third-party localization"""
import os
import shutil
from lxml import etree as ET
import git

pj = os.path.join
pn = os.path.normpath

script_path = os.path.dirname(os.path.realpath(__file__))
main_path = pn(pj(script_path, ".."))
third_party_path = pj(main_path, "third_party")
localization_path = pj(third_party_path, "Localization")
src_path = pj(main_path, "src")

epic_loc_keys = {}
with open(pj(script_path, "config", "epicLocKeys.txt"),
          "r", encoding="utf-8") as epic_loc_keys_content:
    for line in epic_loc_keys_content:
        if line := line.strip():
            epic_loc_keys[line] = ""

playnite_loc_keys = {}
with open(pj(script_path, "config", "playniteLocKeys.txt"),
          "r", encoding="utf-8") as playnite_loc_keys_content:
    for line in playnite_loc_keys_content:
        if line := line.strip():
            playnite_loc_keys[line] = ""

if os.path.exists(localization_path):
    shutil.rmtree(localization_path)
os.makedirs(localization_path)

xmlns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
xmlns_x = "http://schemas.microsoft.com/winfx/2006/xaml"
xmlns_sys = "clr-namespace:System;assembly=mscorlib"

NSMAP = {None: xmlns,
        "sys": xmlns_sys,
        "x":  xmlns_x}

# Copy shared localizations from Legendary
common_loc_path = pj(main_path, "..", "playnite-common-plugin", "src", "Localization")
for filename in os.listdir(common_loc_path):
    path = os.path.join(common_loc_path, filename)
    if os.path.isdir(path):
        continue
    common_loc = ET.parse(pj(common_loc_path, filename))

    xml_root = ET.Element("ResourceDictionary", nsmap=NSMAP)
    xml_doc = ET.ElementTree(xml_root)

    for child in common_loc.getroot():
        key = child.get(ET.QName(xmlns_x, "Key"))
        key_text = child.text
        if not key_text:
            key_text = ""
        new_key = ET.Element(ET.QName(xmlns_sys, "String"))
        new_key.set(ET.QName(xmlns_x, "Key"), key)
        new_key.text = key_text.replace("{PluginShortName}", "Legendary").replace("{AppName}", "Legendary").replace("{OriginalPluginShortName}", "Epic")
        xml_root.append(new_key)

    ET.indent(xml_doc, level=0)

    with open(pj(src_path, "Localization", filename), "w", encoding="utf-8") as i18n_file:
        i18n_file.write(ET.tostring(xml_doc, encoding="utf-8", xml_declaration=True, pretty_print=True).decode())

# Copy localizations from Playnite
for filename in os.listdir(pj(main_path, "..", "PlayniteExtensions", "PlayniteRepo", "source", "Playnite", "Localization")):
    git_repo = git.Repo(
        pj(main_path, "..", "PlayniteExtensions"), search_parent_directories=True)
    commit = git_repo.head.object.hexsha
    source = git_repo.remotes.origin.url.replace(".git", f"/tree/{commit}")
    Playnite_git_repo = git.Repo(pj(main_path, "..", "PlayniteExtensions", "PlayniteRepo"), search_parent_directories=True)
    commit2 = Playnite_git_repo.head.object.hexsha
    source2 = Playnite_git_repo.remotes.origin.url.replace(".git", f"/tree/{commit2}")


    xml_root = ET.Element("ResourceDictionary", nsmap=NSMAP)
    xml_doc = ET.ElementTree(xml_root)

    new_filename = filename
    if filename not in ["LocSource.xaml", "LocalizationKeys.cs", "locstatus.json"]:
        if filename == "en_US.xaml":
            new_filename = "LocSource.xaml"
        playnite_loc = ET.parse(pj(main_path, "..", "PlayniteExtensions",
                                "PlayniteRepo", "source", "Playnite", "Localization", new_filename))
        for child in playnite_loc.getroot():
            key = child.get(ET.QName(xmlns_x, "Key"))
            if key in playnite_loc_keys:
                key_text = child.text
                if not key_text:
                    key_text = ""
                new_key = ET.Element(ET.QName(xmlns_sys, "String"))
                new_key.set(ET.QName(xmlns_x, "Key"), key.replace("LOC", "LOCLegendary3P_Playnite"))
                new_key.text = key_text
                xml_root.append(new_key)

    if filename not in ["LocSource.xaml", "LocalizationKeys.cs", "locstatus.json"]:
        epic_loc = ET.parse(pj(main_path, "..", "PlayniteExtensions",
                            "source", "Libraries", "EpicLibrary", "Localization", filename))
        for child in epic_loc.getroot():
            key = child.get(ET.QName(xmlns_x, "Key"))
            if key in epic_loc_keys:
                key_text = child.text
                if not key_text:
                    key_text = ""
                new_key = ET.Element(ET.QName(xmlns_sys, "String"))
                new_key.set(ET.QName(xmlns_x, "Key"), key.replace("LOCEpic", "LOCLegendary3P_Epic"))
                new_key.text = key_text
                xml_root.append(new_key)

        ET.indent(xml_doc, level=0)
        with open(pj(localization_path, filename), "w", encoding="utf-8") as i18n_file:
            i18n_file.write("<?xml version='1.0' encoding='utf-8'?>\n")
            i18n_file.write(
                f'<!--\n  Automatically generated via update_3p_localization.py script using files from {source} and {source2}.\n  DO NOT MODIFY, CUZ IT MIGHT BE OVERWRITTEN DURING NEXT RUN!\n-->\n')
            i18n_file.write(ET.tostring(xml_doc, encoding="utf-8", pretty_print=True).decode())
