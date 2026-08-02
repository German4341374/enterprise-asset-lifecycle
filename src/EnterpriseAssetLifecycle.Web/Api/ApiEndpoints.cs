using EnterpriseAssetLifecycle.Contracts;
using EnterpriseAssetLifecycle.Domain;
using EnterpriseAssetLifecycle.Infrastructure;
using EnterpriseAssetLifecycle.Services;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAssetLifecycle.Api;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapAssetApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api").WithTags("Enterprise asset lifecycle");
        var assets = api.MapGroup("/assets").WithTags("Assets");

        assets.MapGet("/", async (
            string? search,
            AssetState? state,
            AssetType? type,
            Guid? departmentId,
            int page,
            int pageSize,
            AssetQueryService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(
                search,
                state,
                type,
                departmentId,
                page == 0 ? 1 : page,
                pageSize == 0 ? 25 : pageSize,
                cancellationToken)))
            .WithName("ListAssets")
            .Produces<PageResult<AssetDto>>();

        assets.MapGet("/{id:guid}", async (
            Guid id,
            AssetLifecycleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(id, cancellationToken)))
            .WithName("GetAsset")
            .Produces<AssetDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        assets.MapPost("/", async (
            CreateAssetRequest body,
            HttpContext context,
            AssetLifecycleService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RegisterAsync(
                body,
                Actor(context),
                context.TraceIdentifier,
                cancellationToken);
            return Results.Created($"/api/assets/{result.Id}", result);
        })
            .AddEndpointFilter<RequestValidationFilter<CreateAssetRequest>>()
            .WithName("RegisterAsset")
            .Produces<AssetDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        assets.MapPost("/{id:guid}/assign", async (
            Guid id,
            AssignAssetRequest body,
            HttpContext context,
            AssetLifecycleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.AssignAsync(id, body, context.TraceIdentifier, cancellationToken)))
            .AddEndpointFilter<RequestValidationFilter<AssignAssetRequest>>()
            .WithName("AssignAsset")
            .Produces<AssetDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        assets.MapPost("/{id:guid}/return", async (
            Guid id,
            ReturnAssetRequest body,
            HttpContext context,
            AssetLifecycleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ReturnAsync(id, body, context.TraceIdentifier, cancellationToken)))
            .AddEndpointFilter<RequestValidationFilter<ReturnAssetRequest>>()
            .WithName("ReturnAsset")
            .Produces<AssetDto>();

        assets.MapPost("/{id:guid}/move", async (
            Guid id,
            MoveAssetRequest body,
            HttpContext context,
            AssetLifecycleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.MoveAsync(id, body, context.TraceIdentifier, cancellationToken)))
            .AddEndpointFilter<RequestValidationFilter<MoveAssetRequest>>()
            .WithName("MoveAsset")
            .Produces<AssetDto>();

        assets.MapPost("/{id:guid}/repairs", async (
            Guid id,
            StartRepairRequest body,
            HttpContext context,
            AssetLifecycleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.StartRepairAsync(id, body, context.TraceIdentifier, cancellationToken)))
            .AddEndpointFilter<RequestValidationFilter<StartRepairRequest>>()
            .WithName("StartAssetRepair")
            .Produces<AssetDto>();

        assets.MapPost("/{id:guid}/repairs/complete", async (
            Guid id,
            CompleteRepairRequest body,
            HttpContext context,
            AssetLifecycleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.CompleteRepairAsync(id, body, context.TraceIdentifier, cancellationToken)))
            .AddEndpointFilter<RequestValidationFilter<CompleteRepairRequest>>()
            .WithName("CompleteAssetRepair")
            .Produces<AssetDto>();

        assets.MapPost("/{id:guid}/retire", async (
            Guid id,
            RetireAssetRequest body,
            HttpContext context,
            AssetLifecycleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.RetireAsync(id, body, context.TraceIdentifier, cancellationToken)))
            .AddEndpointFilter<RequestValidationFilter<RetireAssetRequest>>()
            .WithName("RetireAsset")
            .Produces<AssetDto>();

        assets.MapPut("/{id:guid}/warranty", async (
            Guid id,
            UpsertWarrantyRequest body,
            HttpContext context,
            AssetLifecycleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.UpsertWarrantyAsync(id, body, context.TraceIdentifier, cancellationToken)))
            .AddEndpointFilter<RequestValidationFilter<UpsertWarrantyRequest>>()
            .WithName("UpsertAssetWarranty")
            .Produces<AssetDto>();

        assets.MapPost("/{id:guid}/software", async (
            Guid id,
            InstallSoftwareRequest body,
            HttpContext context,
            AssetLifecycleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.InstallSoftwareAsync(id, body, context.TraceIdentifier, cancellationToken)))
            .AddEndpointFilter<RequestValidationFilter<InstallSoftwareRequest>>()
            .WithName("InstallAssetSoftware")
            .Produces<AssetDto>();

        assets.MapGet("/{id:guid}/events", async (
            Guid id,
            AssetQueryService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.EventsAsync(id, cancellationToken)))
            .WithName("GetAssetAuditTrail")
            .Produces<IReadOnlyList<AssetEventDto>>();

        assets.MapGet("/export.csv", async (AssetCsvService service, CancellationToken cancellationToken) =>
        {
            var stream = new MemoryStream();
            await service.WriteExportAsync(stream, cancellationToken);
            stream.Position = 0;
            return Results.File(stream, "text/csv; charset=utf-8", $"assets-{DateTime.UtcNow:yyyyMMdd}.csv");
        })
            .WithName("ExportAssets")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv");

        api.MapPost("/imports/assets", async (
            IFormFile file,
            HttpContext context,
            AssetCsvService service,
            CancellationToken cancellationToken) =>
        {
            var key = context.Request.Headers["Idempotency-Key"].ToString();
            await using var stream = file.OpenReadStream();
            return Results.Ok(await service.ImportAsync(
                stream,
                file.Length,
                key,
                Actor(context),
                cancellationToken));
        })
            .DisableAntiforgery()
            .WithName("ImportAssets")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ImportResultDto>();

        api.MapGet("/dashboard", async (
            AssetQueryService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.DashboardAsync(cancellationToken)))
            .WithName("GetDashboard")
            .Produces<DashboardDto>();

        var departments = api.MapGroup("/departments").WithTags("Departments");
        departments.MapGet("/", async (AssetDbContext db, CancellationToken cancellationToken) =>
            Results.Ok(await db.Departments.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new DepartmentDto(x.Id, x.Code, x.Name))
                .ToListAsync(cancellationToken)));
        departments.MapPost("/", async (
            CreateDepartmentRequest body,
            AssetDbContext db,
            CancellationToken cancellationToken) =>
        {
            var department = new Department { Code = body.Code.Trim(), Name = body.Name.Trim() };
            db.Departments.Add(department);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/departments/{department.Id}", department);
        }).AddEndpointFilter<RequestValidationFilter<CreateDepartmentRequest>>();

        var employees = api.MapGroup("/employees").WithTags("Employees");
        employees.MapGet("/", async (AssetDbContext db, CancellationToken cancellationToken) =>
            Results.Ok(await db.Employees.AsNoTracking().Include(x => x.Department)
                .OrderBy(x => x.FullName)
                .Select(x => new EmployeeDto(
                    x.Id,
                    x.EmployeeNumber,
                    x.FullName,
                    x.Email,
                    x.DepartmentId,
                    x.Department!.Name,
                    x.IsActive))
                .ToListAsync(cancellationToken)));
        employees.MapPost("/", async (
            CreateEmployeeRequest body,
            AssetDbContext db,
            CancellationToken cancellationToken) =>
        {
            _ = await db.Departments.FindAsync([body.DepartmentId], cancellationToken)
                ?? throw new ResourceNotFoundException(nameof(Department), body.DepartmentId);
            var employee = new Employee
            {
                EmployeeNumber = body.EmployeeNumber.Trim(),
                FullName = body.FullName.Trim(),
                Email = body.Email.Trim().ToLowerInvariant(),
                DepartmentId = body.DepartmentId
            };
            db.Employees.Add(employee);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/employees/{employee.Id}", employee);
        }).AddEndpointFilter<RequestValidationFilter<CreateEmployeeRequest>>();

        return endpoints;
    }

    private static string Actor(HttpContext context)
    {
        var actor = context.Request.Headers["X-Actor"].ToString();
        return string.IsNullOrWhiteSpace(actor) ? "api-client" : actor[..Math.Min(actor.Length, 120)];
    }
}
