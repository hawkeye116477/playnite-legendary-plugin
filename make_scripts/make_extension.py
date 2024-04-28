#!/usr/bin/env python
# pylint: disable=C0103
# pylint: disable=C0301
# pylint: disable=E1129
# pylint: disable=E1136
"""Pack extension"""
import os
import subprocess
import shutil
import datetime
import hashlib
import winreg
import xml.etree.ElementTree as ET
import yaml
import git
import get_extension_version


class MyDumper(yaml.Dumper):
    """https://stackoverflow.com/a/39681672"""

    def increase_indent(self, flow=False, indentless=False):
        return super().increase_indent(flow, False)


pj = os.path.join
pn = os.path.normpath

playnitePath = pn(pj(os.path.expanduser('~'), r"scoop\apps\playnite\current"))
if not os.path.isdir(playnitePath):
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall') as key:
        for i in range(0, winreg.QueryInfoKey(key)[0]):
            with winreg.OpenKey(key, winreg.EnumKey(key, i)) as subkey:
                if winreg.QueryValueEx(subkey, "DisplayName")[0] == "Playnite":
                    playnitePath = pn(winreg.QueryValueEx(subkey, "InstallLocation")[0])
                    break

toolbox = pj(playnitePath, "Toolbox.exe")
scriptPath = os.path.dirname(os.path.realpath(__file__))
mainPath = pn(scriptPath + "/..")
compiledPath = pn(pj(mainPath, "src/bin/Release"))
releasesPath = pj(mainPath, "Releases")

if not os.path.exists(releasesPath):
    os.makedirs(releasesPath)
else:
    shutil.rmtree(releasesPath)

# Remove inactive (not enough translated) languages
active_languages = ["en_US", "it_IT", "pl_PL", "es_ES", "tr_TR", "pt_BR"]
for root, dirs, files in os.walk(pj(compiledPath, "Localization")):
    for file in files:
        if not any(substring in file for substring in active_languages):
            os.remove(pj(root, file))


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
    print(f"Checksum: {checksumExt}")

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

    packagesConfig = ET.parse(pj(mainPath, "src", "packages.config"))
    for child in packagesConfig.getroot():
        if child.get('id') == "PlayniteSDK":
            sdkVersion = child.get('version')
            break

    if newVersion == "true":
        installerManifest["Packages"].insert(0, {
            "Version": version,
            "RequiredApiVersion": sdkVersion,
            "ReleaseDate": datetime.date.today(),
            "PackageUrl": f"https://github.com/hawkeye116477/playnite-legendary-plugin/releases/download/{version}/LegendaryLibrary_{versionUnderline}.pext",
            "Changelog": [line.rstrip().replace("* ", "") for line in changelog]
        })

        with open(pj(mainPath, "installer.yaml"), "w", encoding="utf-8") as file:
            yaml.dump(installerManifest, file,
                      sort_keys=False, Dumper=MyDumper)

        git_repo = git.Repo(mainPath)
        git_repo.create_tag(version)
