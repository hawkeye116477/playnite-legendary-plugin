#!/usr/bin/env python
# pylint: disable=C0103
"""Update Localization Keys (LOC class)"""
import os
import xml.etree.ElementTree as ET
from fluent.syntax import parse as FluentParse
from fluent.syntax.ast import (
    Message, Term, Pattern, TextElement, Placeable,
    VariableReference, SelectExpression
)
from pathlib import Path

def to_camel_case(s):
    parts = s.split('-')
    return ''.join(part.capitalize() for part in parts)

def reconstruct_pattern_string(pattern):
    """
    Reconstructs a string from a Pattern AST node, handling text and placeables.
    """
    if not isinstance(pattern, Pattern):
        return ""
    
    reconstructed_value = []
    for element in pattern.elements:
        if isinstance(element, TextElement):
            reconstructed_value.append(element.value)
        elif isinstance(element, Placeable):
            expr = element.expression
            if isinstance(expr, VariableReference):
                reconstructed_value.append(f'{{${expr.id.name}}}')
            elif isinstance(expr, SelectExpression):
                selector_name = expr.selector.id.name
                variants_list = [
                    f'<br/>\n\t\t/// {v.key.name}: {reconstruct_pattern_string(v.value)}'
                    for v in expr.variants
                ]
                variants_str = "".join(variants_list)
                reconstructed_value.append(f'{{${selector_name} ->}}{variants_str}')
            else:
                print(f"Warning: Found unhandled expression type: {type(expr).__name__}")
                reconstructed_value.append(f'{{UNHANDLED_EXPRESSION}}')
    
    return "".join(reconstructed_value)

pj = os.path.join
pn = os.path.normpath

script_path = os.path.dirname(os.path.realpath(__file__))
main_path = pn(script_path + "/..")

third_party_loc_file = pn(
    pj(main_path, "third_party", "Localization", "en-US", "third-party.ftl"))
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
common_loc_path = pj(main_path, "third_party", "CommonLocalization", "en-US")
loc_path = pn(pj(main_path, "src", "Localization", "en-US"))
ftl_files = list(Path(loc_path).rglob('*.ftl')) + list(Path(common_loc_path).rglob('*.ftl'))
ftl_files.append(third_party_loc_file)

for file_path in ftl_files:
    with open(file_path, 'r', encoding='utf-8') as f:
        ftl_resource = FluentParse(f.read())

    for entry in ftl_resource.body:
        if isinstance(entry, (Message, Term)) and entry.value:
            key = entry.id.name
            string_value = reconstruct_pattern_string(entry.value)
            string_value = string_value.replace("< ", '&lt; ').replace('> ', '&gt; ')
            loc_keys_file_content += f'''\
        /// <summary>
        /// {string_value}
        /// </summary>
        public const string {to_camel_case(key)} = "{key}";
\
'''

loc_keys_file_content += '''\
    }
}
\
'''

with open(loc_keys_file, 'w', encoding='utf-8') as loc:
    loc.write(loc_keys_file_content)
