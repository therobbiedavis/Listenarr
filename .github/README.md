# Assets Directory

This directory contains branding and documentation assets for the Listenarr project.

## Files

### Logo Files
- **logo-icon.png** - Square icon format
  - Used for: Favicons, app icons, social media
  - Primary brand mark
  - Also available as `icon.png` in public folder
  
- **logo-full.png** - Horizontal logo with text
  - Used for: README headers, documentation, marketing
  - Complete brand identity
  - Also available as `logo.png` in public folder

### Documentation
- **BRANDING.md** - Complete branding guidelines and logo usage
- **copilot-instructions.md** - Development guidelines for GitHub Copilot

## Usage

### In README
```markdown
![Listenarr](.github/logo-full.png)
```

### In Web Application
Logo files are also available in `fe/public/` for use in the Vue.js application:
```html
<img src="/icon.png" alt="Listenarr">
<img src="/logo.png" alt="Listenarr">
```

## Brand Colors

| Color Name | Hex | Usage |
|------------|-----|-------|
| Primary Blue | `#2196F3` | Main brand color |
| Dark Blue | `#1976D2` | Hover states |
| Light Blue | `#E3F2FD` | Highlights |

For complete branding guidelines, see [BRANDING.md](BRANDING.md).
