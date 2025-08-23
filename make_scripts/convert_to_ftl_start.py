import os
import importlib.util

pj = os.path.join
pn = os.path.normpath

script_path = os.path.dirname(os.path.realpath(__file__))
main_path = pn(script_path+"/..")
FTL_script_path = pn(pj(main_path, '..', "playnite-common-plugin", "make_scripts", "convert_to_ftl.py"))

spec = importlib.util.spec_from_file_location("convert_to_ftl", FTL_script_path)
convertToFtl_script = importlib.util.module_from_spec(spec)
spec.loader.exec_module(convertToFtl_script)
src_path = pj(main_path, "src")
config_path = pj(script_path, "config")
loc_path = pj(main_path, "Localization")
#convertToFtl(plugin_prefix, src_path, config_path, loc_path, convert_csharp = True, convert_3p = True, comment = ""):
convertToFtl_script.convertToFtl("Legendary", src_path, config_path, loc_path, True, True)
