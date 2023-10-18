#!/usr/bin/env python
# pylint: disable=C0103
"""Update Localization Keys (LOC class)"""
import os
import xml.etree.ElementTree as ET

pj = os.path.join
pn = os.path.normpath

script_path = os.path.dirname(os.path.realpath(__file__))
main_path = pn(script_path + "/..")

third_party_loc_file = pn(
    pj(main_path, "third_party", r"Localization\en_US.xaml"))
legendary_loc_file = pn(pj(main_path, r"src\Localization\en_US.xaml"))
loc_keys_file = pj(main_path, "src", "LocalizationKeys.cs")

loc_keys_file_content = '''\
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
third_party_loc = ET.parse(third_party_loc_file)
for child in third_party_loc.getroot():
    key = child.get(x_ns + 'Key')
    loc_keys_file_content += f'''\
        /// <summary>
        /// {child.text}
        /// </summary>
        public const string {key.replace("LOC", "")} = "{key}";
\
'''
legendary_loc = ET.parse(legendary_loc_file)
for child in legendary_loc.getroot():
    key = child.get(x_ns + 'Key')
    loc_keys_file_content += f'''\
        /// <summary>
        /// {child.text}
        /// </summary>
        public const string {key.replace("LOC", "")} = "{key}";
\
'''

loc_keys_file_content += '''\
    }
}
\
'''

with open(loc_keys_file, 'w', encoding='utf-8') as loc:
    loc.write(loc_keys_file_content)
