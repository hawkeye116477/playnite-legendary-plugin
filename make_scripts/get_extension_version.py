#!/usr/bin/env python
# pylint: disable=C0103
# pylint: disable=C0301
"""Get extension version"""
import os
import xml.etree.ElementTree as ET
import clr
from System.Diagnostics import FileVersionInfo as DotNetVersionInfo

pj = os.path.join
pn = os.path.normpath

script_path = os.path.dirname(os.path.realpath(__file__))
main_path = pn(script_path + "/..")
src_path = pj(main_path, "src")
compiled_path = pj(src_path, "bin", "Release", "net10.0-windows", "LegendaryLibrary.dll")

def run():
    """Let's start"""
    v = DotNetVersionInfo.GetVersionInfo(compiled_path).FileVersion.replace("-", ".")
    return v
