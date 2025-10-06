# Listenarr Branding Assets

This document describes the Listenarr brand identity and logo usage guidelines.

## Logo Files

### Icon Files (Multiple Sizes)

#### Primary Brand Icon
- **File**: `icon.png` / `logo-icon.png`
- **Format**: PNG (with transparency)
- **Usage**: Primary brand mark, universal icon
- **Locations**: 
  - `fe/public/icon.png` (web app)
  - `.github/logo-icon.png` (documentation)

#### Favicon Set
- **favicon.ico** - 16x16, 32x32 multi-resolution ICO file
- **favicon-16x16.png** - 16×16 PNG for browsers
- **favicon-32x32.png** - 32×32 PNG for browsers

#### Apple/iOS Icons
- **apple-touch-icon.png** - 180×180 PNG for iOS home screen

#### Android/Chrome Icons
- **android-chrome-192x192.png** - 192×192 PNG for Android
- **android-chrome-512x512.png** - 512×512 PNG for high-res displays

### Full Logo (Horizontal Format)
- **File**: `logo.png` / `logo-full.png`
- **Format**: PNG (with transparency)
- **Usage**: Headers, README, documentation, marketing materials, splash screens
- **Locations**:
  - `fe/public/logo.png` (web app)
  - `.github/logo-full.png` (documentation)

### Web App Manifest
- **site.webmanifest** - PWA configuration with icon references and theme colors

## Brand Colors

### Primary Blue
- **Hex**: `#2196F3`
- **RGB**: `rgb(33, 150, 243)`
- **Usage**: Primary brand color, logo, buttons, accents

### Dark Blue
- **Hex**: `#1976D2`
- **RGB**: `rgb(25, 118, 210)`
- **Usage**: Hover states, shadows, depth

### Light Blue
- **Hex**: `#E3F2FD`
- **RGB**: `rgb(227, 242, 253)`
- **Usage**: Highlights, book pages in logo, backgrounds

### Dark Background
- **Hex**: `#1a1a1a`
- **RGB**: `rgb(26, 26, 26)`
- **Usage**: Main background

### Medium Background
- **Hex**: `#2a2a2a`
- **RGB**: `rgb(42, 42, 42)`
- **Usage**: Cards, sidebar, header

## Logo Design Elements

The Listenarr logo combines three key elements that represent the core functionality:

1. **Headphones** - Represents listening and audio content
2. **Book** - Represents audiobooks and literature
3. **Circular Frame** - Represents completeness and the continuous nature of the service

### Design Principles
- Clean and modern design
- Scalable vector graphics (SVG)
- Works well on light and dark backgrounds
- Maintains recognizability at small sizes

## Usage Guidelines

### DO ✅
- Use the official SVG files provided
- Maintain aspect ratios when scaling
- Use on solid backgrounds for best visibility
- Ensure adequate spacing around the logo

### DON'T ❌
- Don't distort or stretch the logo
- Don't change the colors without approval
- Don't add effects (shadows, gradients) to the logo
- Don't use low-resolution versions

## File Formats

All logos are provided in PNG format for:
- High-quality raster graphics
- Transparency support (alpha channel)
- Universal compatibility across browsers and platforms
- Optimal for web and print use
- Multiple sizes for different use cases (16px to 512px)

Note: SVG versions are also available in the public folder for scalability if needed.

### Icon Sizes Available
- **16×16** - Browser favicon
- **32×32** - Browser favicon (high DPI)
- **180×180** - Apple touch icon
- **192×192** - Android Chrome (standard)
- **512×512** - Android Chrome (high resolution)
- **Variable** - icon.png and logo.png for general use

## Integration

### Web Application
```html
<!-- Primary icon in components -->
<img src="/icon.png" alt="Listenarr" class="brand-logo" />

<!-- Full logo for headers/splash screens -->
<img src="/logo.png" alt="Listenarr" />
```

### Favicons (in HTML head)
```html
<!-- Multiple sizes for best compatibility -->
<link rel="icon" type="image/x-icon" href="/favicon.ico">
<link rel="icon" type="image/png" sizes="16x16" href="/favicon-16x16.png">
<link rel="icon" type="image/png" sizes="32x32" href="/favicon-32x32.png">
<link rel="apple-touch-icon" sizes="180x180" href="/apple-touch-icon.png">
<link rel="icon" type="image/png" sizes="192x192" href="/android-chrome-192x192.png">
<link rel="icon" type="image/png" sizes="512x512" href="/android-chrome-512x512.png">
<link rel="manifest" href="/site.webmanifest">
<meta name="theme-color" content="#2196F3">
```

### README/Documentation
```markdown
![Listenarr Logo](.github/logo-full.png)
```

## Contact

For questions about branding or logo usage, please open an issue in the GitHub repository.
