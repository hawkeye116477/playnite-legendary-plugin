#!/usr/bin/env python
# pylint: disable=C0103
"""Update third-party localization"""
import os
import xml.etree.ElementTree as ET
import shutil
import git

pj = os.path.join
pn = os.path.normpath

script_path = os.path.dirname(os.path.realpath(__file__))
main_path = pn(pj(script_path, ".."))
third_party_path = pj(main_path, "third_party")
localization_path = pj(third_party_path, "Localization")
src_path = pj(main_path, "src")

EPIC_LOC_KEYS = ["LOCEpicSettingsImportInstalledLabel", "LOCEpicSettingsConnectAccount",
                 "LOCEpicSettingsImportUninstalledLabel", "LOCEpicAuthenticateLabel",
                 "LOCEpicLoggedIn", "LOCEpicNotLoggedIn", "LOCEpicLoginChecking",  
                 "LOCEpicTroubleShootingIssues", "LOCEpicStartUsingClient",
                 "LOCEpicNotLoggedInError"]

PLAYNITE_LOC_KEYS = ["LOCUninstallGame", "LOCGameStartError", "LOCLoginRequired",
                     "LOCDoNothing", "LOCMenuShutdownSystem",
                     "LOCMenuRestartSystem", "LOCMenuHibernateSystem",
                     "LOCMenuSuspendSystem", "LOCOptionOnceADay", "LOCOptionOnceAWeek",
                     "LOCSettingsPlaytimeImportModeNever", "LOCSettingsClearCacheTitle",
                     "LOCOKLabel", "LOCDontShowAgainTitle", "LOCGameInstallError",
                     "LOCLibraryImportError", "LOCProgressMetadata", "LOCMetadataDownloadError",
                     "LOCCommonLinksStorePage", "LOCInstallGame", "LOCFilterActiveLabel",
                     "LOCSettingsGeneralLabel", "LOCSelectDirectoryTooltip", "LOCSelectFileTooltip", "LOCCancelLabel",
                     "LOCSettingsAdvancedLabel", "LOCGameInstallDirTitle", "LOCLoadingLabel",
                     "LOCSaveLabel", "LOCFilters", "LOCGameNameTitle", "LOCInstallSizeLabel",
                     "LOCAddedLabel", "LOCOpen", "LOCCheckForUpdates", "LOCUpdaterWindowTitle", "LOCUpdateCheckFailMessage", "LOCUpdaterInstallUpdate", "LOCExecutableTitle"]

if os.path.exists(localization_path):
    shutil.rmtree(localization_path)
os.makedirs(localization_path)

for filename in os.listdir(pj(main_path, "..", "PlayniteExtensions", "PlayniteRepo", "source", "Playnite", "Localization")):
    git_repo = git.Repo(
        pj(main_path, "..", "PlayniteExtensions"), search_parent_directories=True)
    commit = git_repo.head.object.hexsha
    source = git_repo.remotes.origin.url.replace(".git", f"/tree/{commit}")
    Playnite_git_repo = git.Repo(pj(main_path, "..", "PlayniteExtensions", "PlayniteRepo"), search_parent_directories=True)
    commit2 = Playnite_git_repo.head.object.hexsha
    source2 = Playnite_git_repo.remotes.origin.url.replace(".git", f"/tree/{commit2}")

    i18n_content = ['<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:sys="clr-namespace:System;assembly=mscorlib">']
    new_filename = filename
    if filename not in ["LocSource.xaml", "LocalizationKeys.cs", "locstatus.json"]:
        if filename == "en_US.xaml":
            new_filename = "LocSource.xaml"
        playnite_loc = ET.parse(pj(main_path, "..", "PlayniteExtensions",
                                "PlayniteRepo", "source", "Playnite", "Localization", new_filename))
        for child in playnite_loc.getroot():
            key = child.get(
                "{http://schemas.microsoft.com/winfx/2006/xaml}" + 'Key')
            if key in PLAYNITE_LOC_KEYS:
                key_text = child.text
                if not key_text:
                    key_text = ""
                i18n_content.append(
                    f'<sys:String x:Key="{key.replace("LOC", "LOCLegendary3P_Playnite")}">{key_text}</sys:String>')

    if filename not in ["LocSource.xaml", "LocalizationKeys.cs", "locstatus.json"]:
        epic_loc = ET.parse(pj(main_path, "..", "PlayniteExtensions",
                            "source", "Libraries", "EpicLibrary", "Localization", filename))
        for child in epic_loc.getroot():
            key = child.get(
                "{http://schemas.microsoft.com/winfx/2006/xaml}" + 'Key')
            if key in EPIC_LOC_KEYS:
                key_text = child.text
                if not key_text:
                    key_text = ""
                i18n_content.append(
                    f'<sys:String x:Key="{key.replace("LOCEpic", "LOCLegendary3P_Epic")}">{key_text}</sys:String>')
        i18n_content.append("</ResourceDictionary>")

        tree = ET.fromstring("\n".join(i18n_content))
        ET.register_namespace(
            '', "http://schemas.microsoft.com/winfx/2006/xaml/presentation")
        ET.register_namespace(
            'x', "http://schemas.microsoft.com/winfx/2006/xaml")
        ET.register_namespace('sys', "clr-namespace:System;assembly=mscorlib")
        ET.indent(tree, level=0)
        with open(pj(localization_path, filename), "w", encoding="utf-8") as i18n_file:
            i18n_file.write("<?xml version='1.0' encoding='utf-8'?>\n")
            i18n_file.write(
                f'<!--\n  Automatically generated via update_3p_localization.py script using files from {source} and {source2}.\n  DO NOT MODIFY, CUZ IT MIGHT BE OVERWRITTEN DURING NEXT RUN!\n-->\n')
            i18n_file.write(ET.tostring(tree, encoding="utf-8").decode())
