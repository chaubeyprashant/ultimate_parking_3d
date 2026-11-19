Google Play Console — Store listing graphics

1. App Icon:
   Upload icon-512.png as the app icon (Store settings → Main store listing).
   Requirements (Google Play):
     • 512 × 512 pixels
     • 32-bit PNG with alpha
     • Max file size 1 MB
     • Square full-bleed (Play applies masking); do not pre-round corners

Regenerate from project root:
  python3 Tools/generate_play_store_icon.py

Play Store phone screenshots (7× 1920×1080 landscape PNG):
  Unity menu: Tools → Ultimate Car Parking → Generate Play Store Screenshots (7)
  or batch:
  Tools/unity via -executeMethod UltimateCarParking.EditorTools.StoreScreenshotBuilder.GenerateAll

Upload folder: StoreAssets/GooglePlay/screenshots/phone-screenshot-01.png … 07.png
Raw captures (no text): StoreAssets/GooglePlay/screenshots/raw/

Same art is copied to Assets/_Project/Art/Branding/AppIcon.png for the Android build.
   After regenerating, run Tools → Ultimate Car Parking → Rebuild Bootstrap Splash in Unity (or full bootstrap).

2. Feature Graphic:
   Upload feature-graphic-1024x500.png (or .jpg) under "Feature graphic" in Main store listing.
   Requirements (Google Play):
     • 1024 × 500 pixels
     • JPEG or 24-bit PNG (no alpha)
     • Max file size 15 MB
   Included high-reach ASO keyword badges:
     • CAR PARKING 3D
     • DRIVING SIMULATOR
     • DRIVING SCHOOL
     • PRECISION PARKING
     • CAR GAMES
