#!/usr/bin/env python3
# pylint: disable=C0103
# pylint: disable=C0301
"""Pack extension"""
import os
import subprocess
import shutil
import datetime
import hashlib
import yaml
import get_extension_version


class MyDumper(yaml.Dumper):
    """https://stackoverflow.com/a/39681672"""

    def increase_indent(self, flow=False, indentless=False):
        return super().increase_indent(flow, False)


pj = os.path.join
pn = os.path.normpath

playnitePath = pn(r"C:\Program Files\Playnite")
toolbox = pj(playnitePath, "Toolbox.exe")
scriptPath = os.path.dirname(os.path.realpath(__file__))
mainPath = pn(scriptPath + "/..")
compiledPath = pn(pj(mainPath, "src/bin/Release"))
releasesPath = pj(mainPath, "Releases")

if not os.path.exists(releasesPath):
    os.makedirs(releasesPath)
else:
    shutil.rmtree(releasesPath)

subprocess.run([pj(playnitePath, "Toolbox.exe"), "pack",
               compiledPath, releasesPath], check=True)

version = get_extension_version.run()
versionUnderline = version.replace(".", "_")
extFile = pj(mainPath, "Releases", "LegendaryLibrary_" +
             versionUnderline + ".pext")
checksumFilePath = pj(mainPath, "Releases",
                      "LegendaryLibrary_" + versionUnderline + ".pext.sha256")

if os.path.exists(extFile):
    with open(extFile, 'rb') as fileToCheck:
        data = fileToCheck.read()
    checksumExt = hashlib.sha256(data).hexdigest()

    with open(checksumFilePath, "a", encoding="utf-8") as checksumFile:
        checksumFile.write(checksumExt+"  "+os.path.basename(extFile))

    with open(pj(mainPath, "changelog.txt"), "r", encoding="utf-8") as cf:
        changelog = cf.readlines()

    with open(pj(mainPath, "installer.yaml"), "r", encoding="utf-8") as file:
        installerManifest = yaml.safe_load(file)

    newVersion = "true"
    if installerManifest["Packages"] is not None:
        for element in installerManifest["Packages"]:
            if element["Version"] == version:
                newVersion = "false"
    else:
        installerManifest["Packages"] = []

    if newVersion == "true":
        installerManifest["Packages"].insert(0, {
            "Version": version,
            "RequiredApiVersion": '6.4.0',
            "ReleaseDate": datetime.date.today(),
            "PackageUrl": f"https://github.com/hawkeye116477/playnite-legendary-plugin/releases/download/{version}/LegendaryLibrary_{versionUnderline}.pext",
            "Changelog": [line.rstrip().replace("* ", "") for line in changelog]
        })

        with open(pj(mainPath, "installer.yaml"), "w", encoding="utf-8") as file:
            yaml.dump(installerManifest, file,
                      sort_keys=False, Dumper=MyDumper)
