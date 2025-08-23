import os
import shutil
import tempfile
from zipfile import ZipFile
import requests
from crowdin_api import CrowdinClient

pj = os.path.join
pn = os.path.normpath
script_path = os.path.dirname(os.path.realpath(__file__))
main_path = pn(script_path+"/..")
src_path = pj(main_path, "src")

def request(url):
    resp = requests.get(url, timeout = 30)
    if resp.status_code == 200:
        return resp.content
    print("ERROR: " + str(resp.status_code) + " " + resp.reason)
    return None

class FirstCrowdinClient(CrowdinClient):
    TOKEN = os.getenv("crowdin_token")
    PROJECT_ID = os.getenv("crowdin_project_id")

client = FirstCrowdinClient()
build = client.translations.build_project_translation(dict({"skipUntranslatedStrings": True}))["data"]
status = ''
while status != 'finished':
    info = client.translations.check_project_build_status(build['id'])
    status = info['data']['status']
    if status != 'finished':
        print(f"Build progress: {info['data']['progress']}")
download_info = client.translations.download_project_translations(build['id'])
url = download_info['data']['url']

os.chdir(main_path)

with tempfile.TemporaryDirectory() as tmpdirname:
    with open(pj(tmpdirname, 'temp.zip'), 'wb') as file:
        file.write(request(url))

    with ZipFile(pj(tmpdirname, "temp.zip"), 'r') as zObject:
        zObject.extractall(pj(tmpdirname))

    os.remove(pj(tmpdirname, "temp.zip"))
    xmlns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns_x = "http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns_sys = "clr-namespace:System;assembly=mscorlib"

    NSMAP = {None: xmlns,
            "sys": xmlns_sys,
            "x":  xmlns_x}
    # Copy localizations
    shared_loc_path = pj(tmpdirname, "src", "Localization")
    for root, dirs, files in os.walk(shared_loc_path):
        for filename in files:
            full_file_path = pj(root, filename)
            if os.path.getsize(full_file_path) > 0:
                relative_path = os.path.relpath(root, shared_loc_path)
                destination_path = ""
                if "legendary" in filename:
                    destination_path = pj(src_path, "Localization", relative_path)
                elif "common" in filename:
                    destination_path = pj(main_path, "third_party", "CommonLocalization", relative_path)
                if destination_path != "":
                    if not os.path.exists(destination_path):
                        os.makedirs(destination_path)
                    shutil.copy(full_file_path, destination_path)
