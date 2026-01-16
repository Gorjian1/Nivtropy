# DDD Migration Instructions for Codex

## Current State (after this session)

The codebase is in a transitional state with:
- **New DDD architecture** partially implemented in `Domain/`, `Application/`, `Infrastructure/`
- **Legacy models** still used by ViewModels in `Models/` and `Presentation/Models/`
- **Duplicate model files** exist in both locations

### Folder Structure

```
Nivtropy/
├── Domain/                    # NEW: DDD Domain layer
│   ├── Model/                 # Domain entities (TraverseSystem, MeasurementStation, etc.)
│   ├── Services/              # Domain services
│   └── ValueObjects/          # Value objects (Height, Distance, etc.)
├── Application/               # NEW: Application layer (CQRS)
│   ├── Commands/              # Command handlers
│   ├── Queries/               # Query handlers
│   ├── DTOs/                  # Data transfer objects
│   └── Services/              # Application services
├── Infrastructure/            # NEW: Infrastructure layer
│   ├── Parsers/               # File parsers
│   ├── Export/                # Export services
│   └── Persistence/           # Data persistence
├── Presentation/              # UI Layer (WPF)
│   ├── ViewModels/            # MVVM ViewModels (STILL USE LEGACY MODELS)
│   ├── Views/                 # XAML views
│   ├── Models/                # UI-specific models (TraverseRow, LineSummary, etc.)
│   ├── Converters/            # WPF converters
│   └── Resources/             # Resources, styles
├── Models/                    # LEGACY: Old models (MeasurementRecord, GeneratedMeasurement, etc.)
├── Services/                  # Mixed: Some legacy, some new
│   ├── Dialog/                # Dialog service
│   ├── Visualization/         # Visualization services
│   ├── Calculation/           # Calculation services
│   ├── TraverseBuilder.cs     # Legacy traverse builder
│   └── ITraverseBuilder.cs
└── Constants/                 # Application constants
```

## Migration Tasks (Priority Order)

### Phase 1: Fix Compilation (DONE by previous sessions)
- [x] Fix namespace typos (ViewModelss → ViewModels)
- [x] Delete duplicate folders
- [x] Restore missing UI models (PointItem, BenchmarkItem, SharedPointLinkItem)
- [x] Restore missing services (ITraverseBuilder, TraverseBuilder, etc.)
- [x] Fix using directives across all files

### Phase 2: Consolidate Models (NEXT)

**Goal:** Remove duplication between `Models/` and `Presentation/Models/`

Files in `Models/` (namespace `Nivtropy.Models`):
- `DesignRow.cs` - DUPLICATE, check if same as Presentation version
- `GeneratedMeasurement.cs` - UNIQUE, used by DataGeneratorViewModel
- `JournalRow.cs` - DUPLICATE
- `MeasurementRecord.cs` - UNIQUE, core data model
- `OutlierPoint.cs` - DUPLICATE
- `ProfileStatistics.cs` - UNIQUE, used by services
- `RowColoringMode.cs` - DUPLICATE
- `ValidationResult.cs` - UNIQUE

Files in `Presentation/Models/` (namespace `Nivtropy.Presentation.Models`):
- `DesignRow.cs` - UI version
- `JournalRow.cs` - UI version
- `LineSummary.cs` - UNIQUE, used by many ViewModels
- `OutlierPoint.cs` - UI version
- `PointItem.cs` - UNIQUE, UI model
- `RowColoringMode.cs` - UI version
- `SharedPointLinkItem.cs` - UNIQUE, UI model
- `TraverseRow.cs` - UNIQUE, used everywhere
- `TraverseSystem.cs` - UI version (different from Domain/Model/TraverseSystem.cs!)

**Action items:**
1. Compare duplicate files, keep only one version
2. Move unique Models/ files to appropriate location:
   - `MeasurementRecord.cs` → Keep in Models/ or move to Domain if pure data
   - `GeneratedMeasurement.cs` → Keep in Models/ (UI-specific)
   - `ProfileStatistics.cs` → Move to Application/DTOs/ or keep
   - `ValidationResult.cs` → Move to Application/DTOs/
3. Update all using directives after consolidation

### Phase 3: Migrate TraverseCalculationViewModel to DDD

**This is the most complex ViewModel** (~2000 lines, god-file)

Current dependencies:
- `TraverseRow` (Presentation/Models)
- `LineSummary` (Presentation/Models)
- `MeasurementRecord` (Models)
- `ITraverseBuilder` (Services)
- Various calculation methods inline

**Migration strategy:**
1. Extract calculation logic to `Application/Services/TraverseCalculationService.cs`
2. Create Commands/Queries:
   - `LoadMeasurementsCommand` / `LoadMeasurementsHandler`
   - `CalculateTraverseQuery` / `CalculateTraverseHandler`
   - `ApplyCorrectionCommand` / `ApplyCorrectionHandler`
3. Create DTOs for data transfer:
   - `TraverseCalculationResultDto`
   - `StationDataDto`
4. Refactor ViewModel to use CQRS pattern via MediatR or simple dispatch
5. Keep UI-specific models (TraverseRow) for DataGrid binding

### Phase 4: Migrate Other ViewModels

Order of complexity (easiest first):
1. `DataViewModel` - Simple, just displays data
2. `TraverseDesignViewModel` - Medium complexity
3. `NetworkViewModel` - Already partially migrated
4. `DataGeneratorViewModel` - Medium, generates test data
5. `TraverseJournalViewModel` - Complex, depends on TraverseCalculationViewModel

### Phase 5: Clean Up Legacy Services

After ViewModels are migrated:
1. Remove `Services/TraverseBuilder.cs` → Use Domain services
2. Remove `Services/Calculation/TraverseCorrectionService.cs` → Use Application handlers
3. Remove `Services/Calculation/SystemConnectivityService.cs` → Use Domain services
4. Update `ServiceCollectionExtensions.cs` to register only DDD services

## Key Files Reference

### ViewModels that need migration:
- `Presentation/ViewModels/TraverseCalculationViewModel.cs` - GOD FILE, priority #1
- `Presentation/ViewModels/TraverseJournalViewModel.cs` - Depends on above
- `Presentation/ViewModels/DataViewModel.cs`
- `Presentation/ViewModels/DataGeneratorViewModel.cs`
- `Presentation/ViewModels/NetworkViewModel.cs`
- `Presentation/ViewModels/TraverseDesignViewModel.cs`

### DDD infrastructure already in place:
- `Domain/Model/TraverseSystem.cs` - Domain entity
- `Domain/Model/MeasurementStation.cs` - Domain entity
- `Domain/ValueObjects/Height.cs`, `Distance.cs` - Value objects
- `Application/Commands/` - Command infrastructure
- `Application/Queries/` - Query infrastructure
- `Application/DTOs/` - Data transfer objects

### DI Registration:
- `Services/ServiceCollectionExtensions.cs` - Register all services here

## Testing After Changes

After each phase, verify:
1. Build compiles without errors
2. Application launches
3. Data import works
4. Calculations produce correct results
5. UI displays data correctly

## Notes

- `TraverseSystem` exists in TWO places with DIFFERENT implementations:
  - `Domain/Model/TraverseSystem.cs` - Domain entity with business logic
  - `Presentation/Models/TraverseSystem.cs` - UI model with INotifyPropertyChanged

- Keep UI models separate from Domain entities. ViewModels should map between them.

- The goal is NOT to delete all legacy code immediately, but to gradually migrate while keeping the app functional.
