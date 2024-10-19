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
shared_loc_file = pn(pj(main_path, r"src\Localization\en_US.xaml"))
legendary_loc_file = pn(pj(main_path, r"src\Localization\en_US-legendary.xaml"))
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
loc_files = [third_party_loc_file, legendary_loc_file, shared_loc_file]
for loc_file in loc_files:
    loc_parse = ET.parse(loc_file)
    for child in loc_parse.getroot():
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
