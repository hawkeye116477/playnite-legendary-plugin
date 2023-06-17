#!/usr/bin/env python
# pylint: disable=C0103
"""Update Localization Keys (LOC class)"""
import os
import xml.etree.ElementTree as ET

pj = os.path.join
pn = os.path.normpath

scriptPath = os.path.dirname(os.path.realpath(__file__))
mainPath = pn(scriptPath + "/..")

epicLocFile = pn(
    pj(mainPath, "..", r"PlayniteExtensions\source\Libraries\EpicLibrary\Localization\en_US.xaml"))
legendaryLocFile = pn(pj(mainPath, r"src\Localization\en_US.xaml"))
locKeysFile = pj(mainPath, "src", "LocalizationKeys.cs")

locKeysFileContent = '''\
///
/// DO NOT MODIFY! Automatically generated via update_localization_keys.py script.
///
namespace System
{
    public static class LOC
    {
\
'''

x_ns = "{http://schemas.microsoft.com/winfx/2006/xaml}"
epicLoc = ET.parse(epicLocFile)
for child in epicLoc.getroot():
    key = child.get(x_ns + 'Key')
    locKeysFileContent += f'''\
        /// <summary>
        /// {child.text}
        /// </summary>
        public const string {key.replace("LOC", "")} = "{key}";
\
'''
legendaryLoc = ET.parse(legendaryLocFile)
for child in legendaryLoc.getroot():
    key = child.get(x_ns + 'Key')
    locKeysFileContent += f'''\
        /// <summary>
        /// {child.text}
        /// </summary>
        public const string {key.replace("LOC", "")} = "{key}";
\
'''

locKeysFileContent += '''\
    }
}
\
'''

with open(locKeysFile, 'w', encoding='utf-8') as loc:
    loc.write(locKeysFileContent)
