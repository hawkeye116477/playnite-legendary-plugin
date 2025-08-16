import os
import re
import shutil
import tempfile
from lxml import etree as ET
from fluent.syntax import parse as FluentParse
from fluent.syntax import serialize as FluentSerialize
import yaml

convert_csharp = False

pj = os.path.join
pn = os.path.normpath
script_path = os.path.dirname(os.path.realpath(__file__))
main_path = pn(script_path+"/..")
src_path = pj(main_path, "src")

os.chdir(main_path)


xmlns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
xmlns_x = "http://schemas.microsoft.com/winfx/2006/xaml"
xmlns_sys = "clr-namespace:System;assembly=mscorlib"

NSMAP = {None: xmlns,
        "sys": xmlns_sys,
        "x":  xmlns_x}

loc_path = pj(src_path, "Localization")

with open(pj(script_path, "config", "placeholderReplacements.yaml"), 'r') as r_file:
    replacement_strings = yaml.safe_load(r_file)

key_to_prefix_map = {}

for filename in os.listdir(loc_path):
    translations = {}
    path = os.path.join(loc_path, filename)
    if os.path.isdir(path):
        continue
    if ".ftl" in filename:
        continue

    loc = ET.parse(pj(loc_path, filename))
    xml_root = ET.Element("ResourceDictionary", nsmap=NSMAP)
    xml_doc = ET.ElementTree(xml_root)
    for child in loc.getroot():
        key = child.get(ET.QName(xmlns_x, "Key"))
        prefix = "Common"
        filename_without_extension = filename.replace(".xaml", "")
        if "-" in filename:
            prefix = filename_without_extension.split('-')[1].capitalize()
        key_to_prefix_map[key] = prefix
        key_text = child.text
        if not key_text:
            key_text = ""
        if "{0}" in key_text:
            key_text = key_text.replace("{0}", f'{{{replacement_strings[key]['first']}}}')
        if "{1}" in key_text:
            key_text = key_text.replace("{1}", f"{{{replacement_strings[key]['second']}}}")
        key_text = key_text.replace("{AppName}", "{LauncherName}")
        key_text = key_text.replace("{SourceName}", "{UpdatesSourceName}")
        key_text = re.sub(r'\{([a-zA-Z0-9_-]+)\}', lambda m: f'{{ ${m.group(1)[0].lower() + m.group(1)[1:]} }}', key_text)
        new_key = key.replace("LOC", "")
        if not "-" in filename:
            new_key = new_key.replace("Legendary", "Common")
        new_key = new_key.replace("Other", "")
        new_key = re.sub(r'([a-z0-9])([A-Z])|([A-Z])([a-z])', r'\1-\2\3\4', new_key)
        new_key = new_key.lower().lstrip('-')
        
        if new_key not in translations:
            translations[new_key] = {'is_plural': False, 'one': None, 'other': None}
        if key.endswith("Other"):
            translations[new_key]['is_plural'] = True
            translations[new_key]['other'] = key_text
        else:
            translations[new_key]['one'] = key_text

    ftl_string = ""
    for ftl_id, data in translations.items():
        if data['is_plural']:
            one_variant = data['one']
            other_variant = data['other']

            ftl_string += f"""
{ftl_id} =
{{ $count ->
"""
            if one_variant != None:
                ftl_string += f"""
[one] {one_variant}
"""
            ftl_string += f"""
*[other] {other_variant}
}}
"""     
        else:
            value = data['one']
            ftl_string += f"""
{ftl_id} = {value}
"""

    ftl_resource_ast = FluentParse(ftl_string)
    serialized_output = FluentSerialize(ftl_resource_ast)
    filename_without_extension = filename.replace(".xaml", "")
    ftl_dir = filename_without_extension
    ftl_filename = "common"
    if "-" in filename:
        ftl_filename = ftl_dir.split('-')[1]
        ftl_dir = ftl_dir.split('-')[0]
    ftl_dir = ftl_dir.replace("_", "-")
    full_ftl_dir = pj(loc_path, ftl_dir)
    ftl_filename = f"{ftl_filename}.ftl"
    if not os.path.exists(full_ftl_dir):
        os.makedirs(full_ftl_dir)
    if serialized_output != "":
        with open(pj(full_ftl_dir, ftl_filename), 'w', encoding='utf-8') as ftl_file:
            ftl_file.write(serialized_output)
        print(f"File {ftl_dir}\{ftl_filename} was succesfully generated.")

if not convert_csharp:
    exit(0)

