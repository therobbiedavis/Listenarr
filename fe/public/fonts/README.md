Place Figtree font files here for self-hosting the brand font.

Recommended files (preferred order):
- Figtree-VariableFont_wght.woff2  (preferred - modern browsers, best compression)
- Figtree-VariableFont_wght.woff
- Figtree-VariableFont_wght.ttf

Alternate (per-weight files):
- Figtree-Regular.woff2
- Figtree-SemiBold.woff2
- Figtree-Bold.woff2

Where to get the fonts
- Google Fonts (download family): https://fonts.google.com/specimen/Figtree
- Google Fonts Github or font source if you prefer a packaged download

How to add files
1. Download the Figtree family (variable font or individual woff2 files).
2. Place the chosen files into this directory exactly as named above (or update the paths in `fe/src/App.vue` if you use different filenames).
3. Commit the files if you want them versioned with the repo (verify license — Figtree is open source via Google Fonts).

Notes
- Using the variable `woff2` is recommended for best performance and broad support.
- If you don't add files here, the app will fall back to loading Figtree from Google Fonts (see `fe/index.html`).
- If you need me to add the actual font files, upload them here or provide a download link and I can include them in the repo (make sure the license allows bundling in this repository).