PharmacyERP/
│
├── src/
│   ├── Core/
│   │   ├── PharmacyERP.Domain/                 # Entities, value objects, domain events, enums
│   │   │   ├── Products/
│   │   │   ├── Inventory/
│   │   │   ├── Sales/
│   │   │   ├── Purchases/
│   │   │   ├── Accounting/
│   │   │   ├── B2B/
│   │   │   └── Common/                          # Base entity, AggregateRoot, DomainEvent
│   │   │
│   │   └── PharmacyERP.Application/             # Use cases / business logic orchestration
│   │       ├── Products/
│   │       │   ├── Commands/                    # CreateProduct, UpdateStock, etc.
│   │       │   ├── Queries/                     # SearchProducts, GetProductById
│   │       │   └── Validators/
│   │       ├── Sales/
│   │       │   ├── Commands/                    # CreateSale, ApplyDiscount, FinalizeSale
│   │       │   └── Queries/
│   │       ├── Inventory/
│   │       ├── Purchases/
│   │       ├── Accounting/
│   │       ├── Common/
│   │       │   ├── Interfaces/                  # IProductRepository, IUnitOfWork — defined here, implemented in Infrastructure
│   │       │   └── Behaviors/                   # Cross-cutting: validation, logging pipeline
│   │       └── DependencyInjection.cs
│   │
│   ├── Infrastructure/
│   │   ├── PharmacyERP.Infrastructure.Data/     # EF Core / Dapper implementations, DbContext, migrations
│   │   │   ├── Repositories/
│   │   │   ├── Migrations/
│   │   │   └── Persistence/
│   │   ├── PharmacyERP.Infrastructure.Sync/     # Outbox, sync engine, conflict resolution
│   │   ├── PharmacyERP.Infrastructure.Hardware/ # Barcode scanner, printer, cash drawer abstractions
│   │   └── PharmacyERP.Infrastructure.Identity/ # Auth, permissions
│   │
│   ├── Clients/
│   │   ├── PharmacyERP.Desktop/                 # WinUI 3 app
│   │   │   ├── Views/
│   │   │   │   ├── POS/
│   │   │   │   ├── Inventory/
│   │   │   │   ├── Purchases/
│   │   │   │   ├── Accounting/
│   │   │   │   └── Shell/                        # App shell, navigation frame
│   │   │   ├── ViewModels/                       # Mirrors Views/ folder-for-folder
│   │   │   ├── Controls/                         # Reusable custom controls
│   │   │   ├── Commands/                         # ICommand implementations (ties to your keyboard-shortcut layer)
│   │   │   ├── Keymaps/                          # Your MARG-compatible / default keymap profiles (JSON + loader)
│   │   │   └── App.xaml.cs
│   │   │
│   │   └── (future) PharmacyERP.Mobile/
│   │
│   ├── Services/
│   │   ├── PharmacyERP.LocalServer/              # ASP.NET Core API — runs at branch/pharmacy level
│   │   └── PharmacyERP.CloudApi/                 # ASP.NET Core API — cloud platform
│   │
│   └── Shared/
│       └── PharmacyERP.Contracts/                # DTOs shared between client ↔ local server ↔ cloud
│
├── tests/
│   ├── PharmacyERP.Domain.Tests/
│   ├── PharmacyERP.Application.Tests/
│   ├── PharmacyERP.Infrastructure.Tests/         # Testcontainers-based Postgres integration tests
│   └── PharmacyERP.Desktop.Tests/                # ViewModel tests
│
├── build/                                        # CI/CD scripts, Dockerfiles
├── docs/                                         # Your TRD, shortcut mapping, ADRs live here
└── PharmacyERP.sln