for root, _, files in os.walk(src_path):
    for filename in files:
        if filename.endswith(".cs"):
            file_path = pj(root, filename)
            temp_file_path = None
            
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()

            def get_new_key(old_key):
                if old_key is None:
                    return None
                old_key_replaced = old_key.replace(".", "")
                if old_key_replaced in key_to_prefix_map:
                    prefix = key_to_prefix_map[old_key_replaced]
                    
                    new_key = old_key
                    
                    if prefix == "Common":
                        new_key = new_key.replace("LOC.Legendary", "LOC.Common")
                    return new_key
                return old_key
            
            # This is the single, comprehensive regex to handle all cases.
            # The order of the OR (|) conditions is CRITICAL to prevent incorrect matches.
            pattern = re.compile(
                # 1. string.Format(ResourceProvider/playniteAPI.Resources/api.Resources.GetString(), ...)
                r'string\.Format\((?P<string_key_arg>(?:ResourceProvider|playniteAPI\.Resources|api\.Resources)\.GetString\((?P<string_key>LOC\.[^)]+)\)),\s*(?P<string_args>(?:[^()]|\([^()]*\))*)\)'
                r'|'
                 # 2. ResourceProvider/playniteAPI.Resources/api.Resources.GetString().Format(...)
                r'(?:ResourceProvider|playniteAPI\.Resources|api\.Resources)\.GetString\((?P<formatted_key>LOC\.[^)]+)\)\.Format\((?P<formatted_args>(?:[^()]|\([^()]*\))*)\)'
                r'|'
                # 3. Simple ResourceProvider/playniteAPI.Resources/api.Resources.GetString()
                r'(?P<simple_full>(?:ResourceProvider|playniteAPI\.Resources|api\.Resources)\.GetString\((?P<simple_key>LOC\.[^)]+)\))'
                r'|'
                # 4. Only LOC.
                r'(?P<standalone_key>LOC\.[^\s,;)]+)'
            )

            def process_match_and_replace(match):
                if match.group('string_key'):
                    old_key = match.group('string_key')
                    args_list = match.group('string_args')
                    
                    if 'LOC.Legendary3P' in old_key:
                        # Recursively convert only the arguments if the main key is 3P
                        converted_args = re.sub(pattern, process_match_and_replace, args_list)
                        return f'string.Format(ResourceProvider.GetString({old_key}), {converted_args})'
                    
                    new_key = get_new_key(old_key)
                    old_key_nodot = old_key.replace(".", "")

                    if old_key_nodot not in replacement_strings:
                        # Recursively convert arguments
                        converted_args = re.sub(pattern, process_match_and_replace, args_list)
                        return f'LocalizationManager.Instance.GetString({new_key}, {converted_args})'
                    
                    args_yaml = replacement_strings[old_key_nodot]
                    csharp_args = [a.strip() for a in args_list.split(',')]

                    fluent_args_list = []
                    # Recursively convert each argument before adding it to the fluent list
                    if 'first' in args_yaml and len(csharp_args) > 0:
                        converted_arg = re.sub(pattern, process_match_and_replace, csharp_args[0])
                        fluent_args_list.append(f'["{args_yaml["first"]}"] = (FluentString){converted_arg}')
                    if 'second' in args_yaml and len(csharp_args) > 1:
                        converted_arg = re.sub(pattern, process_match_and_replace, csharp_args[1])
                        fluent_args_list.append(f'["{args_yaml["second"]}"] = (FluentString){converted_arg}')
                    
                    fluent_args_string = ', '.join(fluent_args_list)
                    return f'LocalizationManager.Instance.GetString({new_key}, new Dictionary<string, IFluentType> {{ {fluent_args_string} }})'

                elif match.group('formatted_key'):
                    old_key = match.group('formatted_key')
                    arg_content = match.group('formatted_args')
                    
                    if 'LOC.Legendary3P' in old_key:
                        # Recursively convert only the arguments if the main key is 3P
                        converted_args = re.sub(pattern, process_match_and_replace, arg_content)
                        return f'ResourceProvider.GetString({old_key}).Format({converted_args})'
                    
                    new_key = get_new_key(old_key)
                    old_key_nodot = old_key.replace(".", "")
                    
                    if old_key_nodot not in replacement_strings:
                        # Recursively convert arguments
                        converted_args = re.sub(pattern, process_match_and_replace, arg_content)
                        return f'LocalizationManager.Instance.GetString({new_key})'
                    
                    args_yaml = replacement_strings[old_key_nodot]
                    csharp_args = [a.strip() for a in arg_content.split(',')]
                    
                    fluent_args_list = []
                    # Recursively convert each argument before adding it to the fluent list
                    if 'first' in args_yaml and len(csharp_args) > 0:
                        converted_arg = re.sub(pattern, process_match_and_replace, csharp_args[0])
                        fluent_args_list.append(f'["{args_yaml["first"]}"] = (FluentString){converted_arg}')
                    if 'second' in args_yaml and len(csharp_args) > 1:
                        converted_arg = re.sub(pattern, process_match_and_replace, csharp_args[1])
                        fluent_args_list.append(f'["{args_yaml["second"]}"] = (FluentString){converted_arg}')
                    
                    fluent_args_string = ', '.join(fluent_args_list)
                    return f'LocalizationManager.Instance.GetString({new_key}, new Dictionary<string, IFluentType> {{ {fluent_args_string} }})'

                elif match.group('simple_key'):
                    old_key = match.group('simple_key')
                    
                    if 'LOC.Legendary3P' in old_key:
                        return match.group(0)

                    new_key = get_new_key(old_key)
                    return f'LocalizationManager.Instance.GetString({new_key})'

                elif match.group('standalone_key'):
                    old_key = match.group('standalone_key')

                    if 'LOC.Legendary3P' in old_key:
                        return match.group(0)

                    new_key = get_new_key(old_key)
                    return new_key
                
                return match.group(0)

            content = re.sub(pattern, process_match_and_replace, content)

            with tempfile.NamedTemporaryFile(mode='w', delete=False, encoding='utf-8') as tmp_file:
                temp_file_path = tmp_file.name
                tmp_file.write(content)
            shutil.move(temp_file_path, file_path)
            print(f"File {file_path} was succesfully processed.")
