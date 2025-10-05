#!/usr/bin/env python
# pylint: disable=C0103
"""Update third-party localization"""
import os
import shutil
from lxml import etree as ET
from pathlib import Path
import git
import importlib.util

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

git_repo = git.Repo(
    pj(main_path, "..", "PlayniteExtensions"), search_parent_directories=True)
commit = git_repo.head.object.hexsha
source = git_repo.remotes.origin.url.replace(".git", f"/tree/{commit}")
Playnite_git_repo = git.Repo(pj(main_path, "..", "PlayniteExtensions", "PlayniteRepo"), search_parent_directories=True)
commit2 = Playnite_git_repo.head.object.hexsha
source2 = Playnite_git_repo.remotes.origin.url.replace(".git", f"/tree/{commit2}")

FTL_script_path = pn(pj(main_path, '..', "playnite-common-plugin", "make_scripts", "convert_to_ftl.py"))
spec = importlib.util.spec_from_file_location("convert_to_ftl", FTL_script_path)
convertToFtl_script = importlib.util.module_from_spec(spec)
spec.loader.exec_module(convertToFtl_script)

# Copy localizations from Playnite
for filename in os.listdir(pj(main_path, "..", "PlayniteExtensions", "PlayniteRepo", "source", "Playnite", "Localization")):
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
                if key_text != "":
                    xml_root.append(new_key)

    if filename not in ["LocSource.xaml", "LocalizationKeys.cs", "locstatus.json"]:
        loc_sub_dir = filename.replace("_", "-").replace(".xaml", "")
        epic_file_path = pj(main_path, "..", "PlayniteExtensions",
                            "source", "Libraries", "EpicLibrary", "Localization", filename)
        if os.path.isfile(epic_file_path):
            epic_loc = ET.parse(epic_file_path)
            for child in epic_loc.getroot():
                key = child.get(ET.QName(xmlns_x, "Key"))
                if key in epic_loc_keys:
                    key_text = child.text
                    if not key_text:
                        key_text = ""
                    new_key = ET.Element(ET.QName(xmlns_sys, "String"))
                    new_key.set(ET.QName(xmlns_x, "Key"), key.replace("LOCEpic", "LOCLegendary3P_Epic"))
                    new_key.text = key_text
                    if key_text != "":
                        xml_root.append(new_key)
    
        ET.indent(xml_doc, level=0)
        os.makedirs(pj(localization_path, loc_sub_dir))

        comment = f"""###
### Automatically generated via update_3p_localization.py script using files from 
### {source} and 
### {source2}. 
### DO NOT MODIFY, CUZ IT MIGHT BE OVERWRITTEN DURING NEXT RUN!
###
"""
        if len(xml_doc.getroot()) > 0:
            with open(pj(localization_path, loc_sub_dir, "third-party.ftl"), "w", encoding="utf-8") as i18n_file:
                i18n_file.write(f"{comment}")
                ftl_new_content = convertToFtl_script.convertXamlStringToFtl(xml_doc, "Legendary")
                i18n_file.write(ftl_new_content )

third_party_loc_path = pj(main_path, "third_party", "Localization")
for root, dirs, files in os.walk(third_party_loc_path):
    for filename in files:
        if filename.endswith(".xaml"):
            os.remove(pj(root, filename))


# Copy shared localizations
def copy_specific_named_files_with_subdirs(source_dir, destination_dir, file_names=None):
    if not os.path.exists(destination_dir):
        os.makedirs(destination_dir)

    for root, dirs, files in os.walk(source_dir):
        relative_path = os.path.relpath(root, source_dir)
        if relative_path == '.':
            continue
        
        dest_subdir = os.path.join(destination_dir, relative_path)
        
        if not os.path.exists(dest_subdir):
            os.makedirs(dest_subdir)

        for file in files:
            if file_names is None or file in file_names:
                source_file_path = os.path.join(root, file)
                destination_file_path = os.path.join(dest_subdir, file)
                
                try:
                    shutil.copy2(source_file_path, destination_file_path)
                    print(f"Copied: {source_file_path} -> {destination_file_path}")
                except Exception as e:
                    print(f"An error occured during copying file {source_file_path}: {e}")

common_loc_path = pj(main_path, "..", "playnite-common-plugin", "src", "Localization")
copy_specific_named_files_with_subdirs(common_loc_path, pj(third_party_path, "CommonLocalization"))
