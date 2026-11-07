# World Tour map and flags

Regenerating the World Tour artwork (only needed when changing countries or the projection):

1. Natural Earth 1:110m GeoJSON (public domain) from `nvkelso/natural-earth-vector/geojson/`:
   `ne_110m_land.geojson`, `ne_110m_admin_0_countries.geojson` into a folder, then
   `python3 Tools/worldmap/build_map.py <folder> Assets/_Project/ThirdParty/NaturalEarth/world_map.json`
   The projection constants must match `Runtime/WorldTour/WorldMapProjection.cs` (Miller, lat -57..80).
2. Flags (MIT, lipis/flag-icons): `flags/4x3/<iso>.svg` → `qlmanage -t -s 640 -o <out> *.svg`, crop the centred
   640x480 band, save as `Assets/_Project/ThirdParty/Flags_flag-icons/Flag_<iso>.png`.
3. Run `Tools/bootstrap.sh` (rebuilds country assets, previews and the WorldTour scene).
