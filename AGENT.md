# AGY Agent Protocol & Architecture Directives — Medistock-offlinefirst

> **Mandatory Instructions for Antigravity AI Agent (AGY CLI)**  
> This file acts as the primary project operational memory and directive sheet for developing the **Medistock-offlinefirst** Pharmacy ERP application.

---

## 1. Context Retention & Session Resumption Protocol

To guarantee zero loss of context memory across CLI sessions or system restarts:

### A. Initialization on Startup
At the start of **EVERY session** or user request, the agent MUST:
1. Inspect `AGY_STATE.md` to restore current project status, previous milestones achieved, pending tasks, and architecture decisions.
2. Read `AGENT.md` (this file) to re-align on strict coding and architectural rules.

### B. Session State Update Requirement
At the conclusion of **EVERY successful step/turn**, the agent MUST update `AGY_STATE.md`:
- Record newly completed steps/features.
- Log updated architecture decisions or schema modifications.
- Explicitly state the exact **Next Action Items** so future sessions pick up seamlessly.

---

## 2. Core Project Architecture & Domain Rules

### A. Pharmacy ERP Specifications
- **Domain Target:** Indian Chemists / Pharmacies (Replacing MARG ERP, Medicin ERP).
- **Core Modules:** POS & Billing, Batch & Expiry Tracking, Purchase Entry, GST Calculation (CGST/SGST vs IGST by state & HSN), Schedule-H/H1 Narcotics Safeguards, Strip-to-Tablet Conversions, and Financial Accounts.
- **Topology:** Local-First Desktop App.
  - Phase 1: Local SQLite DB per workstation (WAL mode).
  - Phase 2: Local PostgreSQL LAN Server for multi-counter store sync.
  - Phase 3: Cloud background sync queue (Azure/PostgreSQL).

### B. Strict Performance Requirements (`trd.md`)
- **Hot Path Data Access:** Use **Dapper / Raw ADO.NET** with direct lightweight DTO projection for POS barcode scan, product search, cart mutations, and stock commits. DO NOT use EF Core on hot paths.
- **Cold Path Data Access:** **EF Core** for admin screens, settings, reporting, and non-realtime accounting.
- **Product Search Engine:**
  - SQLite **FTS5 Inverted Index** shadow table for products (Search by Name, Generic, Salt, Manufacturer, Barcode).
  - 150ms-180ms keystroke debounce + in-flight cancellation.
  - Sub-100ms latency target on low-spec baseline rig (4GB RAM, dual-core).
- **UI & Rendering Rules:**
  - UI Virtualization (`ItemsRepeater`) for all lists > 50 items.
  - Flat visual templates (avoid nested panels).
  - No synchronous I/O on UI main thread.
  - Compiled bindings (`x:Bind`) on hot path screens.

### C. Keyboard & Input Architecture (`KEYBOARD_SHORTCUTS.md`)
- **Command Layer First:** NEVER wire a keyboard shortcut directly to business logic or a database call. Every action is an `ICommand`. Shortcuts, buttons, context menus, command palette — all invoke the same command.
- **Two keymap profiles:** Medistock Standard (Ctrl+S = Save) and MARG-Compatible (Ctrl+S = Flush Cache, Ctrl+W = Save). Profile selected by user in Settings → Keyboard. Applied instantly.
- **Scope system mandatory:** F2, F3, F4 do different things per active screen context. Each screen registers its own local scope. Deepest scope wins on key press.
- **Barcode scanners = HID keyboard:** POS search field must always re-focus after each cart action. Zero extra keystrokes between consecutive scans.
- **Mouse parity:** Every keyboard action is also reachable via mouse. All buttons show shortcut in tooltip.
- **Alt+F1 = In-app shortcut help overlay:** Shows only the active scope's shortcuts. Must be implemented from day one.

### D. UI Design System (`UI_DESIGN_SYSTEM.md`)
- **Dual theme (Light + Dark):** Both fully specified. User selects at first launch wizard. Switch anytime, no restart.
- **Color tokens only:** Never hardcode hex in component code. All colors via semantic ResourceDictionary tokens.
- **8 accent color presets:** User selectable. Pharmacy Green is default.
- **Density system:** Compact / Normal / Comfortable — all row heights and paddings derive from this setting.
- **POS screen performance override:** Zero Mica/Acrylic/animations on POS. Flat render only.

---

## 3. Workflow, Verification & Communication Rules

### A. Pre-Execution Clarification & Understanding
- **Understand Before Coding:** Deeply analyze requirements before making any code modifications.
- **Direct Clarification:** If any requirement, edge case, or architecture detail is ambiguous or not 100% clear, **ask the user directly** first. Proceed with implementation only once fully understood and aligned.

### B. Post-Response Build & Auto-Launch Verification
- **Mandatory Build & Launch:** After every turn/response involving code changes, the agent MUST:
  1. Build/publish the project to verify zero compilation or packaging errors.
  2. Launch/open the software automatically using commands (e.g. `Start-Process` / `dotnet run`) so the user can immediately test and verify the live application.
- **Empirical Log Verification:** Never claim success without running build/test/launch commands with live exit code verification.
- **Superficial Patching Forbidden:** Always fix root causes rather than swallowing exceptions or returning dummy data.
- **No Assumptions:** Always inspect files directly before and after editing.

---

## 4. Quick Index of Reference Documents
- [PRD.md](file:///D:/Projects/Medistock-offlinefirst/PRD.md) — Product Requirements Document (features, acceptance criteria, NFRs)
- [ARCHITECTURE.md](file:///D:/Projects/Medistock-offlinefirst/ARCHITECTURE.md) — Master system blueprint (v1.1, 23 sections)
- [UI_DESIGN_SYSTEM.md](file:///D:/Projects/Medistock-offlinefirst/UI_DESIGN_SYSTEM.md) — Design tokens, components, themes, performance rules
- [KEYBOARD_SHORTCUTS.md](file:///D:/Projects/Medistock-offlinefirst/KEYBOARD_SHORTCUTS.md) — Full keyboard/input architecture, both keymap profiles, scope system
- [AGY_STATE.md](file:///D:/Projects/Medistock-offlinefirst/AGY_STATE.md) — Active session state and progress log

