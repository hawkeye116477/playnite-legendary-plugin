#!/usr/bin/env python
# pylint: disable=C0103
# pylint: disable=C0301
"""Do this after succesful build with Visual Studio"""
import os
import sys
import get_extension_version

pj = os.path.join
pn = os.path.normpath

compiledPath = pn(sys.argv[1])

version = get_extension_version.run()

with open(pj(compiledPath, "extension.yaml"), 'r', encoding='utf-8') as extManifest:
    data = extManifest.read()
    data = data.replace("_version_", version)
with open(pj(compiledPath, "extension.yaml"), 'w', encoding='utf-8') as extManifest:
    extManifest.write(data)
