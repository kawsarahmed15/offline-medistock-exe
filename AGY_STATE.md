# AGY CLI Memory & Progress State Log
> **Project:** Medistock-offlinefirst (Pharmacy ERP Platform)  
> **Last Updated:** 2026-09-24 15:29:00 UTC  
> **Current Phase:** Phase 0 — Architecture & Foundation Documents  

---

## 1. System Vision (Summary)
**Build:** An offline-first, multi-branch pharmacy operating system with integrated accounting, procurement, B2B pharmacy-to-wholesaler commerce, inventory intelligence, compliance, and cloud management.

**NOT:** "MARG with a nicer UI."

---

## 2. Tech Stack (Frozen)
| Layer | Technology |
|---|---|
| Desktop UI | C# / WinUI 3 / .NET 10 LTS / Windows App SDK |
| Client Architecture | MVVM + Clean Architecture |
| Local Client DB | SQLite WAL + FTS5 shadow table |
| Hot Path Data Access | **Dapper / Raw ADO.NET** (POS, search, cart — NO EF Core) |
| Cold Path Data Access | **EF Core** (admin, reports, settings) |
| Local Server DB | PostgreSQL (branch authoritative source of truth) |
| Cloud API | ASP.NET Core modular monolith |
| Cloud DB | PostgreSQL + read replicas + PITR |
| Cache | Redis |
| Messaging | PostgreSQL Outbox → (Service Bus/Kafka at scale) |
| Auth | OIDC/OAuth 2.1 + MFA |
| Observability | OpenTelemetry + Serilog |
| CI/CD | GitHub Actions / Azure DevOps |
| Desktop Deploy | Signed MSIX + auto-updater |

---

## 3. Key Architecture Decisions (Locked)
- **3-tier topology:** Workstation SQLite ↔ Local Branch PostgreSQL ↔ Cloud PostgreSQL
- **Outbox pattern** for all sync — transactions are never lost
- **Idempotency keys** on every outbox event — no duplicate processing
- **Document sequences** via locked DB row — no `MAX(no)+1` race conditions
- **ULIDs** as internal IDs; human-readable numbers as separate `invoice_no` field
- **org_id always from JWT** — never from request body (tenant isolation)
- **Financial records immutable** once posted — corrections via reversal/credit note only
- **External APIs (GST, payment)** always async via outbox — never block sale commit

---

## 4. TRD Performance Targets (Non-Negotiable)
| Operation | Target | Hard Ceiling |
|---|---|---|
| Barcode scan → cart | ≤ 100ms | 200ms |
| Keystroke → search | ≤ 100ms | 150ms |
| Cold start → POS ready | ≤ 3s | 5s |
| Sale → DB commit | ≤ 50ms | 100ms |
| Module navigation | ≤ 150ms | 300ms |
| 5,000+ row grid scroll | 60fps | Smooth |

---

## 5. Session Progress Log

### ✅ Step 1 — File Inspection & Context Capture
Analyzed: `trd.md`, `marg-research.md`, `chatgpt-conversation.md` (3786 lines), `chatgpt-conversation-research.md`

### ✅ Step 2 — Context Memory System Setup
Created: `AGENT.md` (rules & protocols) + `AGY_STATE.md` (this file)

### ✅ Step 3 — Master Architecture Document
Created: `ARCHITECTURE.md` — 22 sections covering every system component.

### ✅ Step 4 — UI/UX Design System
Created: `UI_DESIGN_SYSTEM.md` — 20 sections covering:
- Design philosophy ("Operational depth of MARG + modern SaaS clarity + native performance")
- **Dual theme system** (Light + Dark, user selects at first launch, switchable anytime — no restart)
- First-launch Theme Selection Wizard (7-step flow: welcome → theme → accent → density → font → org setup → done)
- Full color token system (50+ tokens, semantic names, both theme values, pharmacy-specific tokens)
- **8 accent color presets** user-selectable (Pharmacy Green default, Ocean Blue, Indigo, Teal, Violet, Rose, Amber, Slate)
- Typography: Lexend (headings) + Source Sans 3 (body) + Fira Code (monospace for codes)
- Type scale (10px–32px), 4 weight levels, 3 user font size options
- Spacing system (8dp grid, 8 levels), density system (Compact/Normal/Comfortable)
- Window layout grid (sidebar 240px collapsible to 56px, 1280×720 min)
- Component library: Buttons (5 types), Inputs (4 states), Data tables (virtualized), Status badges, KPI cards, Toasts, Dialogs
- App shell: sidebar nav, top toolbar, Command Palette (Ctrl+K) with global search
- POS screen (performance-critical, no Mica/animations, two-panel split, F-key shortcuts)
- Payment screen with change calculator
- Role-based dashboards (Owner, Manager, Pharmacist, Accountant — different widgets)
- Inventory grid (ItemsRepeater, flat templates, filter slide-in panel)
- Purchase entry form (tab-key navigation, barcode fill, auto-GST)
- B2B transaction timeline UI (visual step-by-step with colored dot states)
- Chart types per screen (LiveCharts2/SkiaSharp), chart performance rules
- User customization panel (12 settings, all apply instantly without restart)
- Motion/animation rules (zero on POS, budgets per screen type, reduced-motion support)
- Accessibility (WCAG 2.1 AA, focus rings, color contrast, keyboard nav, screen reader)
- Performance budget (60fps scroll, ≤150ms navigation, ≤180ms dialogs)
- WinUI 3 implementation notes (ResourceDictionary structure, x:Bind rules, ItemsRepeater pattern)
- Anti-patterns list (20 banned patterns with reasons)

