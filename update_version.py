import os
import xml.etree.ElementTree as ET
path = 'listenarr.api/Listenarr.Api.csproj'
new = os.environ['NEW_VERSION']
tree = ET.parse(path)
root = tree.getroot()
# Update Version
found_version = False
for elem in root.findall('.//Version'):
    elem.text = new
    found_version = True
    break
if not found_version:
    pg = root.find('PropertyGroup')
    if pg is None:
        pg = ET.SubElement(root, 'PropertyGroup')
    ET.SubElement(pg, 'Version').text = new
# Update AssemblyVersion
found_assembly = False
for elem in root.findall('.//AssemblyVersion'):
    elem.text = new
    found_assembly = True
    break
if not found_assembly:
    pg = root.find('PropertyGroup')
    if pg is None:
        pg = ET.SubElement(root, 'PropertyGroup')
    ET.SubElement(pg, 'AssemblyVersion').text = new
tree.write(path, encoding='utf-8', xml_declaration=True)
print('Wrote new version and assembly version to csproj')
