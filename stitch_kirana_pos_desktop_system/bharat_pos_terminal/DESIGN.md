---
name: Bharat POS Terminal
colors:
  surface: '#f8f9fa'
  surface-dim: '#d9dadb'
  surface-bright: '#f8f9fa'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f3f4f5'
  surface-container: '#edeeef'
  surface-container-high: '#e7e8e9'
  surface-container-highest: '#e1e3e4'
  on-surface: '#191c1d'
  on-surface-variant: '#43474e'
  inverse-surface: '#2e3132'
  inverse-on-surface: '#f0f1f2'
  outline: '#74777f'
  outline-variant: '#c4c6cf'
  surface-tint: '#476083'
  primary: '#000613'
  on-primary: '#ffffff'
  primary-container: '#001f3f'
  on-primary-container: '#6f88ad'
  inverse-primary: '#afc8f0'
  secondary: '#575f67'
  on-secondary: '#ffffff'
  secondary-container: '#d8e1ea'
  on-secondary-container: '#5b646b'
  tertiary: '#110200'
  on-tertiary: '#ffffff'
  tertiary-container: '#391303'
  on-tertiary-container: '#b5785f'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#d4e3ff'
  primary-fixed-dim: '#afc8f0'
  on-primary-fixed: '#001c3a'
  on-primary-fixed-variant: '#2f486a'
  secondary-fixed: '#dbe4ed'
  secondary-fixed-dim: '#bfc8d0'
  on-secondary-fixed: '#141d23'
  on-secondary-fixed-variant: '#3f484f'
  tertiary-fixed: '#ffdbce'
  tertiary-fixed-dim: '#fdb69a'
  on-tertiary-fixed: '#351002'
  on-tertiary-fixed-variant: '#6b3a25'
  background: '#f8f9fa'
  on-background: '#191c1d'
  surface-variant: '#e1e3e4'
typography:
  display-sm:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '700'
    lineHeight: 32px
  headline-sm:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  label-sm:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
  data-md:
    fontFamily: JetBrains Mono
    fontSize: 14px
    fontWeight: '500'
    lineHeight: 20px
  data-sm:
    fontFamily: JetBrains Mono
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  unit: 4px
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  row-height: 32px
  gutter: 12px
---

## Brand & Style
The design system is engineered for high-velocity retail environments. It prioritizes information density, legibility, and rapid keyboard-driven workflows. The aesthetic is a **Corporate Modern** style with a focus on a "trading terminal" utility—eschewing all decorative elements like gradients, shadows, or illustrations in favor of raw data clarity.

**Target Audience:** Indian grocery store operators, cashiers, and inventory managers.
**Emotional Response:** Efficiency, precision, reliability, and institutional trust.

## Colors
The palette is rooted in a deep navy blue primary to convey authority. The background architecture utilizes near-white neutrals to reduce eye strain during long shifts. 

- **Surface:** `#FFFFFF` for primary work areas; `#F8F9FA` for sidebar and background contrast.
- **Borders:** `#DEE2E6` for standard dividers; `#001F3F` (2px) for active focus states.
- **Semantic:** All colors are calibrated for WCAG AA compliance against white backgrounds.

## Typography
This design system employs a dual-font strategy. **Inter** handles all interface labels and UI navigation for maximum readability. **JetBrains Mono** is used exclusively for numeric values, prices, weights, and quantities to ensure character alignment in dense data tables, facilitating quick scanning of vertical columns.

- **Currency Format:** Always prefixed as `Rs. 0.00`.
- **Weight Format:** Fixed decimal at `0.000kg`.
- **Case:** Use Sentence case for labels; ALL CAPS is reserved for high-priority function key indicators.

## Layout & Spacing
A strict **4px grid** governs all spatial relationships. The layout is a fixed-width dashboard optimized for 1920x1080 resolution, common in retail hardware. 

- **Grid:** 12-column system with 12px gutters.
- **Density:** High. Vertical padding in lists and tables is minimized to fit more items per screen.
- **Responsiveness:** On smaller desktop viewports, sidebars collapse into icons, but data tables remain horizontal-scroll-only to preserve column alignment.

## Elevation & Depth
This system uses **Low-contrast outlines** instead of shadows. Depth is communicated through color-blocking and stroke weight rather than simulated light.

- **Level 0:** Base background (`#F8F9FA`).
- **Level 1:** Content cards and data containers (`#FFFFFF` with 1px border `#DEE2E6`).
- **Focus State:** 2px solid `#001F3F` outline with 2px offset for all interactive elements to ensure clear keyboard navigation.

## Shapes
In keeping with the "serious business tool" aesthetic, shapes are geometric and sharp. 
- **Buttons/Inputs:** 4px radius (Soft).
- **Function Key Chips:** 2px radius for a more technical, hardware-inspired look.
- **Layout Containers:** Square edges (0px) to maximize screen real estate.

## Components

### Data Tables
- **Row Height:** Strictly 32px.
- **Styling:** Zebra-striping using `#F8F9FA` for even rows.
- **Sticky Headers:** Navy background (`#001F3F`) with White text.
- **Alignment:** Text columns are left-aligned; all numeric columns (Price, Qty, Total) are right-aligned using Monospace fonts.

### Buttons & Chips
- **Primary:** Solid Navy (`#001F3F`) with White text.
- **Secondary:** Outline 1px Navy with Navy text.
- **Destructive:** Solid Red (`#DC3545`).
- **F-Key Chips:** Small gray boxes (e.g., `[F2]`) placed immediately to the left of the button text or action label.

### Forms
- **Structure:** Vertical stack. Label (Inter, Bold, 12px) sits directly above the input field.
- **Inputs:** 32px height, 1px border. 
- **Validation:** Errors appear inline below the input in 12px Red text.

### Notifications
- **Toasts/Banners:** Square-edged banners that slide from the top-right. No icons; use bold color-coded left borders (4px) to indicate status (Success/Warning/Danger).

### Empty States
- No illustrations. Use a subtle `#DEE2E6` dashed border container with centered "No items found" text and a single Primary action button.