import os
import xml.etree.ElementTree as ET
path = 'listenarr.api/Listenarr.Api.csproj'
new = os.environ['NEW_VERSION']
tree = ET.parse(path)
root = tree.getroot()
found = False
for elem in root.findall('.//Version'):
    elem.text = new
    found = True
    break
if not found:
    pg = root.find('PropertyGroup')
    if pg is None:
        pg = ET.SubElement(root, 'PropertyGroup')
    ET.SubElement(pg, 'Version').text = new
tree.write(path, encoding='utf-8', xml_declaration=True)
print('Wrote new version to csproj')
