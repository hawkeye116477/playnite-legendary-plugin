import os
import shutil
import tempfile
from zipfile import ZipFile
import requests
from crowdin_api import CrowdinClient
from lxml import etree as ET

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
    # Copy localizations and rename strings
    shared_loc_path = pj(tmpdirname, "src", "Localization")
    for filename in os.listdir(shared_loc_path):
        path = os.path.join(shared_loc_path, filename)
        if os.path.isdir(path):
            continue
        if "legendary" in filename:
            shutil.copy(path, pj(src_path, "Localization", os.path.dirname(path)))
        elif "common" in filename:
            shutil.copy(path, pj(src_path, "Localization", os.path.dirname(path)))
        else:
            print("")