### ✅ Step 5 — Keyboard & Input Architecture
Created: `KEYBOARD_SHORTCUTS.md` — 15 sections covering:
- **Command Layer First** rule: every action is an `ICommand`; shortcuts are a config binding layer, never direct-to-logic wiring
- **Two keymap profiles:** Medistock Standard (Ctrl+S=Save) and MARG-Compatible (Ctrl+S=Flush Cache) — user-selectable, instant switch
- **Scope system:** Global → Screen → Panel → Field — deepest scope wins; F2/F3/F4 safely reused per context
- **MARG ERP 9+ full shortcut parity** across: POS sale screen, payment, purchase entry, accounting/vouchers, product/inventory, reports
- Complete **Conflict Resolution** decisions (Ctrl+S, Ctrl+T, Ctrl+Tab, Alt+I, F1) — all resolved with rationale
- **POS keyboard-only flow:** F2 → type/scan → Enter → Tab → type qty → F6 → type amount → Enter = full sale in 4-8 keystrokes
- **4-keystroke payment flow:** F6 → type amount → Enter → receipt printed
- **Barcode scanner protocol:** HID keyboard mode; search field always re-focused after cart actions; zero extra keystrokes between scans
- **Mouse parity:** every shortcut also reachable by mouse; all buttons show shortcut in tooltip
- **In-app shortcut help** (Alt+F1): context-aware overlay showing only current scope's shortcuts
- Both `ARCHITECTURE.md` (Section 16 added) and `AGENT.md` (Section C+D added) updated with keyboard rules

Updated: `ARCHITECTURE.md` v1.1 — keyboard architecture section added (Section 16), 3 new anti-patterns added  
Updated: `AGENT.md` — keyboard command-layer rules + full reference index

### ✅ Step 6 — Full Product Requirements Document (PRD)
Created: `PRD.md` — 26 sections, 50+ NFRs, priority ratings P1-P4, acceptance criteria for all modules (POS, Inventory, Accounting, GST, B2B, Multi-Branch, Platform).

### ✅ Step 7 — Git Setup & Repository Cleanup
- Initialized local git repository on branch `main`.
- Created comprehensive `.gitignore` tailored for .NET 10, WinUI 3, Windows App SDK, and SQLite.
- Removed obsolete intermediate scratch research files (`chatgpt-conversation.md`, `chatgpt-conversation-research.md`, `marg-research.md`, `shortcut-mapping.md`, `trd.md`) after full synthesis into the final documentation suite.
- Created initial commit `a9a5fd3` containing the 6 final authoritative specification documents.
- Clean working tree verified.

---

## 6. Next Action Items (Phase 0 → Phase 1)
**All Phase 0 foundation documents are COMPLETE. Ready for Phase 1:**
1. **Set up remote Git repository:** Configure `origin` remote URL when provided by user (`git remote add origin <url>` and `git push -u origin main`).
2. **Set up solution structure:** `Medistock.sln` with projects: `Medistock.Desktop` (WinUI 3), `Medistock.Core` (domain), `Medistock.Infrastructure` (data access), `Medistock.LocalApi` (ASP.NET Core), `Medistock.CloudApi` (ASP.NET Core).
3. **Finalize DB migration scripts:** SQLite schema (workstation), PostgreSQL schema (local server + cloud).
4. **Build vertical slice #1:** Barcode-scan → FTS5 search → add to cart → stock deduct → local commit — measure on baseline rig against NFR-PERF targets.

---

## 7. Master Reference Documents
- [PRD.md](file:///D:/Projects/Medistock-offlinefirst/PRD.md) — Product Requirements Document (features, acceptance criteria, NFRs)
- [ARCHITECTURE.md](file:///D:/Projects/Medistock-offlinefirst/ARCHITECTURE.md) — Master system blueprint (v1.1, 23 sections)
- [UI_DESIGN_SYSTEM.md](file:///D:/Projects/Medistock-offlinefirst/UI_DESIGN_SYSTEM.md) — Design tokens, components, themes, performance rules
- [KEYBOARD_SHORTCUTS.md](file:///D:/Projects/Medistock-offlinefirst/KEYBOARD_SHORTCUTS.md) — Full keyboard/input architecture, both keymap profiles, scope system
- [AGENT.md](file:///D:/Projects/Medistock-offlinefirst/AGENT.md) — Agent operational rules & coding protocols
- [AGY_STATE.md](file:///D:/Projects/Medistock-offlinefirst/AGY_STATE.md) — Active session state and progress log
