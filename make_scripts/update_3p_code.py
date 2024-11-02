#!/usr/bin/env python
# pylint: disable=C0103
# pylint: disable=C0301
# pylint: disable=E1129
# pylint: disable=E1136
"""Update third-party code"""
import os
import xml.etree.ElementTree as ET
import shutil
import git

pj = os.path.join
pn = os.path.normpath

script_path = os.path.dirname(os.path.realpath(__file__))
main_path = pn(pj(script_path, ".."))
third_party_path = pj(main_path, "third_party")
src_path = pj(main_path, "src")

if os.path.exists(pj(third_party_path, "PlayniteExtensions")):
    shutil.rmtree(pj(third_party_path, "PlayniteExtensions"))

legendary_csproj = ET.parse(pj(src_path, "LegendaryLibrary.csproj"))
xml_ns = "{http://schemas.microsoft.com/developer/msbuild/2003}"
for child in legendary_csproj.getroot():
    if child.tag == f"{xml_ns}ItemGroup":
        if "Label" in child.attrib:
            for compile_items in child:
                needed_file = compile_items.get('Include').replace("..\\third_party\\", "")
                needed_file = pn(pj(main_path, "..", needed_file))
                dst = os.path.relpath(os.path.dirname(needed_file), pj(main_path, ".."))
                dst = pj(third_party_path, dst)
                if not os.path.exists(dst):
                    os.makedirs(dst)
                shutil.copy(needed_file, pj(dst, os.path.basename(needed_file)))

shutil.copy(pj(main_path, "..", "PlayniteExtensions", "LICENSE.md"), pj(third_party_path, "PlayniteExtensions", "LICENSE.md"))
shutil.copy(pj(main_path, "..", "PlayniteExtensions", "PlayniteRepo", "LICENSE.md"), pj(third_party_path, "PlayniteExtensions", "PlayniteRepo", "LICENSE.md"))

with open(pj(third_party_path, "PlayniteExtensions", "SOURCE_INFO.txt"), "w", encoding="utf-8") as source_info:
    git_repo = git.Repo(pj(main_path, "..", "PlayniteExtensions"), search_parent_directories=True)
    source = git_repo.remotes.origin.url
    source_info.write(f"Source: {source}\n")
    commit = git_repo.head.object.hexsha
    source_info.write(f"Commit: {commit}\n")

with open(pj(third_party_path, "PlayniteExtensions", "PlayniteRepo", "SOURCE_INFO.txt"), "w", encoding="utf-8") as source_info:
    git_repo = git.Repo(pj(main_path, "..", "PlayniteExtensions", "PlayniteRepo"), search_parent_directories=True)
    source = git_repo.remotes.origin.url
    source_info.write(f"Source: {source}\n")
    commit = git_repo.head.object.hexsha
    source_info.write(f"Commit: {commit}\n")

with open(pj(third_party_path, "playnite-common-plugin", "SOURCE_INFO.txt"), "w", encoding="utf-8") as source_info:
    git_repo = git.Repo(pj(main_path, "..", "playnite-common-plugin"), search_parent_directories=True)
    source = git_repo.remotes.origin.url
    source_info.write(f"Source: {source}\n")
    commit = git_repo.head.object.hexsha
    source_info.write(f"Commit: {commit}\n")
