#!/usr/bin/env python
# pylint: disable=C0103
# pylint: disable=C0301
"""Get extension version"""
import os
import xml.etree.ElementTree as ET

pj = os.path.join
pn = os.path.normpath

script_path = os.path.dirname(os.path.realpath(__file__))
main_path = pn(script_path + "/..")
src_path = pj(main_path, "src")

csproj_path = pj(src_path, "LegendaryLibrary.csproj")
csproj = ET.parse(csproj_path)
xml_ns = "{http://schemas.microsoft.com/developer/msbuild/2003}"

def run():
    """Let's start"""
    root = csproj.getroot()
    v = ""
    for pg in root.findall(".//PropertyGroup"):
        v = pg.find("Version")
        if v is not None and v.text:
            return v.text
    return v
