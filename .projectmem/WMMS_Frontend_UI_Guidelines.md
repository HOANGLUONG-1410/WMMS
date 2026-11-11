# WMMS – Frontend UI Design Guidelines

> Mandatory rules for developers and AI agents. Each rule has a stable ID (e.g. `COLOR-02`) so it can be cited when reporting conflicts.

## 1. Overview

This document establishes a mandatory set of guidelines and requirements that all developers and AI agents **must follow** whenever implementing, modifying, or maintaining any part of the system's frontend (only for front end).

These guidelines take precedence over individual implementation instructions. If a developer's request or instruction conflicts with any requirement defined in this document, the AI agent **must identify and report the conflict to the developer before proceeding**. No changes to the system should be made until the conflict has been clarified and resolved.

## 2. UI Design Rules

### 2.1. Consistency

- `CONS-01`: All frontend pages in the system **must** follow the same rules and have the same consistent, uniform design.
- `CONS-02`: Every component should feel like it belongs to the same website.

### 2.2. Colors

Follow the 60-30-10 rule for the palette structure:

- `COLOR-01`: **60% – Dominant (background/canvas):** clean white #FFFFFF or soft off-white/light gray (#F4F5F7 or #F8F9FA) to maximize data and text legibility.
- `COLOR-02`: **30% – Secondary (structure/sidebars/cards):** soft cool gray, slate, or muted blue-gray for sidebars, headers, and container cards.
- `COLOR-03`: **10% – Accent (CTAs, active states, key actions):** royal blue, deep teal, or vibrant indigo to guide user attention without causing visual fatigue.

In addition:

- `COLOR-04`: Meet WCAG AA standards (minimum 4.5:1 contrast ratio) for all text and background pairings.
- `COLOR-05`: Avoid pure black on pure white. Use dark charcoal (`#1E293B` or `#334155`) for body text instead of harsh `#000000` to reduce glare.
- `COLOR-06`: Keep palettes restrained: stick to 2–3 core hues and let the data do the talking rather than overwhelming the dashboard with bright, clashing colors.

### 2.3. Structure

- `STRUCT-01`: Screen flow must be designed as stated in the screen flow diagram in Report 3. Any problem the AI encounters when referring to the diagram while designing the UI must be reported to the developer in the chat before any work is done on the actual website.
- `STRUCT-02`: The header and navigation must be implemented as reusable, separate components to maintain design consistency across the application. Their content and visibility must be dynamically adjusted based on the authenticated user's assigned roles and permissions.

### 2.4. Tailwind CSS Implementation Standards

- `TW-01`: Use the existing Tailwind CSS framework and established project configuration.
- `TW-02`: Use centralized design tokens defined in the Tailwind theme for colors, typography, border radius, and other shared design values.
- `TW-03`: Prefer existing Tailwind utility classes and design tokens over arbitrary CSS values.
- `TW-04`: Avoid introducing new colors, spacing values, font sizes, shadows, or border radius values without a valid design requirement.
- `TW-05`: Reuse existing React components and component variants whenever possible.
- `TW-06`: Avoid duplicating components by re-styling them separately on multiple pages.
- `TW-07`: Do not introduce inline styles or additional CSS files when existing Tailwind utilities can reasonably achieve the same result.
- `TW-08`: Keep responsive behavior consistent by using Tailwind's responsive utilities and established breakpoints.
- `TW-09`: Do not modify global theme definitions or existing shared components when implementing an isolated page, unless necessary.
- `TW-10`: Ensure that all generated Tailwind classes are compatible with the project's installed Tailwind CSS version.

### 2.5. Typography and Spacing

> Added section – review before adopting.

- `TYPO-01`: Use a single font family, defined in the Tailwind theme, across the entire system. Per-page font overrides are not allowed.
- `TYPO-02`: Use a consistent type scale (page title, section title, body, caption) defined through theme tokens. Heading levels must follow a logical hierarchy (h1 → h2 → h3) without skipping levels.
- `TYPO-03`: Use the Tailwind spacing scale for all padding, margins, and gaps. Equivalent elements must have identical spacing on every page.
- `TYPO-04`: Body text must not be smaller than 14px, and line height must stay comfortable for reading dense data.

### 2.6. Components and Interaction States

> Added section – review before adopting.

- `COMP-01`: **Buttons:** each view has one clear primary action in the accent color. Secondary actions use neutral variants. Destructive actions use a single, consistent danger variant and require confirmation.
- `COMP-02`: **Forms:** labels are placed above inputs, required fields are marked consistently, and validation messages appear next to the relevant field. Input heights and styles must be identical across pages.
- `COMP-03`: **Tables and lists:** use one shared table style with consistent headers, left-aligned text, right-aligned numbers, pagination, and (where relevant) sorting and filtering.
- `COMP-04`: Every interactive component must define default, hover, focus, active, and disabled states. Every data view must define loading, empty, and error states.
- `COMP-05`: Feedback messages (success, warning, error, info) must use one shared notification component. Status colors must be used only to convey status, not for decoration.
- `COMP-06`: Use a single icon library throughout the system.

### 2.7. Accessibility and Responsiveness

> Added section – review before adopting.

- `A11Y-01`: All interactive elements must be reachable and operable by keyboard, with a clearly visible focus indicator.
- `A11Y-02`: Never rely on color alone to convey meaning. Pair status colors with text or icons.
- `A11Y-03`: Use semantic HTML elements and provide accessible names (e.g., labels, `aria-label`) for controls without visible text.
- `A11Y-04`: Layouts must adapt across desktop, tablet, and mobile breakpoints. The sidebar must collapse on small screens, and wide tables must scroll horizontally within their own container instead of breaking the page layout.

## 3. Conflict Reporting Procedure

1. Before implementing any request, compare it against the rules in this document.
2. If a conflict is found, stop and report it to the developer in the chat. The report must state:
   - the affected rule ID (e.g. `TW-07`),
   - how the request conflicts with that rule,
   - a compliant alternative, if one exists.
3. Wait for the developer's decision. Do not modify any part of the system until the conflict has been clarified and resolved.
4. If the developer approves an exception, apply it only to the scope that was approved and note it so this document can be updated if needed.
