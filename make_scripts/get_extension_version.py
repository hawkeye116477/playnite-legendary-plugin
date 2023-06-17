#!/usr/bin/env python
# pylint: disable=C0103
# pylint: disable=C0301
"""Get extension version"""
import os

pj = os.path.join
pn = os.path.normpath

scriptPath = os.path.dirname(os.path.realpath(__file__))
mainPath = pn(scriptPath + "/..")


def run():
    """Let's start"""
    with open(pn(pj(mainPath, r"src\Properties\AssemblyInfo.cs")), "r", encoding="utf-8") as assemblyInfo:
        assemblyInfoLines = assemblyInfo.read().splitlines()
        for line in assemblyInfoLines:
            if line.startswith("[") and "AssemblyVersion" in line:
                version = line.split('AssemblyVersion("')[
                    1].replace('")]', '').replace(".*", "")
    return version
