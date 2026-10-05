# GDA ERP

.NET 10 microservices ERP for GDA (Galiyat Development Authority): Identity, Property Management, Assets Management
and a YARP gateway over one PostgreSQL database (`ERP_DB`, one schema per service).

@docs/claude/overview.md

## Before working on a service, read its file

- Identity (auth, users, roles, permissions, JWT): `docs/claude/identity.md`
- Property Management (property register, GDA Act rules): `docs/claude/property-management.md`
- Assets Management (fixed-asset register): `docs/claude/assets-management.md`
- API gateway (YARP): `docs/claude/api-gateway.md`

## Ground rules

- Follow the existing patterns in the service you touch: Clean Architecture layers, CQRS use-case folders, Carter
  endpoints, no `namespace` lines, 2-space indentation.
- Business rules belong in domain methods, not in handlers or endpoints.
- Schema changes go through an EF migration in that service's Infrastructure project. For Property, keep
  `docs/gda_property_module.dbml` in step with the schema.
- Build with `dotnet build src/ERP.slnx` and test with `dotnet test src/ERP.slnx` before saying a change is done.
- Work on a feature branch. Don't push to `main` unless the user asks.
- When code and these docs disagree, trust the code and update the doc in the same change.
